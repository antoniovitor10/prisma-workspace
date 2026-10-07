"""Instala a ponte opcional no VPS; conta real é autenticada pelo administrador na UI."""
import fcntl
import json
import os
from pathlib import Path
import re
import secrets
import subprocess
import sys
import time
from datetime import datetime, timezone

ROOT = Path("/home/dev/prisma-deploy")
NETWORK = "prisma_ai_bridge"
CONTAINER = "prisma-ai-bridge"


def run(*args, capture=False, env=None):
    if capture:
        return subprocess.check_output(args, text=True, env=env).strip()
    subprocess.check_call(args, env=env)


def exists(kind, name):
    return subprocess.run(["docker", kind, "inspect", name], capture_output=True).returncode == 0


def validate_existing(current, token):
    environment = dict(item.split("=", 1) for item in current["Config"]["Env"])
    expected = {"/home/node/.claude": "prisma_ai_cli_auth", "/home/node/.codex": "prisma_ai_codex_auth"}
    volumes = {}
    for mount in current["Mounts"]:
        if mount["Type"] == "tmpfs" and mount["Destination"] == "/tmp":
            continue
        if mount["Type"] != "volume" or not mount["RW"]:
            raise ValueError("Mounts da ponte devem ser volumes privados graváveis")
        volumes[mount["Destination"]] = mount["Name"]
    if volumes not in (expected, {**expected, "/home/node/.gemini": "prisma_ai_gemini_auth"}):
        raise ValueError("Volumes inesperados; atualização recusada")
    host = current["HostConfig"]
    if (set(current["NetworkSettings"]["Networks"]) != {NETWORK} or current["Config"]["User"] != "node"
            or not host["ReadonlyRootfs"] or host.get("Privileged") or any((host.get("PortBindings") or {}).values())
            or environment.get("AI_BRIDGE_TOKEN") != token):
        raise ValueError("Isolamento/configuração da ponte não correspondem à instalação")


def wait_healthy():
    for _ in range(45):
        state = json.loads(run("docker", "inspect", CONTAINER, capture=True))[0]["State"]
        if state.get("Health", {}).get("Status") == "healthy":
            return
        if state.get("Status") in {"exited", "dead"}:
            break
        time.sleep(2)
    raise RuntimeError("Ponte não ficou saudável")


def install(source, sha, upgrade=False):
    if not re.fullmatch(r"[0-9a-f]{40}", sha):
        raise ValueError("Commit inválido")
    source = Path(source).resolve()
    allowed = (ROOT / "incoming").resolve()
    if allowed not in source.parents or not (source / "Dockerfile").is_file():
        raise ValueError("Fonte deve estar na pasta de release privada")
    image = "prisma-ai-bridge:" + sha
    run("docker", "build", "--label", "org.opencontainers.image.revision=" + sha, "-t", image, str(source))
    if not exists("network", NETWORK):
        run("docker", "network", "create", "--label", "prisma.purpose=ai-bridge", NETWORK)
    network = json.loads(run("docker", "network", "inspect", NETWORK, capture=True))[0]
    if network.get("Labels", {}).get("prisma.purpose") != "ai-bridge" or any(x["Name"] not in {CONTAINER, "prisma-workspace-api"} for x in network.get("Containers", {}).values()):
        raise ValueError("Rede deve ser exclusiva da ponte e da aplicação")
    private = ROOT / "ai-bridge.env"
    if private.exists():
        values = dict(line.split("=", 1) for line in private.read_text().splitlines() if line)
        token = values["Ai__BridgeToken"]
    else:
        token = secrets.token_hex(32)
        descriptor = os.open(private, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
        with os.fdopen(descriptor, "w") as file:
            file.write("Ai__BridgeToken=" + token + "\nAi__BridgeUrl=http://prisma-ai-bridge:8080/v1\n")
    os.chmod(private, 0o600)
    previous = None
    if exists("container", CONTAINER):
        current = json.loads(run("docker", "inspect", CONTAINER, capture=True))[0]
        validate_existing(current, token)
        if current["Config"].get("Labels", {}).get("org.opencontainers.image.revision") == sha:
            wait_healthy()
            print("Ponte já instalada para esta versão.")
            return
        if not upgrade:
            raise ValueError("Use --upgrade para a atualização aprovada; contas e volumes serão preservados")
        previous = CONTAINER + "-rollback-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    for volume in ["prisma_ai_cli_auth", "prisma_ai_codex_auth", "prisma_ai_gemini_auth"]:
        run("docker", "volume", "create", volume)
    try:
        if previous:
            run("docker", "stop", "--time", "15", CONTAINER)
            run("docker", "rename", CONTAINER, previous)
            run("docker", "network", "disconnect", NETWORK, previous)
        run("docker", "run", "-d", "--name", CONTAINER, "--restart", "unless-stopped", "--network", NETWORK,
            "--network-alias", CONTAINER, "--read-only", "--tmpfs", "/tmp:rw,noexec,nosuid,size=128m", "--security-opt", "no-new-privileges:true", "--cap-drop", "ALL",
            "--env", "AI_BRIDGE_TOKEN", "--volume", "prisma_ai_cli_auth:/home/node/.claude", "--volume", "prisma_ai_codex_auth:/home/node/.codex", "--volume", "prisma_ai_gemini_auth:/home/node/.gemini", image,
            env={**os.environ, "AI_BRIDGE_TOKEN": token})
        wait_healthy()
        validate_existing(json.loads(run("docker", "inspect", CONTAINER, capture=True))[0], token)
    except Exception:
        renamed = bool(previous and exists("container", previous))
        if exists("container", CONTAINER):
            failed = json.loads(run("docker", "inspect", CONTAINER, capture=True))[0]
            if failed["Config"].get("Labels", {}).get("org.opencontainers.image.revision") == sha:
                run("docker", "rm", "-f", CONTAINER)
        if renamed:
            run("docker", "rename", previous, CONTAINER)
            old = json.loads(run("docker", "inspect", CONTAINER, capture=True))[0]
            if NETWORK not in old["NetworkSettings"]["Networks"]:
                run("docker", "network", "connect", "--alias", CONTAINER, NETWORK, CONTAINER)
        if previous:
            run("docker", "start", CONTAINER)
            wait_healthy()
        raise
    print("Ponte instalada e saudável. Contas e volumes anteriores preservados; login Google depende do administrador.")
    if previous:
        print("Rollback da ponte: " + previous)


if __name__ == "__main__":
    ROOT.mkdir(mode=0o700, exist_ok=True)
    with (ROOT / "deploy.lock").open("w") as lock:
        fcntl.flock(lock, fcntl.LOCK_EX)
        install(sys.argv[1], sys.argv[2], upgrade=len(sys.argv) == 4 and sys.argv[3] == "--upgrade")
