---
id: ADR-0004
titulo: "Subtarefa na entidade WorkItem via ParentId"
status: aceita
data: 2026-06-30
origem: "DECISIONS.md D4"
substitui: ""
---

# ADR-0004 · Subtarefa na entidade WorkItem via ParentId

## Decisão

- **D4** — Subtarefa = a própria entidade `WorkItem` com `ParentId` (auto-referência),
  e não uma tabela separada. Assim subtarefa herda alocação, anexo e horas de graça. (2026-06-30)
