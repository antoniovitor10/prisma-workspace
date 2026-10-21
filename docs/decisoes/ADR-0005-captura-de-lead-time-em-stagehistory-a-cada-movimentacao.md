---
id: ADR-0005
titulo: "Captura de lead time em StageHistory a cada movimentação"
status: aceita
data: 2026-06-30
origem: "DECISIONS.md D5"
substitui: ""
---

# ADR-0005 · Captura de lead time em StageHistory a cada movimentação

## Decisão

- **D5** — Lead time é capturado **desde a Fase 2** gravando uma linha em `StageHistory`
  a cada movimentação de card — mesmo antes do dashboard existir. Os dados acumulam desde o dia 1. (2026-06-30)
