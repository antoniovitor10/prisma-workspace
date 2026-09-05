# SPEC-MULTI-BOARD-VIEWS: Tarefas em Múltiplos Quadros

**Status:** superseded

**Substituída por:** `SPEC-BOARDS-STAGES-WIP` (`specs/boards-stages-wip.md`) e D52.

## Motivo da substituição

Esta spec definiu a relação N:N `WorkItem -> WorkItemBoardPlacement -> Board`, quadro padrão por projeto,
entrada automática em coluna `Backlog` e sincronização do mesmo status entre várias projeções. Esse modelo foi
implementado e permanece no código/banco atual, mas deixou de ser o contrato funcional desejado após decisão
explícita do PO em 2026-08-24.

O contrato vigente passa a ser:

- quadro como fluxo operacional transversal, podendo reunir tarefas de projetos diferentes;
- uma única posição operacional por tarefa (`Board` + `Stage` + posição);
- transferência entre quadros, e não projeção simultânea;
- colunas com nome e ordem livres, classificadas internamente como abertas ou concluídas;
- nenhuma coluna `Backlog` obrigatória e nenhum quadro padrão por projeto;
- Kanban como entrada principal, com seletor superior de quadros e painel de filtros contextual.

## Valor histórico

- A migration `MultiBoard_WorkItemPlacements` e a TASK-029 comprovam por que o modelo atual contém placements
  N:N e regras de compatibilidade entre quadros.
- Esta spec deve continuar no repositório para explicar o schema implantado e apoiar a futura migração segura.
- Nenhuma regra deste arquivo autoriza nova implementação.
- A reversão do N:N exige `G-MIGRATION`, plano de escolha da posição canônica por tarefa, validação de
  ambiguidades e rollback verificável.

## Traceability histórica

CAND-MULTI-BOARD-VIEWS -> SPEC-MULTI-BOARD-VIEWS -> TASK-029 -> migration
`MultiBoard_WorkItemPlacements` -> superseded por D52 / SPEC-BOARDS-STAGES-WIP.
