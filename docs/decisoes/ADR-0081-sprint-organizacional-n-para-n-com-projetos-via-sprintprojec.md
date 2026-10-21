---
id: ADR-0081
titulo: "Sprint organizacional N para N com projetos via SprintProject"
status: substituida # substituida por ADR-0084
data: 2026-09-08
origem: "DECISIONS.md D81"
substitui: "ADR-0025"
substituida_por: "ADR-0084"
---

# ADR-0081 · Sprint organizacional N para N com projetos via SprintProject

## Decisão

- **D81 — PROPOSTA, NÃO APROVADA** — Sucessora de D25. Sprint deixa de pertencer a um único projeto e passa a
  pertencer à **Organization**, com `Team` opcional. Introduz `SprintProject` (`SprintId` + `ProjectId`): uma
  sprint reúne vários projetos e um projeto participa de várias sprints. `WorkItem` continua em um único
  `Project` e em no máximo uma sprint, e só pode ser vinculado a uma sprint que contenha o seu projeto.
  Adicionar ou remover sprint nunca altera `Board`, `Stage` ou `Position`. Remover um projeto de uma sprint exige
  tratar atomicamente as tarefas vinculadas daquele projeto, com destino explícito. Métricas são consolidadas e
  segmentáveis por projeto. Permissões são validadas em todos os projetos envolvidos, sem execução parcial. O
  backfill de `Sprint.ProjectId` para `SprintProject` é obrigatório e verificável. O estado da sprint passa a ser
  calculado pelas datas, não existe `Iniciar sprint` e períodos sobrepostos são permitidos. Excluir sprint remove
  somente o `SprintId` das tarefas; tarefas e histórico nunca são excluídos. Planejamento hierárquico é atômico e
  tarefas abertas no encerramento exigem destino explícito. **Esta proposta sucede D25 somente se e quando o PO
  aprovar o `G-SPEC` da `SPEC-S-003 v2`.** Até lá, D25 permanece vigente e nenhuma implementação é autorizada.
  Gates exigidos: `G-SPEC` (pendente), `G-MIGRATION` (obrigatório), `G-WORKFLOW` (obrigatório) e `G-HISTORY`
  (somente se `SprintItemSnapshot` mudar de estrutura). (proposta em 2026-09-08)

  Atualização — **APROVADA em 2026-09-08.** O PO leu o contrato e respondeu *"Aprovo, pode implementar"* ao `G-SPEC` da
  `SPEC-S-003 v2`. A `SPEC-S-003` passa a `approved` e D81 sucede D25, que fica revogada quanto à regra de uma
  única sprint ativa e à transição manual de status. `G-MIGRATION` e `G-WORKFLOW` ficam cobertos pela mesma
  instrução, combinada com a determinação anterior do PO de que a Sprint N:N deve rodar em produção
  (*"no prisma em si, quero ver no prisma.nordevs.com.br"*), que não é realizável sem a alteração de schema e de
  regra de workflow. `G-HISTORY` permanece exigido se e quando `SprintItemSnapshot` mudar de estrutura.
  (2026-09-08)
