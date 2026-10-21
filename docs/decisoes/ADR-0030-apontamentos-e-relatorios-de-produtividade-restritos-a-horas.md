---
id: ADR-0030
titulo: "Apontamentos e relatórios de produtividade restritos a horas e capacidade"
status: aceita
data: 2026-07-20
origem: "DECISIONS.md D30"
substitui: ""
---

# ADR-0030 · Apontamentos e relatórios de produtividade restritos a horas e capacidade

## Decisão

- **D30** — Apontamentos e relatórios de produtividade tratam somente duração, horas previstas,
  horas realizadas, capacidade e seus desvios. Valor-hora, salário, custo, faturamento e
  rentabilidade não fazem parte do modelo, dos DTOs nem das telas. Relatórios são projeções do
  mesmo `TimeEntry` e `WorkItem`, agrupadas por usuário, equipe e projeto. (2026-07-20)
