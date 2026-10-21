---
id: ADR-0082
titulo: "Classificação explícita da categoria de coluna sem inferência nem backfill"
status: aceita
data: 2026-09-08
origem: "DECISIONS.md D82"
substitui: ""
---

# ADR-0082 · Classificação explícita da categoria de coluna sem inferência nem backfill

## Decisão

- **D82** — Classificação de coluna escolhida pela pessoa, nunca inferida. O PO decidiu em 2026-09-08, entre três
  alternativas apresentadas, que a categoria da coluna (`StageCategory`) é escolhida explicitamente na interface.
  Ficam recusadas a inferência por nome e a convenção de "última coluna é a de conclusão", porque ambas concluem
  tarefa sem intenção humana. Consequências: `StageDto` passa a expor `Category`; a criação de coluna no Kanban
  oferece o campo de classificação; a reclassificação continua pela configuração de workflow do projeto, que já
  propaga `status.Category` para a etapa. O PO também declarou que *"todos os dados atuais são fakes"*, portanto
  **não há backfill** de colunas existentes e nenhuma tarefa histórica é reclassificada em massa. (2026-09-08)
