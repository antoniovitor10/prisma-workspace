---
id: ADR-0024
titulo: "Atualizações em tempo real do quadro via SignalR"
status: aceita
data: 2026-07-16
origem: "DECISIONS.md D24"
substitui: ""
---

# ADR-0024 · Atualizações em tempo real do quadro via SignalR

## Decisão

- **D24** — Atualizações em tempo real do quadro usam SignalR com grupos por organização/quadro.
  O evento transporta apenas o identificador do quadro; clientes autorizados invalidam o cache e
  recarregam a projeção, mantendo regras e dados sensíveis na API. Preferências de conteúdo dos
  cartões são persistidas no próprio quadro como JSON versionável. (2026-07-16)
