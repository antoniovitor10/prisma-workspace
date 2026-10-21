---
id: ADR-0099
titulo: "Reconciliação de visibilidade de Gantt, dependências, Linha do tempo e ocultações"
status: aceita
data: 2026-10-14
origem: "DECISIONS.md D99"
substitui: "ADR-0059"
---

# ADR-0099 · Reconciliação de visibilidade de Gantt, dependências, Linha do tempo e ocultações

## Decisão

- **D99** — Decisões do PO em 2026-10-14 sobre as divergências entre specs aprovadas e o produto, levantadas pela varredura de QA (Farol) e pelo lote L07 de testes. (1) Gantt e calendário permanecem visíveis no seletor de visualização do Kanban: a SPEC-GANTT-001 deixa de exigir ocultação e as duas visões entram no plano de testes de ponta a ponta. (2) Dependências e bloqueios permanecem visíveis no detalhe da tarefa com o comportamento atual: a SPEC-F-009 deixa de exigir ocultação, a D59 fica substituída nesse ponto e esta decisão é o G-SCOPE de reativação; expansões futuras do módulo continuam exigindo decisão própria. (3) A aba Linha do tempo da SPEC-TASK-HISTORY passa a existir como aba principal de movimentações e o Grafo de estados continua disponível como visão complementar, em vez de ser substituído. (4) Seguem como já aprovado: automações ocultas na interface (SPEC de ações em massa e automações), lead time e cycle time ocultos (SPEC de auditoria e lead time) e nome completo obrigatório no cadastro (SPEC de autenticação e segurança). O PO também decidiu corrigir todos os defeitos P1 e P2 da varredura e os defeitos de tarefas do L07 antes de seguir com os lotes de teste, e intercalar as melhorias de usabilidade com os lotes seguintes.
