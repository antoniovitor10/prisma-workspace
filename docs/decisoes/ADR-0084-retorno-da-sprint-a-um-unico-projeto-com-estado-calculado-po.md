---
id: ADR-0084
titulo: "Retorno da sprint a um único projeto com estado calculado por datas"
status: aceita
data: 2026-09-09
origem: "DECISIONS.md D84"
substitui: "ADR-0025, ADR-0081"
---

# ADR-0084 · Retorno da sprint a um único projeto com estado calculado por datas

## Decisão

- **D84 — SUCEDE E REVOGA D81.** Sprint volta a pertencer a **um único projeto**. O PO determinou em
  2026-09-09, textualmente: *"a sprint pode ter apenas 1 projeto, mas 1 projeto pode ter várias sprints"*.
  Ficam **revogados** a associação `SprintProject`, o `Sprint.OrganizationId` próprio e o backfill previsto em
  D81; o tenant da sprint continua sendo o do seu projeto e `Sprint.ProjectId` segue obrigatório. Permanecem
  válidas, vindas da v2: estado calculado pelas datas no fuso da organização, ausência da ação manual
  `Iniciar sprint`, várias sprints simultâneas por projeto quando os períodos se sobrepõem, exclusão que apenas
  desvincula tarefas de forma transacional, destino explícito para tarefas abertas no encerramento e
  planejamento hierárquico atômico. Acrescenta-se a regra de que **sprint encerrada não aceita novas tarefas**,
  defeito relatado pelo PO na mesma data. Consequência prática: o contrato deixa de exigir migration —
  `G-MIGRATION` passa a ser necessário apenas se a implementação alterar schema, por exemplo ao remover
  `Sprint.Status` da tabela. Ver `SPEC-S-003 v3`. (2026-09-09)
