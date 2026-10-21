---
id: ADR-0040
titulo: "Execução obrigatória de testes E2E na Definition of Done"
status: aceita
data: 2026-08-19
origem: "DECISIONS.md D40"
substitui: ""
---

# ADR-0040 · Execução obrigatória de testes E2E na Definition of Done

## Decisão

- **D40** — Toda tarefa que modifique frontend (React), endpoints da API (.NET) ou schema de banco de dados
  deve ter os testes E2E executados e passando como parte da Definition of Done. Antes de considerar a
  tarefa concluída, execute `npm run e2e` em `src/Prisma.Workspace.Web` com o ambiente E2E ativo (API +
  frontend + SQL Server). Se os testes falharem, a tarefa não está completa até que as falhas sejam
  corrigidas ou novos testes sejam adicionados para cobrir a funcionalidade implementada. Esta regra
  não se aplica a tarefas que modificam apenas documentação, configurações de infraestrutura sem impacto
  no comportamento, ou refatorações internas sem mudança de interface. (2026-08-19)
