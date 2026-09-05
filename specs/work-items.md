# SPEC-WORK-ITEMS: Story Points condicionados à metodologia

**Status:** superseded

**Substituída por:**

- `SPEC-WORK-ITEM-MANAGEMENT` (`specs/work-item-management.md`) para o contrato canônico de tarefas.
- `SPEC-PROJECT-METHODOLOGY-HIDDEN` (`specs/project-methodology-hidden.md`) para metodologia e XP ocultos.

## Motivo da substituição

Esta spec definia que Story Points seriam ocultos apenas em projetos classificados internamente como Kanban e
continuariam visíveis em Scrum, Scrumban e Lista simples. Esse comportamento dependia de `Project.Methodology`
para variar a experiência funcional.

A decisão humana posterior tornou metodologia e XP totalmente ocultos e proibiu que o valor técnico de
metodologia habilite ou oculte recursos visíveis. Ao mesmo tempo, o contrato completo e mais recente de
`WorkItem` foi consolidado em `SPEC-WORK-ITEM-MANAGEMENT`, conforme D52–D54, D57, D61, D63, D65 e as decisões já
registradas sobre criação, posição singular, movimentação, conclusão, responsabilidade, lixeira, arquivamento,
subtarefas, histórico e concorrência.

Por isso, esta spec não é mais fonte normativa e não autoriza implementação.

## Contrato vigente

- Criação e gestão de tarefas seguem exclusivamente `SPEC-WORK-ITEM-MANAGEMENT`.
- Metodologia, XP e qualquer variação visual baseada em metodologia seguem
  `SPEC-PROJECT-METHODOLOGY-HIDDEN`.
- Dados e campos internos existentes podem permanecer por compatibilidade até uma mudança futura aprovada.
- Nenhuma migration, remoção de campo ou alteração de produto é autorizada por esta substituição documental.

## Valor histórico

- `TASK-004` e `TASK-012` permanecem no backlog como tarefas concluídas vinculadas a esta spec para preservar a
  rastreabilidade do comportamento implementado na época.
- Os componentes e testes citados na versão anterior explicam o acoplamento atual entre Story Points e
  metodologia, hoje registrado como gap em `SPEC-PROJECT-METHODOLOGY-HIDDEN`.
- A eventual correção do produto deve partir das specs vigentes e dos gates aplicáveis, não deste documento.

## Gaps conhecidos

1. Superfícies atuais ainda consultam `project.methodology` para decidir se Story Points aparecem.
2. O backlog histórico ainda descreve a regra condicional por Kanban em `TASK-004` e `TASK-012`; as tarefas
   ficam preservadas como evidência histórica, não como contrato futuro.
3. O Context Explorer possui análise manual estática para `SPEC-WORK-ITEMS` com texto anterior à substituição;
   essa explicação visual deve ser ajustada em tarefa própria da ferramenta para não sugerir que esta spec ainda
   é canônica.

## Traceability histórica

CAND-F-003 → SPEC-WORK-ITEMS → TASK-004/TASK-012 → implementação histórica de Story Points →
superseded por D48 / SPEC-PROJECT-METHODOLOGY-HIDDEN e por D52–D54 / SPEC-WORK-ITEM-MANAGEMENT.
