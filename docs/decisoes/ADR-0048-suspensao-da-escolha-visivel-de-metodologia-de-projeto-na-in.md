---
id: ADR-0048
titulo: "Suspensão da escolha visível de metodologia de projeto na interface"
status: aceita
data: 2026-08-20
origem: "DECISIONS.md D48"
substitui: ""
---

# ADR-0048 · Suspensão da escolha visível de metodologia de projeto na interface

## Decisão

- **D48** — A escolha de metodologia/estrutura de trabalho fica suspensa na experiência do produto. A interface
  não deve exibir, permitir selecionar ou editar Kanban, Scrum, Scrumban ou Lista simples. Projetos novos usam
  internamente `ProjectMethodology.Kanban` como valor compatível; projetos existentes preservam o valor já
  persistido. O enum, a coluna e os contratos permanecem temporariamente para retrocompatibilidade, sem migration.
  Esta decisão substitui somente a parte visível de metodologia prevista em D20/D42 e em
  `SPEC-PROJECT-STRUCTURE`. (2026-08-20)
