---
id: ADR-0054
titulo: "Product Backlog filtrado por sprint e desvinculação ao excluir sprint"
status: aceita
data: 2026-08-24
origem: "DECISIONS.md D54"
substitui: ""
---

# ADR-0054 · Product Backlog filtrado por sprint e desvinculação ao excluir sprint

## Decisão

- **D54** — Product Backlog lista por padrão tarefas sem sprint e oferece filtro para incluir tarefas já
  planejadas em sprint. Kanban e sprint são recortes do mesmo `WorkItem`; a tarefa pode permanecer no Kanban
  sem `SprintId`. Excluir uma sprint remove apenas o vínculo `SprintId`, preserva quadro, coluna, conteúdo e
  histórico da tarefa e faz os itens voltarem ao Product Backlog padrão. (2026-08-24)
