---
id: HU-0002
titulo: "Quadros Kanban, colunas, ordem visual e status de workflow (pendências do backlog)"
trilha: funcionalidade
status: aberta
prd: nao
criado_em: 2026-10-21
atualizado_em: 2026-10-21
---

# HU-0002 · Quadros Kanban, colunas, ordem visual e status de workflow (pendências do backlog)

## História

**Como** usuário operacional e gestor de projeto no Prisma WorkSpace
**Quero** concluir as pendências herdadas de ordenação manual de cartões e colunas, classificação/backfill de categoria de etapa, ciclo de vida de quadros e alinhamento entre coluna atual e status de workflow
**Para que** o quadro Kanban e as visões por projeto operem sem empates de posição, sem inferência silenciosa de conclusão e com transições de etapa consistentes.

## Critérios de aceite

### CA-01 · Ordem manual compartilhada de cartões e isolamento de visão filtrada (TASK-018, TASK-210)
- **Dado que** um quadro Kanban com cartões em qualquer posição inicial (inclusive posição 0)
- **Quando** uma nova tarefa for inserida no topo da coluna ou o usuário reordenar cartões manualmente (com ou sem filtro ativo)
- **Então** a posição compartilhada persiste sem empate no topo e a reordenação em visão filtrada permanece isolada/temporária conforme o contrato de kanban-visual-order.md.

### CA-02 · Reordenação de colunas por arraste e teclado com rollback (TASK-027, TASK-124)
- **Dado que** um quadro com múltiplas colunas independentes
- **Quando** o usuário reordenar colunas por drag-and-drop ou atalho de teclado
- **Então** Stage.Position é persistido com atualização otimista e rollback em caso de erro, sem alterar o status nem a posição interna dos cartões da coluna.

### CA-03 · Backfill de categoria de coluna, ciclo de vida de quadro e unificação de status textual (TASK-105, TASK-109, TASK-BUG-001, TASK-103, TASK-106)
- **Dado que** que os gates humanos pendentes (G-MIGRATION, G-WORKFLOW e G-SCOPE) sejam deliberados pelo PO para cada item
- **Quando** as tarefas de backfill de Stage.Category, reconciliação de CompletedAt e unificação de WorkflowStatus com a coluna atual forem executadas
- **Então** não resta inferência por nome de coluna nem divergência entre coluna concluída e estado de conclusão da tarefa.

## Pendências herdadas do `backlog.md` (auditoria do PR #29)

Esta HU agrupa as pendências vivas (`parcial`, `não feita` e `blocked`) da área, migradas sem tasks filhas pré-criadas conforme `ADR-0103`. Ao puxar esta HU para refinamento/execução, cada pendência elegível será quebrada em tasks (`T-XXXX`) respeitando os gates humanos listados abaixo:

| ID original | Estado no backlog / auditoria PR #29 | Requisito original | O que falta / evidência da auditoria | Gate humano pendente | Referência da área |
|---|---|---|---|---|---|
| `TASK-018` | pending (auditoria PR #29: **parcial**) | Preservar colunas livres, inserir novas tarefas no topo e permitir ordem manual compartilhada, isolando a reordenação pessoal e temporária de visões filtradas. | [Kanban.tsx:845](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/src/pages/Kanban.tsx#L845) usa min(position)-100 limitado a 0; empate quando o primeiro cartão já está em 0. [kanbanOrdering.ts:26](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/src/features/board/kanbanOrdering.ts#L26) desempata por ID. Ordem vertical/filtro também permanece incompleta. — D60 aprovada; não exige migration para corrigir algoritmo, validar contrato de posições. Gate humano no backlog original: não. | não | `metodo/projeto/skills/dominio/referencias/kanban-visual-order.md` |
| `TASK-210` | pending (auditoria PR #29: **parcial**) | Fazer a tarefa recem-criada entrar no topo da coluna escolhida, conforme D60. | [Kanban.tsx:845](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/src/pages/Kanban.tsx#L845) usa min(position)-100 limitado a 0; empate quando o primeiro cartão já está em 0. [kanbanOrdering.ts:26](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/src/features/board/kanbanOrdering.ts#L26) desempata por ID. Ordem vertical/filtro também permanece incompleta. — D60 aprovada; não exige migration para corrigir algoritmo, validar contrato de posições. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/kanban-visual-order.md` |
| `TASK-027` | in_progress (auditoria PR #29: **parcial**) | Permitir reordenar colunas por arraste e teclado, persistindo Stage.Position com atualização otimista e rollback. | [Kanban.tsx:1455](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/src/pages/Kanban.tsx#L1455) oferece setas acessíveis; arraste em:922-999 é de cartões, não de colunas. — Reordenação visual coberta; preservar botões e persistência, sem modificar workflow. Gate humano no backlog original: sim (G-SPEC da SPEC-BOARDS-STAGES-WIP + G-WORKFLOW). | sim (G-SPEC da SPEC-BOARDS-STAGES-WIP + G-WORKFLOW) | `metodo/projeto/skills/dominio/referencias/boards-stages-wip.md` |
| `TASK-124` | pending (auditoria PR #29: **parcial**) | Completar a reordenacao de colunas com arraste, preservando os botoes acessiveis ja existentes. | [Kanban.tsx:1455](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/src/pages/Kanban.tsx#L1455) oferece setas acessíveis; arraste em:922-999 é de cartões, não de colunas. — Reordenação visual coberta; preservar botões e persistência, sem modificar workflow. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/boards-stages-wip.md` |
| `TASK-105` | blocked (**bloqueada**) | Produzir relatorio de colunas candidatas a concluida e, apos decisao humana, aplicar o backfill de Stage.Category. | Item bloqueado em backlog.md (prioridade P1, risco alto). Requer deliberação do gate antes de quebrar em task. | sim (estrategia de backfill em aberto; ver perguntas de gate) | `metodo/projeto/skills/dominio/referencias/boards-stages-wip.md` |
| `TASK-109` | pending (auditoria PR #29: **não feita**) | Adicionar IsArchived/ArchivedAt ao Board e arquivar o quadro junto com suas tarefas, com restauracao por administrador. | [Board.cs:8](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Domain/Entities/Board.cs#L8) não tem IsArchived/ArchivedAt; D89 define exclusão com transferência explícita. — Reconciliar escopo antigo D52 com D83/D89 antes de introduzir arquivamento; schema novo exigiria G-MIGRATION. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/boards-stages-wip.md` |
| `TASK-BUG-001` | pending (auditoria PR #29: **parcial**) | Fazer a coluna ser a unica fonte funcional de status e de conclusao da tarefa, corrigindo o relato do PO de que concluir no Kanban nao atualiza. | [MoveWorkItemCommandHandler.cs:144](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Application/Features/WorkItems/Commands/MoveWorkItemCommandHandler.cs#L144) conclui pela categoria; outras superfícies ainda exibem status genérico. A criação com categoria Em andamento está autorizada pela remediação E03 posterior à D82, não é defeito por si só. — D82 proíbe inferir/backfill automático; preservar dados. Não reabrir estratégia já recusada. Gate humano no backlog original: sim (G-WORKFLOW; G-MIGRATION se houver backfill; escolha da estrategia de backfill em aberto). | sim (G-WORKFLOW; G-MIGRATION se houver backfill; escolha da estrategia de backfill em aberto) | `metodo/projeto/skills/dominio/referencias/workflow-status.md` |
| `TASK-103` | pending (auditoria PR #29: **parcial**) | Usar exclusivamente o nome da coluna como status textual da tarefa em todas as superficies. | [TaskDetailDrawer.tsx:441](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx#L441) usa nome da coluna; [SprintBacklogPanel.tsx:255](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/src/features/scrum/SprintBacklogPanel.tsx#L255) troca por Concluído quando finalizada. — D65 aprovada; sem migration para corrigir rótulos. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/workflow-status.md` |
| `TASK-106` | pending (auditoria PR #29: **parcial**) | Retirar WorkflowStatus, templates e transicoes do governo do status, preservando dados e codigo como legado tecnico. | [WorkflowMoveGuard.cs:16](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Application/Features/Workflow/WorkflowMoveGuard.cs#L16) ignora workflow legado em colunas BoardId; legado sem quadro ainda valida transições. — Preservar compatibilidade D89; remoção física/reescrita exige gate específico. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/workflow-status.md` |

## Fora de escopo

- Tarefas já concluídas em `backlog.md` (`completed`, `done`, `implemented`, `cancelled`) ou descartadas na auditoria do PR #29 como `feita` (`TASK-100`, `TASK-101`, `TASK-104`, `TASK-108`, `TASK-508`) ou `sucedida` (`TASK-102`, `TASK-303`, `TASK-305`, `TASK-400`, `TASK-403`).
- Execução imediata de itens com `G-MIGRATION`, `G-WORKFLOW`, `G-HISTORY`, `G-SCOPE` ou `G-DEPLOY` sem aprovação explícita prévia do Vitor (PO).

## Notas e referências

- **Referências de contrato vigente da área (skill `dominio`):**
- `metodo/projeto/skills/dominio/referencias/boards-stages-wip.md`
- `metodo/projeto/skills/dominio/referencias/kanban-visual-order.md`
- `metodo/projeto/skills/dominio/referencias/workflow-status.md`
- `metodo/projeto/skills/dominio/referencias/board-as-view.md`
- `metodo/projeto/skills/dominio/referencias/independent-board-columns.md`
- **ADRs relacionadas:** `ADR-0060`, `ADR-0062`, `ADR-0065`, `ADR-0082`, `ADR-0083`, `ADR-0089`, `ADR-0103`
- **Origem:** migração sob demanda de `backlog.md` reconciliado pela auditoria do PR #29 (`origin/docs/backlog-audit-20261020:relatorios/oficina-backlog-audit.md`).
