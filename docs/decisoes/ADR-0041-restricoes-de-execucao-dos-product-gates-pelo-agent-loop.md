---
id: ADR-0041
titulo: "Restrições de execução dos product gates pelo Agent Loop"
status: aceita
data: 2026-08-20
origem: "DECISIONS.md D41"
substitui: ""
---

# ADR-0041 · Restrições de execução dos product gates pelo Agent Loop

## Decisão

- **D41** — O Agent Loop pode executar automaticamente somente os product gates ativos e versionados em
  `profiles/prisma-loop.yaml`. A execução ocorre apenas no estado `gating`, respeita a ordem declarada,
  interrompe na primeira falha quando configurado, registra resultado/duração/código de saída no envelope
  e na telemetria e nunca persiste stdout/stderr. Não existe entrada para executar comandos arbitrários,
  autoaprovar Human Gates, chamar agentes/LLMs, aplicar migrations, criar PR, fazer merge ou deploy.
  Caminhos de trabalho devem permanecer dentro do repositório. (2026-08-20)
