---
id: ADR-0050
titulo: "Quadro como projeção operacional multi-quadro N para N do projeto"
status: substituida # substituida por ADR-0052
data: 2026-08-20
origem: "DECISIONS.md D50"
substitui: ""
substituida_por: "ADR-0052"
---

# ADR-0050 · Quadro como projeção operacional multi-quadro N para N do projeto

## Decisão

- **D50** — ~~Quadro é uma projeção operacional do projeto, não o proprietário da tarefa. Um `WorkItem` é um
  agregado canônico do projeto, deve possuir ao menos uma associação de quadro e pode aparecer em vários
  quadros sem duplicação. Cada projeto possui um quadro padrão e novas tarefas entram em seu `Backlog` quando
  não houver seleção explícita. A posição é específica da projeção; o `WorkflowStatusId` permanece canônico e
  uma transição só é válida se todos os quadros associados puderem representá-la.~~ Sucedida pela D52. O modelo
  N:N permanece somente como estado implantado a migrar. (2026-08-20)
