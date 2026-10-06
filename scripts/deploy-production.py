"""Promove imagem homologada; preserva dados e restaura o contêiner em falha."""
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import time
from datetime import datetime, timezone
import urllib.request
import fcntl

ROOT = Path("/home/dev/prisma-deploy")
CONTAINER = "prisma-workspace-api"
SQL_CONTAINER = "detran-kanban-db"
DATA_ROOT = Path("/home/dev/painel-projects/prisma-runtime")
BRIDGE_NETWORK = "prisma_ai_bridge"


def allowed_networks(current):
    networks = set(current["NetworkSettings"]["Networks"])
    if networks not in ({"slc_default"}, {"slc_default", BRIDGE_NETWORK}):
        raise ValueError("Rede de produção diferente da instalação documentada.")
    return networks


def bridge_environment():
    path = ROOT / "ai-bridge.env"
    if not path.exists():
        return {}
    if path.stat().st_mode & 0o077:
        raise ValueError("Configuração privada da ponte exige permissão 600.")
    values = dict(line.split("=", 1) for line in path.read_text().splitlines() if line)
    if set(values) != {"Ai__BridgeToken", "Ai__BridgeUrl"} or len(values["Ai__BridgeToken"]) < 32 or values["Ai__BridgeUrl"] != "http://prisma-ai-bridge:8080/v1":
        raise ValueError("Configuração externa da ponte inválida.")
    network = inspect(BRIDGE_NETWORK)
    if network.get("Labels", {}).get("prisma.purpose") != "ai-bridge" or any(member.get("Name") not in {CONTAINER, "prisma-ai-bridge"} for member in network.get("Containers", {}).values()):
        raise ValueError("A ponte deve estar em rede exclusiva, sem banco ou outros serviços.")
    if not healthy("prisma-ai-bridge"):
        raise ValueError("Ponte CLI não ficou saudável; promoção interrompida.")
    return values


def run(*args, capture=False, env=None):
    return subprocess.check_output(args, text=True, env=env).strip() if capture else subprocess.check_call(args, env=env)


def inspect(target):
    return json.loads(run("docker", "inspect", target, capture=True))[0]


def healthy(name):
    for _ in range(90):
        state = inspect(name)["State"]
        if state.get("Health", {}).get("Status") == "healthy":
            return True
        if state.get("Status") in ("exited", "dead"):
            return False
        time.sleep(2)
    return False


def deploy(sha):
    if not re.fullmatch(r"[0-9a-f]{40}", sha):
        raise ValueError("Commit inválido.")
    incoming = ROOT / "incoming" / sha
    archive = incoming / "prisma-image.tar.gz"
    expected = (incoming / "prisma-image.sha256").read_text().split()[0]
    with archive.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    if digest != expected:
        raise ValueError("Checksum da imagem divergente.")
    manifest = json.loads((incoming / "migrations.json").read_text())
    baseline = json.loads((ROOT / "migrations.json").read_text())
    if manifest != baseline:
        raise ValueError("Migrations diferentes da produção: requer G-MIGRATION e operação explícita.")
    run("docker", "load", "--input", str(archive))
    image = "prisma-workspace:" + sha
    revision = inspect(image)["Config"].get("Labels", {}).get("org.opencontainers.image.revision")
    if revision != sha:
        raise ValueError("A imagem não corresponde ao commit autorizado.")
    current = inspect(CONTAINER)
    if current["Config"].get("Labels", {}).get("org.opencontainers.image.revision") == sha:
        if not healthy(CONTAINER):
            raise RuntimeError("A imagem já está instalada, mas não está saudável.")
        print("Commit já publicado e saudável:", sha)
        return
    networks = allowed_networks(current)
    bridge_values = bridge_environment()
    if bridge_values:
        networks.add(BRIDGE_NETWORK)
    if any(current["HostConfig"].get("PortBindings", {}).values()):
        raise ValueError("Portas publicadas inesperadas; revisar configuração antes de promover.")
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    backup = Path("/home/dev/backups/prisma") / (stamp + "-" + sha[:12])
    backup.mkdir(parents=True, mode=0o700)
    sql_environment = dict(item.split("=", 1) for item in inspect(SQL_CONTAINER)["Config"]["Env"])
    password = sql_environment.get("MSSQL_SA_PASSWORD") or sql_environment["SA_PASSWORD"]
    sql_path = "/var/opt/mssql/backups/PrismaWorkspace_" + stamp + ".bak"
    run("docker", "exec", SQL_CONTAINER, "mkdir", "-p", "/var/opt/mssql/backups")
    query = f"BACKUP DATABASE [PrismaWorkspace] TO DISK=N'{sql_path}' WITH COPY_ONLY,CHECKSUM; RESTORE VERIFYONLY FROM DISK=N'{sql_path}' WITH CHECKSUM;"
    run("docker", "exec", "-e", "SQLCMDPASSWORD", SQL_CONTAINER,
        "/opt/mssql-tools18/bin/sqlcmd", "-S", "localhost", "-U", "sa", "-C", "-b", "-Q", query,
        env={**os.environ, "SQLCMDPASSWORD": password})
    run("docker", "cp", SQL_CONTAINER + ":" + sql_path, str(backup / "database.bak"))
    run("docker", "cp", CONTAINER + ":/app/App_Data", str(backup / "App_Data"))
    old_name = CONTAINER + "-rollback-" + stamp
    environment = dict(item.split("=", 1) for item in current["Config"]["Env"])
    environment.update(bridge_values)
    command = ["docker", "create", "--name", CONTAINER, "--restart", "unless-stopped", "--network", "slc_default"]
    for alias in current["NetworkSettings"]["Networks"]["slc_default"].get("Aliases") or []:
        if alias != current["Id"]:
            command += ["--network-alias", alias]
    for key in environment:
        command += ["--env", key]
    has_data_mount = False
    for mount in current["Mounts"]:
        source = mount["Source"] if mount["Type"] == "bind" else mount["Name"]
        command += ["--volume", source + ":" + mount["Destination"] + ("" if mount["RW"] else ":ro")]
        has_data_mount |= mount["Destination"] == "/app/App_Data"
    command += ["--label", "org.opencontainers.image.revision=" + sha]
    if not has_data_mount:
        data = DATA_ROOT / "App_Data"
        if data.exists():
            raise ValueError("Diretório persistente de anexos já existe sem mount: revisar antes de substituir.")
        command += ["--volume", str(data) + ":/app/App_Data"]
    command.append(image)
    run("docker", "stop", "--time", "30", CONTAINER)
    try:
        # A cópia final é feita com a aplicação parada para preservar uploads recentes.
        if not has_data_mount:
            run("docker", "cp", CONTAINER + ":/app/App_Data", str(DATA_ROOT / "App_Data"))
        run("docker", "rename", CONTAINER, old_name)
        run(*command, env={**os.environ, **environment})
        if BRIDGE_NETWORK in networks:
            bridge_command = ["docker", "network", "connect"]
            for alias in current["NetworkSettings"]["Networks"].get(BRIDGE_NETWORK, {}).get("Aliases") or []:
                if alias != current["Id"]:
                    bridge_command += ["--alias", alias]
            run(*bridge_command, BRIDGE_NETWORK, CONTAINER)
        run("docker", "start", CONTAINER)
        if not healthy(CONTAINER):
            raise RuntimeError("A imagem candidata não ficou saudável.")
        with urllib.request.urlopen("https://prisma.nordevs.com.br/health", timeout=20) as response:
            if response.status != 200:
                raise RuntimeError("Healthcheck público recusado.")
    except Exception:
        # Só remove a candidata; banco, volumes e contêiner anterior permanecem.
        candidate = subprocess.run(["docker", "inspect", CONTAINER], capture_output=True)
        if candidate.returncode == 0 and json.loads(candidate.stdout)[0]["Id"] != current["Id"]:
            subprocess.run(["docker", "rm", "--force", CONTAINER], check=True)
        previous = subprocess.run(["docker", "inspect", old_name], capture_output=True)
        if previous.returncode == 0:
            run("docker", "rename", old_name, CONTAINER)
        if not has_data_mount and (DATA_ROOT / "App_Data").exists():
            (DATA_ROOT / "App_Data").rename(backup / "candidate-App_Data")
        run("docker", "start", CONTAINER)
        if not healthy(CONTAINER):
            raise RuntimeError("Rollback iniciado, mas a saúde exige intervenção.")
        print("Rollback concluído; produção anterior restaurada.", file=sys.stderr)
        raise
    (ROOT / "current-commit").write_text(sha + "\n")
    (backup / "rollback-container").write_text(old_name + "\n")
    print("Publicado e saudável:", sha, "Rollback:", old_name)


if __name__ == "__main__":
    ROOT.mkdir(mode=0o700, exist_ok=True)
    with (ROOT / "deploy.lock").open("w") as lock:
        fcntl.flock(lock, fcntl.LOCK_EX)
        try:
            deploy(sys.argv[1])
        except Exception as error:
            print("Deploy interrompido:", str(error), file=sys.stderr)
            sys.exit(1)
