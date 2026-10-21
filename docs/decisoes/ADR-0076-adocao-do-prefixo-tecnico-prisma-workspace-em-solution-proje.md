---
id: ADR-0076
titulo: "Adoção do prefixo técnico Prisma.Workspace em solution, projetos e namespaces"
status: aceita
data: 2026-09-05
origem: "DECISIONS.md D76"
substitui: ""
---

# ADR-0076 · Adoção do prefixo técnico Prisma.Workspace em solution, projetos e namespaces

## Decisão

- **D76** — No novo repositório, solution, projetos, pastas, assemblies e namespaces técnicos adotam o prefixo
  `Prisma.Workspace`. A mudança é exclusivamente de identidade técnica: IDs e classes históricas de migrations,
  schema, tabelas, nomes de banco existentes e comportamento não são alterados. Configurações e nomes de banco
  ainda legados serão tratados separadamente na instalação independente, com `G-MIGRATION` quando houver impacto
  persistente. (2026-09-05)
