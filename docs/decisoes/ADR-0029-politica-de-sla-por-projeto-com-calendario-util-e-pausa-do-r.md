---
id: ADR-0029
titulo: "Política de SLA por projeto com calendário útil e pausa do relógio"
status: aceita
data: 2026-07-20
origem: "DECISIONS.md D29"
substitui: ""
---

# ADR-0029 · Política de SLA por projeto com calendário útil e pausa do relógio

## Decisão

- **D29** — O SLA pertence ao projeto e é aplicado às solicitações externas por uma política com
  calendário de atendimento, feriados e regras ordenadas por categoria/prioridade. Ao abrir a
  solicitação, os parâmetros e vencimentos são fotografados no `ExternalRequest`; alterações
  posteriores da política não reescrevem o histórico. Pedido de informação pode pausar o relógio,
  resposta do solicitante o retoma e apenas minutos úteis deslocam os vencimentos. (2026-07-20)
