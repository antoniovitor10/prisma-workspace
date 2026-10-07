"""Ponte experimental: CLI isolada, sem persistir conversa ou acessar o banco."""
from __future__ import annotations

import hmac
import json
import os
import select
import signal
import socket
import subprocess
import tempfile
import time
import auth
from pathlib import Path
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer


def command(adapter: str, model: str) -> list[str]:
    if not model or len(model) > 200 or any(ord(c) < 32 for c in model):
        raise ValueError("Modelo invalido")
    if adapter == "claude":
        return ["claude", "-p", "--restricted", "--tools", "", "--disallowedTools", "mcp__*",
                "--strict-mcp-config", "--mcp-config", '{"mcpServers":{}}',
                "--no-session-persistence", "--output-format", "text", "--model", model]
    if adapter == "codex":
        argv = ["codex", "exec", "--ephemeral", "--ignore-user-config", "--skip-git-repo-check",
                "--sandbox", "read-only", "--color", "never", "--model", model]
        for feature in ["shell_tool", "unified_exec", "shell_snapshot", "multi_agent", "apps", "hooks", "plugins", "remote_plugin", "memories"]:
            argv.extend(["-c", f"features.{feature}=false"])
        argv.extend(["-c", 'web_search="disabled"', "-"])
        return argv
    if adapter == "gemini":
        return ["gemini", "--prompt", "", "--output-format", "json", "--model", model,
                "--extensions", "none", "--policy", "/bridge/gemini-deny-tools.toml"]
    raise ValueError("Adaptador nao configurado: use claude, codex ou gemini")


def gemini_chat_environment(directory):
    env = auth.environment(directory)
    env["GEMINI_CLI_HOME"] = directory
    env["GEMINI_FORCE_AUTH_TYPE"] = "oauth-personal"
    temporary = Path(directory) / ".gemini"
    temporary.mkdir(mode=0o700)
    source = Path(auth.gemini_home()) / ".gemini" / "oauth_creds.json"
    # A CLI verifica os tokens com o Google; arquivo existente não autentica.
    with auth.gemini_lock:
        if not source.is_file() or source.stat().st_size > 32000:
            raise ValueError("Autentique Gemini antes de testar")
        cache = temporary / "oauth_creds.json"
        cache.write_bytes(source.read_bytes())
        cache.chmod(0o600)
    settings = {
        "security": {"auth": {"selectedType": "oauth-personal"}},
        "tools": {"core": [], "useRipgrep": False}, "mcpServers": {}, "hooks": {},
        "hooksConfig": {"enabled": False}, "experimental": {"enableAgents": False},
        "telemetry": {"enabled": False, "logPrompts": False},
        "context": {"fileName": []}, "model": {"maxSessionTurns": 1},
        "general": {"enableAutoUpdate": False, "enablePromptCompletion": False, "sessionRetention": {"enabled": False}}
    }
    file = temporary / "settings.json"
    file.write_text(json.dumps(settings), encoding="utf-8")
    file.chmod(0o600)
    return env


def gemini_result(output, prompt=""):
    data = json.loads(output)
    text = data.get("response")
    if data.get("error") or not isinstance(text, str) or not text.strip() or len(text) > 64000:
        raise ValueError("Resposta Gemini inválida")
    usage = {"prompt_tokens": 0, "completion_tokens": 0}
    for model in data.get("stats", {}).get("models", {}).values():
        tokens = model.get("tokens", {})
        for target, key, fallback in [("prompt_tokens", "input", "prompt"), ("completion_tokens", "candidates", "output")]:
            value = tokens.get(key, tokens.get(fallback, 0))
            if type(value) is int and 0 <= value <= 10_000_000:
                usage[target] += value
    # A reserva da fundação estima entrada por caracteres/3. Sem estatísticas,
    # conserva essa estimativa e aplica o mesmo critério à saída, sem declarar uso gratuito.
    if usage["prompt_tokens"] == 0 and prompt:
        usage["prompt_tokens"] = (len(prompt) + 2) // 3
    if usage["completion_tokens"] == 0:
        usage["completion_tokens"] = (len(text.strip()) + 2) // 3
    return text.strip(), usage


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *_):
        # Prompts, credenciais e respostas nunca entram no log da ponte.
        pass

    def do_GET(self):
        if self.path != "/health":
            self.send_error(404)
            return
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.end_headers()
        self.wfile.write(b'{"status":"healthy"}')

    def do_POST(self):
        expected = os.environ.get("AI_BRIDGE_TOKEN", "")
        supplied = self.headers.get("Authorization", "")
        if not expected or not hmac.compare_digest(supplied, "Bearer " + expected):
            self.send_error(401, "Ponte nao autorizada")
            return
        if self.path != "/v1/chat/completions" and not self.path.startswith("/v1/cli/"):
            self.send_error(404)
            return
        try:
            length = int(self.headers.get("Content-Length", "0"))
            if not 0 < length <= 256_000:
                raise ValueError("Pedido excedeu limite")
            body = json.loads(self.rfile.read(length))
            if self.path.startswith("/v1/cli/"):
                parts = self.path.split("/")
                if len(parts) != 5 or parts[3] not in {"claude", "codex", "gemini"} or parts[4] not in {"status", "models", "login", "progress", "complete", "cancel"}:
                    raise FileNotFoundError()
                adapter, operation = parts[3], parts[4]
                owner = body.get("owner")
                if operation in {"status", "models"}:
                    data = auth.status(adapter) if operation == "status" else auth.models(adapter)
                else:
                    if not isinstance(owner, str) or not 0 < len(owner) < 200:
                        raise ValueError("Dono invalido")
                    if operation == "login":
                        if body.get("acceptedRisk") is not True:
                            raise ValueError("Aceite obrigatorio")
                        data = auth.sessions.start(adapter, owner)
                    else:
                        row = auth.sessions.get(adapter, owner, body.get("sessionId"))
                        if operation == "cancel":
                            row.terminate()
                        if operation == "complete":
                            row.complete(body.get("code"))
                        data = row.view()
                self.send_response(200)
                self.send_header("Content-Type", "application/json")
                self.send_header("Cache-Control", "no-store")
                self.end_headers()
                self.wfile.write(json.dumps(data).encode())
                return
            if body.get("tools"):
                raise ValueError("Ferramentas nao sao aceitas pela ponte")
            prompt = "\n\n".join(str(m["role"]) + ": " + str(m["content"]) for m in body["messages"])
            adapter = body.get("adapter", os.environ.get("AI_BRIDGE_ADAPTER", "claude"))
            argv = command(adapter, body["model"])
            with tempfile.TemporaryDirectory(prefix="prisma-ai-") as directory:
                with tempfile.TemporaryFile() as output, tempfile.TemporaryFile() as errors:
                    env = gemini_chat_environment(directory) if adapter == "gemini" else auth.environment(directory)
                    env["CLAUDE_CODE_SKIP_PROMPT_HISTORY"] = "1"
                    process = subprocess.Popen(argv, stdin=subprocess.PIPE, stdout=output, stderr=errors,
                                               cwd=directory, env=env, start_new_session=True)
                    try:
                        process.stdin.write(prompt.encode("utf-8"))
                        process.stdin.close()
                        deadline = time.monotonic() + 110
                        while process.poll() is None:
                            if time.monotonic() > deadline:
                                raise TimeoutError()
                            readable, _, _ = select.select([self.connection], [], [], .1)
                            if readable and self.connection.recv(1, socket.MSG_PEEK) == b"":
                                raise ConnectionAbortedError()
                        if process.returncode:
                            raise RuntimeError("A CLI recusou a chamada; confira login e assinatura na ponte")
                        output.seek(0)
                        raw = output.read(256_001 if adapter == "gemini" else 64_001).decode("utf-8", errors="replace").strip()
                        if adapter == "gemini" and len(raw) > 256000:
                            raise RuntimeError("Resposta da CLI excedeu limite")
                        text, usage = gemini_result(raw, prompt) if adapter == "gemini" else (raw, {"prompt_tokens": 0, "completion_tokens": 0})
                        if not text or len(text) > 64000:
                            raise RuntimeError("Resposta da CLI invalida")
                    finally:
                        if process.poll() is None:
                            os.killpg(process.pid, signal.SIGTERM)
                            try:
                                process.wait(timeout=2)
                            except subprocess.TimeoutExpired:
                                os.killpg(process.pid, signal.SIGKILL)
                                process.wait()
            self.send_response(200)
            self.send_header("Content-Type", "text/event-stream")
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            data = {"choices": [{"delta": {"content": text}}], "usage": usage}
            self.wfile.write(("data: " + json.dumps(data) + "\n\ndata: [DONE]\n\n").encode("utf-8"))
            self.wfile.flush()
        except (BrokenPipeError, ConnectionAbortedError):
            return
        except (FileNotFoundError, FileExistsError) as error:
            self.send_response(404 if isinstance(error, FileNotFoundError) else 409)
            self.send_header("Content-Type", "application/json")
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(b'{"error":{"message":"Sessao indisponivel ou login ja em andamento."}}')
        except Exception:
            self.send_response(502)
            self.send_header("Content-Type", "application/json")
            self.end_headers()
            self.wfile.write(b'{"error":{"message":"Ponte CLI indisponivel; confira login, assinatura e adaptador."}}')


if __name__ == "__main__":
    if not os.environ.get("AI_BRIDGE_TOKEN"):
        raise SystemExit("AI_BRIDGE_TOKEN precisa estar configurado")
    ThreadingHTTPServer(("0.0.0.0", 8080), Handler).serve_forever()
