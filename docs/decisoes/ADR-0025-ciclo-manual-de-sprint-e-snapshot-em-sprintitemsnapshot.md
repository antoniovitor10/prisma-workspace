---
id: ADR-0025
titulo: "Ciclo manual de sprint e snapshot em SprintItemSnapshot"
status: substituida # substituida por ADR-0081, ADR-0084
data: 2026-07-20
origem: "DECISIONS.md D25"
substitui: ""
substituida_por: "ADR-0081, ADR-0084"
---

# ADR-0025 · Ciclo manual de sprint e snapshot em SprintItemSnapshot

## Decisão

- **D25** — O ciclo de sprint passa a ser Planejada → Ativa → Concluída, com cancelamento a partir
  de Planejada ou Ativa, e somente uma sprint ativa por projeto. Ao concluir ou cancelar, o escopo
  é fotografado em `SprintItemSnapshot`; itens pendentes podem voltar ao Product Backlog ou seguir
  para outra sprint sem apagar métricas e resultados históricos. (2026-07-20)
