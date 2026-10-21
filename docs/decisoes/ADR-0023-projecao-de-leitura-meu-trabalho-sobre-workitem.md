---
id: ADR-0023
titulo: "Projeção de leitura Meu Trabalho sobre WorkItem"
status: aceita
data: 2026-07-16
origem: "DECISIONS.md D23"
substitui: ""
---

# ADR-0023 · Projeção de leitura Meu Trabalho sobre WorkItem

## Decisão

- **D23** — “Meu trabalho” será uma projeção de leitura calculada a partir do mesmo `WorkItem`, sem
  duplicar tarefas em uma fila própria. A projeção combina atribuição, autoria, acompanhamento,
  prazos, bloqueios, solicitações externas, aprovações e comentários; menções usam o token `@e-mail`.
  Alertas importantes são derivados desses dados até o módulo persistente de notificações da Fase 7.
  (2026-07-16)
