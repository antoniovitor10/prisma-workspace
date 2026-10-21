---
id: ADR-0033
titulo: "Pesquisa global paginada sob tenant e permissões"
status: aceita
data: 2026-07-20
origem: "DECISIONS.md D33"
substitui: ""
---

# ADR-0033 · Pesquisa global paginada sob tenant e permissões

## Decisão

- **D33** — A pesquisa global é uma projeção paginada e limitada no servidor sobre tarefas,
  solicitações, projetos, usuários, equipes e sprints, sempre sob tenant e permissões do usuário.
  Ela retorna somente metadados mínimos e destinos de navegação; comandos rápidos são um catálogo
  fechado do frontend e não aceitam código ou rotas fornecidas pelo usuário. (2026-07-20)
