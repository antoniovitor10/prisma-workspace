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
    raise ValueError("Adaptador nao configurado: use claude ou codex")


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *_):
        # Prompts, credenciais e respostas nunca entram no log da ponte.
        pass

    def do_POST(self):
        expected = os.environ.get("AI_BRIDGE_TOKEN", "")
        supplied = self.headers.get("Authorization", "")
        if not expected or not hmac.compare_digest(supplied, "Bearer " + expected):
            self.send_error(401, "Ponte nao autorizada")
            return
        if self.path != "/v1/chat/completions":
            self.send_error(404)
            return
        try:
            length = int(self.headers.get("Content-Length", "0"))
            if not 0 < length <= 256_000:
                raise ValueError("Pedido excedeu limite")
            body = json.loads(self.rfile.read(length))
            if body.get("tools"):
                raise ValueError("Ferramentas nao sao aceitas pela ponte")
            prompt = "\n\n".join(str(m["role"]) + ": " + str(m["content"]) for m in body["messages"])
            argv = command(os.environ.get("AI_BRIDGE_ADAPTER", "claude"), body["model"])
            with tempfile.TemporaryDirectory(prefix="prisma-ai-") as directory:
                with tempfile.TemporaryFile() as output, tempfile.TemporaryFile() as errors:
                    env = {k: v for k, v in os.environ.items() if k not in {"AI_BRIDGE_TOKEN", "DATABASE_URL", "CONNECTION_STRING"}}
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
                        text = output.read(64_001).decode("utf-8", errors="replace").strip()
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
            data = {"choices": [{"delta": {"content": text}}], "usage": {"prompt_tokens": 0, "completion_tokens": 0}}
            self.wfile.write(("data: " + json.dumps(data) + "\n\ndata: [DONE]\n\n").encode("utf-8"))
            self.wfile.flush()
        except (BrokenPipeError, ConnectionAbortedError):
            return
        except Exception:
            self.send_response(502)
            self.send_header("Content-Type", "application/json")
            self.end_headers()
            self.wfile.write(b'{"error":{"message":"Ponte CLI indisponivel; confira login, assinatura e adaptador."}}')


if __name__ == "__main__":
    if not os.environ.get("AI_BRIDGE_TOKEN"):
        raise SystemExit("AI_BRIDGE_TOKEN precisa estar configurado")
    ThreadingHTTPServer(("0.0.0.0", 8080), Handler).serve_forever()
