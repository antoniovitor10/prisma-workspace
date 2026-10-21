---
id: ADR-0021
titulo: "WorkItem como agregado único para demandas internas e externas"
status: aceita
data: 2026-07-15
origem: "DECISIONS.md D21"
substitui: ""
---

# ADR-0021 · WorkItem como agregado único para demandas internas e externas

## Decisão

- **D21** — `WorkItem` permanece o agregado único para demandas internas e externas. Cada item
  recebe número estável gerado por sequence do SQL Server e pode registrar origem, solicitante,
  responsável principal, participantes, critérios de aceite, seguidores, links tipados
  (dependência/bloqueio/relação) e valores de campos personalizados. ~~Exclusão funcional é
  arquivamento lógico para preservar auditoria e apontamentos.~~ Após D53, excluir e arquivar são ações
  distintas. (2026-07-15)
