---
id: HU-0004
titulo: "Backlog do projeto, lixeira e ciclo de sprints (pendências do backlog)"
trilha: funcionalidade
status: aberta
prd: nao
criado_em: 2026-10-21
atualizado_em: 2026-10-21
---

# HU-0004 · Backlog do projeto, lixeira e ciclo de sprints (pendências do backlog)

## História

**Como** gestor de projeto e membro de equipe que planeja backlog e sprints no Prisma WorkSpace
**Quero** concluir as pendências herdadas de preservação de estado no backlog, lixeira de 7 dias, planejamento hierárquico, fuso horário e encerramento de sprint sob o modelo 1:N por projeto (ADR-0084)
**Para que** o planejamento iterativo funcione de forma atômica e auditável sem reativar o modelo N:N revogado.

## Critérios de aceite

### CA-01 · Estado do backlog na URL e lixeira/restauração familiar (TASK-213, TASK-021)
- **Dado que** o usuário navegando no backlog do projeto ou excluindo itens com hierarquia
- **Quando** aplicar filtros/ordenação ou acionar a lixeira de 7 dias (após G-MIGRATION para TASK-021)
- **Então** o estado de navegação do backlog persiste na URL/sessão e a exclusão/restauração preserva a estrutura familiar conforme backlog.md.

### CA-02 · Planejamento hierárquico, estado automático por data e encerramento de sprint 1:N (TASK-022, TASK-023, TASK-202, TASK-205, TASK-208, TASK-209)
- **Dado que** uma sprint pertencente a um único projeto conforme ADR-0084 (que revogou o N:N de SprintProject da ADR-0081)
- **Quando** o usuário planejar itens com subtarefas, criar a primeira sprint mantendo a seleção, consultar o estado calculado no fuso da organização ou encerrar a sprint
- **Então** o fechamento hierárquico é atômico, a exclusão de sprint apenas desvincula SprintId preservando quadro/coluna (ADR-0054) e os itens exclusivos do modelo N:N revogado (TASK-200, TASK-201, TASK-203, TASK-204, TASK-206, TASK-207) permanecem bloqueados/descartados salvo nova deliberação humana.

## Pendências herdadas do `backlog.md` (auditoria do PR #29)

Esta HU agrupa as pendências vivas (`parcial`, `não feita` e `blocked`) da área, migradas sem tasks filhas pré-criadas conforme `ADR-0103`. Ao puxar esta HU para refinamento/execução, cada pendência elegível será quebrada em tasks (`T-XXXX`) respeitando os gates humanos listados abaixo:

| ID original | Estado no backlog / auditoria PR #29 | Requisito original | O que falta / evidência da auditoria | Gate humano pendente | Referência da área |
|---|---|---|---|---|---|
| `TASK-021` | pending (auditoria PR #29: **não feita**) | Implementar lixeira de 7 dias separada de Arquivar, opções para excluir pai com/sem subtarefas, reparenting à avó ou independência, restauração familiar e filtro/restauração de arquivadas. | [WorkItem.cs:82](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Domain/Entities/WorkItem.cs#L82) possui arquivamento; não há estado de lixeira/DeletedAt nem restauração familiar. — G-MIGRATION necessário; proibida migration nova neste goal. Gate humano no backlog original: sim (G-SPEC da revisão da SPEC-B-001). | sim (G-SPEC da revisão da SPEC-B-001) | `metodo/projeto/skills/dominio/referencias/backlog.md` |
| `TASK-213` | pending (auditoria PR #29: **parcial**) | Serializar e restaurar filtros, item aberto, expansao e rolagem na URL do Product Backlog. | [BacklogPlanner.tsx:940](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/src/features/scrum/BacklogPlanner.tsx#L940) serializa item aberto; filtros/expansão não são restaurados integralmente pela URL e preferências parciais vão ao localStorage:1108. — Spec aprovada; sem migration, preservar dados sensíveis fora da URL. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/backlog.md` |
| `TASK-022` | pending (auditoria PR #29: **parcial**) | Aplicar fechamento hierárquico no planejamento da sprint e, quando nenhuma sprint existir, permitir criar a primeira preservando a seleção atual. | [BacklogFeature.cs:156](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Application/Features/Backlog/BacklogFeature.cs#L156) carrega apenas IDs explicitamente enviados; [BacklogPlanner.tsx:1187](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/src/features/scrum/BacklogPlanner.tsx#L1187) planeja seleção, sem criar primeira sprint nesse fluxo. — D84 resolveu Sprint1:N; fechamento hierárquico exige respeitar SPEC-S-003, sem reativar N:N. Gate humano no backlog original: sim (G-SPEC da revisão da SPEC-S-003). | sim (G-SPEC da revisão da SPEC-S-003) | `metodo/projeto/skills/dominio/referencias/sprints.md` |
| `TASK-023` | pending (auditoria PR #29: **parcial**) | Expor edição/exclusão segura de sprint, removendo somente SprintId e preservando quadro/coluna/tarefa conforme D54; recuperar conflitos 409 e remover iniciação manual após G-SCOPE/G-WORKFLOW. | [SprintsFeature.cs:221](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Application/Features/Sprints/SprintsFeature.cs#L221) exclui via RemoveWithUnlinkAsync; [SprintRepository.cs:27](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Infrastructure/Repositories/SprintRepository.cs#L27) usa transação. Datas ainda usam UTC no handler:60,166. — D84 substitui N:N; verificar fuso da organização e contratos atuais antes de ampliar. Gate humano no backlog original: sim (G-SPEC da revisão da SPEC-S-003 + G-SCOPE/G-WORKFLOW para ativação automática). | sim (G-SPEC da revisão da SPEC-S-003 + G-SCOPE/G-WORKFLOW para ativação automática) | `metodo/projeto/skills/dominio/referencias/sprints.md` |
| `TASK-200` | blocked (**bloqueada**) | Criar SprintProject e Sprint.OrganizationId no dominio, na configuracao EF e nos indices. | Item bloqueado em backlog.md (prioridade P0, risco alto). Requer deliberação do gate antes de quebrar em task. | sim (G-SPEC pendente + G-MIGRATION) | `metodo/projeto/skills/dominio/referencias/sprints.md` |
| `TASK-201` | blocked (**bloqueada**) | Migration incremental com backfill idempotente e verificavel de Sprint.ProjectId para SprintProject. | Item bloqueado em backlog.md (prioridade P0, risco alto). Requer deliberação do gate antes de quebrar em task. | sim | `metodo/projeto/skills/dominio/referencias/sprints.md` |
| `TASK-202` | blocked (**bloqueada**) | Calcular o estado da sprint pelas datas, remover Iniciar sprint e a exclusividade de sprint ativa. | Item bloqueado em backlog.md (prioridade P0, risco alto). Requer deliberação do gate antes de quebrar em task. | sim | `metodo/projeto/skills/dominio/referencias/sprints.md` |
| `TASK-203` | blocked (**bloqueada**) | Endpoints de projetos participantes, com remocao exigindo destino atomico das tarefas vinculadas. | Item bloqueado em backlog.md (prioridade P0, risco alto). Requer deliberação do gate antes de quebrar em task. | sim | `metodo/projeto/skills/dominio/referencias/sprints.md` |
| `TASK-204` | blocked (**bloqueada**) | Validar que o projeto da tarefa participa da sprint antes de aceitar o vinculo. | Item bloqueado em backlog.md (prioridade P0, risco medio). Requer deliberação do gate antes de quebrar em task. | sim | `metodo/projeto/skills/dominio/referencias/sprints.md` |
| `TASK-205` | blocked (**bloqueada**) | Implementar DELETE de sprint transacional que remove somente o SprintId das tarefas. | Item bloqueado em backlog.md (prioridade P1, risco medio). Requer deliberação do gate antes de quebrar em task. | sim | `metodo/projeto/skills/dominio/referencias/sprints.md` |
| `TASK-206` | blocked (**bloqueada**) | Trocar a autorizacao fixa por permissao configuravel avaliada em todos os projetos participantes. | Item bloqueado em backlog.md (prioridade P1, risco alto). Requer deliberação do gate antes de quebrar em task. | sim | `metodo/projeto/skills/dominio/referencias/sprints.md` |
| `TASK-207` | blocked (**bloqueada**) | Metricas consolidadas e segmentaveis por projeto no dashboard e no quadro da sprint. | Item bloqueado em backlog.md (prioridade P1, risco medio). Requer deliberação do gate antes de quebrar em task. | sim | `metodo/projeto/skills/dominio/referencias/sprints.md` |
| `TASK-208` | blocked (**bloqueada**) | Planejamento hierarquico atomico com criacao de sprint preservando a selecao atual. | Item bloqueado em backlog.md (prioridade P1, risco medio). Requer deliberação do gate antes de quebrar em task. | sim | `metodo/projeto/skills/dominio/referencias/sprints.md` |
| `TASK-209` | blocked (**bloqueada**) | Encerramento da sprint exigindo destino explicito para as tarefas abertas, com rollback integral em falha. | Item bloqueado em backlog.md (prioridade P1, risco alto). Requer deliberação do gate antes de quebrar em task. | sim (G-HISTORY se SprintItemSnapshot mudar de estrutura) | `metodo/projeto/skills/dominio/referencias/sprints.md` |

## Fora de escopo

- Tarefas já concluídas em `backlog.md` (`completed`, `done`, `implemented`, `cancelled`) ou descartadas na auditoria do PR #29 como `feita` (`TASK-100`, `TASK-101`, `TASK-104`, `TASK-108`, `TASK-508`) ou `sucedida` (`TASK-102`, `TASK-303`, `TASK-305`, `TASK-400`, `TASK-403`).
- Execução imediata de itens com `G-MIGRATION`, `G-WORKFLOW`, `G-HISTORY`, `G-SCOPE` ou `G-DEPLOY` sem aprovação explícita prévia do Vitor (PO).

## Notas e referências

- **Referências de contrato vigente da área (skill `dominio`):**
- `metodo/projeto/skills/dominio/referencias/backlog.md`
- `metodo/projeto/skills/dominio/referencias/sprints.md`
- **ADRs relacionadas:** `ADR-0025`, `ADR-0054`, `ADR-0081`, `ADR-0084`, `ADR-0103`
- **Origem:** migração sob demanda de `backlog.md` reconciliado pela auditoria do PR #29 (`origin/docs/backlog-audit-20261020:relatorios/oficina-backlog-audit.md`).
