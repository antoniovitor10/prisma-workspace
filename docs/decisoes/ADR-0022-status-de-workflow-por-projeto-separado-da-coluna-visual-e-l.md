---
id: ADR-0022
titulo: "Status de workflow por projeto separado da coluna visual e limite de WIP"
status: substituida # substituida por ADR-0065, ADR-0083
data: 2026-07-16
origem: "DECISIONS.md D22"
substitui: ""
substituida_por: "ADR-0065, ADR-0083"
---

# ADR-0022 · Status de workflow por projeto separado da coluna visual e limite de WIP

## Decisão

- **D22** — Status de workflow pertence ao projeto e é separado da coluna visual. Cada `Stage`
  referencia um `WorkflowStatus` e cada `WorkItem` persiste o status atual; mover um cartão sincroniza
  ambos. Transições são direcionais e obrigatórias, e o limite de WIP da coluna é validado no backend.
  A migration cria status equivalentes às colunas existentes e libera as transições já possíveis para
  manter compatibilidade. (2026-07-16)
