---
id: ADR-0032
titulo: "Notificações internas persistentes com fila durável e deduplicação"
status: aceita
data: 2026-07-20
origem: "DECISIONS.md D32"
substitui: ""
---

# ADR-0032 · Notificações internas persistentes com fila durável e deduplicação

## Decisão

- **D32** — Notificações internas são registros persistentes por organização e destinatário,
  separados dos alertas derivados de “Meu Trabalho”. Preferências por tipo controlam os canais
  in-app e e-mail; o registro também funciona como fila durável de entrega, com chave de
  deduplicação para lembretes de prazo/SLA e processamento assíncrono sem bloquear o caso de uso.
  Mensagens transacionais do Portal Externo continuam seguindo a fronteira definida em D28.
  (2026-07-20)
