---
id: ADR-0047
titulo: "Preservação de autoria em auditoria de tarefas por snapshot"
status: aceita
data: 2026-08-20
origem: "DECISIONS.md D47"
substitui: ""
---

# ADR-0047 · Preservação de autoria em auditoria de tarefas por snapshot

## Decisão

- **D47** — A auditoria de tarefas preserva autoria por snapshot: eventos e movimentações registram o
  `ActorId` quando disponível e o `ActorName` observado no momento da ação, sem FK obrigatória que permita
  apagar autoria ou impedir o ciclo de vida do usuário. Comentários continuam separados do histórico
  automático. (2026-08-20)
