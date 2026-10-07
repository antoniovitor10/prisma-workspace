FROM node:22-bookworm-slim
RUN apt-get update && apt-get install -y --no-install-recommends python3 ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && npm install -g @openai/codex@0.160.1 @anthropic-ai/claude-code@2.1.291 @google/gemini-cli@0.63.0 @google/gemini-cli-core@0.63.0
WORKDIR /bridge
RUN install -d -o node -g node -m 700 /home/node/.claude /home/node/.claude/state /home/node/.codex /home/node/.gemini
COPY server.py auth.py gemini-auth.mjs gemini-deny-tools.toml /bridge/
ENV CLAUDE_CONFIG_DIR=/home/node/.claude/state CODEX_HOME=/home/node/.codex
USER node
EXPOSE 8080
HEALTHCHECK --interval=20s --timeout=5s --start-period=5s --retries=3 CMD python3 -c "import urllib.request; urllib.request.urlopen('http://localhost:8080/health', timeout=3).read()"
CMD ["python3", "/bridge/server.py"]
