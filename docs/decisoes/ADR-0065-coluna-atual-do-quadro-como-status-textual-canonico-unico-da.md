---
id: ADR-0065
titulo: "Coluna atual do quadro como status textual canônico único da tarefa"
status: aceita
data: 2026-08-24
origem: "DECISIONS.md D65"
substitui: "ADR-0022, ADR-0045"
---

# ADR-0065 · Coluna atual do quadro como status textual canônico único da tarefa

## Decisão

- **D65** — A tarefa não possui status funcional separado do quadro. Seu status textual canônico é exatamente o
  nome da coluna atual; a classificação interna da coluna determina apenas se a tarefa está aberta ou concluída
  e não aparece como etiqueta principal genérica. Escolher status no detalhe/dropdown significa mover a tarefa
  para a coluna escolhida, e arrastar no Kanban atualiza a mesma informação em todas as demais telas. Não pode
  haver divergência entre coluna e status exibido. Reclassificar uma coluna atualiza aberta/concluída de todas as
  tarefas conforme a operação bidirecional e atômica da D62, sem criar status adicional. `WorkflowStatus`,
  templates, transições e campos relacionados permanecem somente como legado técnico até migração segura e não
  governam nomes, exibição ou movimento. A D65 sucede D22/D45 e o alvo anterior da
  `SPEC-WORKFLOW-STATUS` no que definia status independente por projeto. (2026-08-24)
