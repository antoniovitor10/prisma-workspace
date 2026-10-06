FROM node:22-bookworm-slim
RUN apt-get update && apt-get install -y --no-install-recommends python3 ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && npm install -g @openai/codex@0.160.1 @anthropic-ai/claude-code@2.1.291
WORKDIR /bridge
COPY server.py /bridge/server.py
USER node
EXPOSE 8080
CMD ["python3", "/bridge/server.py"]
