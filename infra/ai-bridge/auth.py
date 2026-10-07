"""Autenticação oficial das CLIs; instruções temporárias ficam somente em memória."""
from __future__ import annotations

import json
import os
import re
import select
import shutil
import signal
import subprocess
import tempfile
import threading
import time
import uuid
from contextlib import nullcontext
from urllib.parse import urlsplit

gemini_lock = threading.RLock()


def gemini_home():
    return os.environ.get("GEMINI_AUTH_HOME", "/home/node")


def gemini_reply(output):
    for line in output.decode(errors="replace").splitlines()[::-1]:
        if line.startswith("PRISMA_GEMINI_AUTH="):
            return json.loads(line.split("=", 1)[1])
    raise ValueError("Resposta Gemini inválida")


def persist_gemini_login(directory):
    # Somente o cache produzido e confirmado pelo módulo oficial é promovido.
    source = os.path.join(directory, ".gemini", "oauth_creds.json")
    destination = os.path.join(gemini_home(), ".gemini")
    if not os.path.isfile(source) or os.path.getsize(source) > 32000:
        raise ValueError("Cache Gemini inválido")
    os.makedirs(destination, mode=0o700, exist_ok=True)
    temporary = os.path.join(destination, ".oauth-" + uuid.uuid4().hex)
    try:
        with open(source, "rb") as original, open(temporary, "xb") as target:
            os.chmod(temporary, 0o600)
            target.write(original.read(32001))
        os.replace(temporary, os.path.join(destination, "oauth_creds.json"))
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def environment(directory):
    allowed = {"PATH", "HOME", "SSL_CERT_FILE", "NODE_EXTRA_CA_CERTS", "HTTPS_PROXY", "HTTP_PROXY", "NO_PROXY"}
    env = {k: v for k, v in os.environ.items() if k in allowed}
    env.update(BROWSER="true", NO_COLOR="1", TERM="dumb", CLAUDE_CODE_SKIP_PROMPT_HISTORY="1",
               CLAUDE_CONFIG_DIR=os.environ.get("CLAUDE_CONFIG_DIR", "/home/node/.claude/state"),
               CODEX_HOME=os.environ.get("CODEX_HOME", "/home/node/.codex"),
               GEMINI_CLI_HOME=gemini_home(), NO_BROWSER="true")
    return env


def stop(process):
    if process.poll() is None:
        os.killpg(process.pid, signal.SIGTERM)
        try:
            process.wait(timeout=2)
        except subprocess.TimeoutExpired:
            os.killpg(process.pid, signal.SIGKILL)
            process.wait()


def status(adapter):
    if adapter not in {"codex", "claude", "gemini"}:
        raise ValueError("Adaptador invalido")
    if not shutil.which(adapter):
        return {"available": False, "authenticated": False, "state": "unavailable", "message": "Esta CLI não está instalada na ponte."}
    with tempfile.TemporaryDirectory(prefix="prisma-cli-status-") as directory:
        argv = ["node", "/bridge/gemini-auth.mjs", "status"] if adapter == "gemini" else ["codex", "login", "status", "-c", 'log_dir="' + directory + '"'] if adapter == "codex" else ["claude", "auth", "status", "--json"]
        try:
            with gemini_lock if adapter == "gemini" else nullcontext():
                result = subprocess.run(argv, capture_output=True, timeout=12, cwd=directory, env=environment(directory))
            if len(result.stdout) + len(result.stderr) > 32000:
                raise ValueError("Saida excedeu limite")
            authenticated = result.returncode == 0
            if adapter == "claude" and authenticated:
                data = json.loads(result.stdout)
                authenticated = data.get("loggedIn", False) and data.get("authMethod") == "claude.ai"
            if adapter == "codex" and authenticated:
                authenticated = "chatgpt" in (result.stdout + result.stderr).decode(errors="replace").lower()
            if adapter == "gemini" and authenticated:
                authenticated = gemini_reply(result.stdout).get("authenticated") is True
            return {"available": True, "authenticated": bool(authenticated), "state": "authenticated" if authenticated else "authenticationRequired",
                    "message": "Conta autenticada; escolha o modelo e teste a conexão." if authenticated else "Entre na conta do provedor para usar a assinatura."}
        except (OSError, ValueError, subprocess.TimeoutExpired):
            return {"available": True, "authenticated": False, "state": "unavailable", "message": "Não foi possível verificar o login. Confira a ponte e tente novamente."}


def instructions(adapter, output):
    text = re.sub(r"\x1b\[[0-?]*[ -/]*[@-~]", "", output)
    hosts = {"auth.openai.com"} if adapter == "codex" else {"accounts.google.com"} if adapter == "gemini" else {"claude.com", "claude.ai", "console.anthropic.com", "platform.claude.com"}
    url = None
    for candidate in re.findall(r"https://[^\s<>]+", text):
        parsed = urlsplit(candidate)
        if parsed.hostname in hosts and not parsed.username and not parsed.password and parsed.port in (None, 443):
            url = candidate
            break
    code = None
    if adapter == "codex":
        match = re.search(r"\b[A-Z0-9]{4,8}-[A-Z0-9]{4,8}\b", text)
        code = match.group() if match else None
    return url, code


class Session:
    def __init__(self, adapter, owner):
        self.id = uuid.uuid4().hex
        self.adapter, self.owner = adapter, owner
        self.expires = time.time() + (300 if adapter == "gemini" else 600)
        self.state, self.url, self.device_code = "starting", None, None
        self.message = "Preparando o login oficial do provedor."
        self.process = None
        self.lock = threading.RLock()
        self.complete_sent = False

    def view(self):
        with self.lock:
            return {"sessionId": self.id, "state": self.state, "url": self.url, "deviceCode": self.device_code,
                    "expiresAt": self.expires, "requiresCode": self.adapter in {"claude", "gemini"} and self.state == "waiting",
                    "authenticated": self.state == "authenticated", "available": True, "message": self.message}

    def terminate(self, state="cancelled"):
        with self.lock:
            if self.state not in {"starting", "waiting", "completing"}:
                return
            self.state, self.url, self.device_code = state, None, None
            self.message = "Login expirado. Inicie novamente." if state == "expired" else "Tentativa cancelada. Você pode iniciar novamente."
            if self.process:
                stop(self.process)

    def complete(self, code):
        with self.lock:
            if self.adapter not in {"claude", "gemini"} or self.state != "waiting" or self.complete_sent or not self.process or self.process.poll() is not None:
                raise ValueError("A CLI nao aguarda codigo")
            if not isinstance(code, str) or not 0 < len(code) <= 4096 or any(ord(c) < 32 for c in code):
                raise ValueError("Codigo invalido")
            self.process.stdin.write((code + "\n").encode())
            self.process.stdin.flush()
            self.complete_sent = True
            self.state = "completing"
            self.message = "Confirmando a autenticação com o provedor."

    def run(self):
        try:
            with tempfile.TemporaryDirectory(prefix="prisma-cli-login-") as directory:
                argv = ["node", "/bridge/gemini-auth.mjs", "login"] if self.adapter == "gemini" else ["codex", "login", "--device-auth", "-c", 'log_dir="' + directory + '"'] if self.adapter == "codex" else ["claude", "auth", "login", "--claudeai"]
                login_env = environment(directory)
                if self.adapter == "gemini":
                    login_env["GEMINI_CLI_HOME"] = directory
                with self.lock:
                    if self.state != "starting":
                        return
                    self.process = subprocess.Popen(argv, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                                    cwd=directory, env=login_env, start_new_session=True)
                output = b""
                try:
                    while self.process.poll() is None:
                        if time.time() >= self.expires:
                            self.terminate("expired")
                            return
                        if select.select([self.process.stdout], [], [], .1)[0]:
                            output += os.read(self.process.stdout.fileno(), 4096)
                            if len(output) > 32000:
                                raise ValueError("Saida excedeu limite")
                            url, code = instructions(self.adapter, output.decode(errors="replace"))
                            with self.lock:
                                if self.state not in {"starting", "waiting"}:
                                    continue
                                self.url, self.device_code = url, code
                                if url and (code or self.adapter in {"claude", "gemini"}):
                                    self.state = "waiting"
                                    self.message = "Abra o provedor e informe o código temporário." if self.adapter == "codex" else "Abra o provedor, autorize o login e cole aqui o código retornado, se solicitado."
                    with self.lock:
                        if self.state in {"cancelled", "expired"}:
                            return
                    if time.time() >= self.expires:
                        self.terminate("expired")
                        return
                    if self.adapter == "gemini":
                        output += self.process.stdout.read(32001)
                        if len(output) > 32000:
                            raise ValueError("Saída excedeu limite")
                        authenticated = self.process.returncode == 0 and gemini_reply(output).get("authenticated") is True
                    else:
                        authenticated = self.process.returncode == 0 and status(self.adapter)["authenticated"]
                    with gemini_lock if self.adapter == "gemini" else nullcontext(), self.lock:
                        if self.state in {"cancelled", "expired"}:
                            return
                        if self.adapter == "gemini" and authenticated:
                            persist_gemini_login(directory)
                        self.state = "authenticated" if authenticated else "failed"
                        self.url, self.device_code = None, None
                        self.message = "Login confirmado. Escolha o modelo e teste a conexão." if authenticated else ("O login não foi concluído. Inicie novamente; no ChatGPT, confira se o login por dispositivo está habilitado." if self.adapter == "codex" else "O login não foi concluído. Inicie novamente com uma conta aceita pelo provedor.")
                finally:
                    stop(self.process)
                    self.process.stdin.close()
                    self.process.stdout.close()
        except Exception:
            with self.lock:
                if self.state not in {"cancelled", "expired"}:
                    self.state, self.url, self.device_code = "failed", None, None
                    self.message = "Não foi possível iniciar ou concluir o login. Confira a instalação e tente novamente."


class Sessions:
    def __init__(self):
        self.rows = {}
        self.lock = threading.RLock()

    def start(self, adapter, owner):
        with self.lock:
            self.cleanup()
            if any(x.adapter == adapter and x.state in {"starting", "waiting", "completing"} for x in self.rows.values()):
                raise FileExistsError("Login em andamento")
            if len(self.rows) >= 32:
                raise FileExistsError("Limite de sessoes")
            row = Session(adapter, owner)
            self.rows[row.id] = row
            threading.Thread(target=row.run, daemon=True).start()
            return row.view()

    def get(self, adapter, owner, session_id):
        with self.lock:
            self.cleanup()
            row = self.rows.get(session_id)
            if row is None or row.owner != owner or row.adapter != adapter:
                raise FileNotFoundError("Sessao nao encontrada")
            return row

    def cleanup(self):
        for key, row in list(self.rows.items()):
            if time.time() >= row.expires:
                row.terminate("expired")
            if time.time() >= row.expires + 60:
                del self.rows[key]


sessions = Sessions()


def models(adapter):
    auth_status = status(adapter)
    if not auth_status["authenticated"]:
        return {**auth_status, "models": [], "source": adapter}
    if adapter == "claude":
        return {"models": [{"id": "sonnet", "name": "Sonnet (alias da CLI)"}, {"id": "opus", "name": "Opus (alias da CLI)"}, {"id": "haiku", "name": "Haiku (alias da CLI)"}],
                "state": "ready", "source": "claude", "message": "Aliases oficiais da CLI. O teste verifica o acesso da assinatura."}
    if adapter == "gemini":
        with tempfile.TemporaryDirectory(prefix="prisma-gemini-models-") as directory:
            result = subprocess.run(["node", "/bridge/gemini-auth.mjs", "models"], capture_output=True, timeout=12, cwd=directory, env=environment(directory))
            if result.returncode or len(result.stdout) > 64000:
                raise ValueError("Catálogo Gemini indisponível")
            return gemini_reply(result.stdout)
    with tempfile.TemporaryDirectory(prefix="prisma-codex-models-") as directory:
        process = subprocess.Popen(["codex", "app-server", "--listen", "stdio://", "-c", 'log_dir="' + directory + '"'],
                                   stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, cwd=directory,
                                   env=environment(directory), start_new_session=True)
        try:
            def send(value):
                process.stdin.write((json.dumps(value) + "\n").encode())
                process.stdin.flush()
            send({"id": 1, "method": "initialize", "params": {"clientInfo": {"name": "prisma", "version": "1.0.0"}}})
            deadline = time.monotonic() + 12
            output = b""
            initialized = False
            while time.monotonic() < deadline:
                if select.select([process.stdout], [], [], .1)[0]:
                    part = os.read(process.stdout.fileno(), 8192)
                    if not part:
                        break
                    output += part
                    if len(output) > 256000:
                        raise ValueError("Catalogo excedeu limite")
                    while b"\n" in output:
                        line, output = output.split(b"\n", 1)
                        value = json.loads(line)
                        if value.get("id") == 1 and not initialized:
                            if value.get("error"):
                                raise ValueError("Inicializacao recusada")
                            initialized = True
                            send({"method": "initialized", "params": {}})
                            send({"id": 2, "method": "model/list", "params": {"includeHidden": False, "limit": 100}})
                        if value.get("id") == 2:
                            if value.get("error"):
                                raise ValueError("Catalogo recusado")
                            rows = value.get("result", {}).get("data", [])
                            return {"models": [{"id": r.get("model", r["id"]), "name": r.get("displayName", r["id"])} for r in rows if not r.get("hidden")],
                                    "state": "ready", "source": "codex", "message": "Catálogo da CLI instalada. O teste verifica o acesso da assinatura."}
            raise TimeoutError("Catalogo indisponivel")
        finally:
            stop(process)
            process.stdin.close()
            process.stdout.close()
