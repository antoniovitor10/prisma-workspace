---
id: HU-0003
titulo: "Tarefas, criação rápida, exclusão em árvore, dependências e anexos (pendências do backlog)"
trilha: funcionalidade
status: aberta
prd: nao
criado_em: 2026-10-21
atualizado_em: 2026-10-21
---

# HU-0003 · Tarefas, criação rápida, exclusão em árvore, dependências e anexos (pendências do backlog)

## História

**Como** membro de equipe que cria e gerencia tarefas e subtarefas no Prisma WorkSpace
**Quero** concluir as pendências herdadas de criação rápida por projeto/quadro, exclusão de tarefa com tratamento explícito de filhas, conclusão recursiva, grafo de dependências e validação de anexos
**Para que** o ciclo de vida do agregado WorkItem respeite todos os invariantes de hierarquia, bloqueio e armazenamento seguro.

## Critérios de aceite

### CA-01 · Validação e invariantes na criação rápida e no ciclo de vida Pai/Filhas (TASK-107, TASK-120, TASK-121)
- **Dado que** uma tarefa com ou sem subtarefas em um projeto
- **Quando** o usuário criar rapidamente um item, excluir uma tarefa pai ou concluir/reabrir uma árvore de tarefas
- **Então** os campos obrigatórios do contrato vigente são validados, a exclusão oferece tratamento explícito da descendência e a conclusão/reabertura aplica a regra atômica recursiva da ADR-0062.

### CA-02 · Grafo visual de dependências e limites de anexos (TASK-306, TASK-309)
- **Dado que** uma tarefa com dependências tipadas ou anexos enviados pelo usuário
- **Quando** a visualização de dependências ou o upload/remoção de anexos for acionado
- **Então** a relação de bloqueio e prevenção de ciclos respeita dependencies.md (e ADR-0099) e o armazenamento de anexos valida tipo, tamanho e exclusão lógica conforme attachments.md.

## Pendências herdadas do `backlog.md` (auditoria do PR #29)

Esta HU agrupa as pendências vivas (`parcial`, `não feita` e `blocked`) da área, migradas sem tasks filhas pré-criadas conforme `ADR-0103`. Ao puxar esta HU para refinamento/execução, cada pendência elegível será quebrada em tasks (`T-XXXX`) respeitando os gates humanos listados abaixo:

| ID original | Estado no backlog / auditoria PR #29 | Requisito original | O que falta / evidência da auditoria | Gate humano pendente | Referência da área |
|---|---|---|---|---|---|
| `TASK-107` | pending (auditoria PR #29: **parcial**) | Exigir titulo, projeto, responsavel, quadro e coluna na criacao de tarefa, na API e na UI. | [CreateWorkItemCommandValidator.cs:14](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Application/Features/WorkItems/Commands/CreateWorkItemCommandValidator.cs#L14) aceita BoardId OU ProjectId; ResponsibleId e StageId opcionais no comando:13,27. — Spec aprovada; risco alto. PR27 corrige só caminho de subtarefa, não toda esta task. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/quick-create-work-item.md` |
| `TASK-120` | pending (auditoria PR #29: **não feita**) | Criar lixeira de 7 dias do WorkItem distinta de arquivar, com escolha sobre subtarefas, reparenting a avo e restauracao familiar. | [WorkItem.cs:82](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Domain/Entities/WorkItem.cs#L82) possui arquivamento; não há estado de lixeira/DeletedAt nem restauração familiar. — G-MIGRATION necessário; proibida migration nova neste goal. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/work-item-management.md` |
| `TASK-121` | pending (auditoria PR #29: **não feita**) | Condicionar a conclusao do pai a todas as subtarefas concluidas e oferecer acao atomica de concluir a familia. | [MoveWorkItemCommandHandler.cs:118](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Application/Features/WorkItems/Commands/MoveWorkItemCommandHandler.cs#L118) e:144 não condicionam conclusão às filhas; [WorkflowMoveGuard.cs:9](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Application/Features/Workflow/WorkflowMoveGuard.cs#L9) também não implementa essa regra geral. — G-WORKFLOW declarado; não assumir concessão por aprovação de spec. Não confundir com reclassificação coletiva já feita. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/work-item-management.md` |
| `TASK-306` | pending (auditoria PR #29: **não feita**) | Remover o efeito operacional de IsBlocked em filtros, badges, priorizacao, notificacoes, relatorios e automacoes. | [MyWorkDashboardFeature.cs:223](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Application/Features/Me/MyWorkDashboardFeature.cs#L223) e [ReportBuilderFeature.cs:459](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Application/Features/Reports/ReportBuilderFeature.cs#L459) ainda derivam bloqueio de links. — Reconciliar efeitos fora do detalhe com D99; risco alto. Não ampliar nem apagar operação por interpretação automática. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/dependencies.md` |
| `TASK-309` | pending (auditoria PR #29: **não feita**) | Implementar lixeira de 7 dias para anexos de tarefa, distinta da exclusao imediata atual. | [AttachmentsFeature.cs:172](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Application/Features/Attachments/AttachmentsFeature.cs#L172) registra explicitamente lixeira de 7 dias pendente G-MIGRATION. — G-MIGRATION necessário e proibido neste goal. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/attachments.md` |

## Fora de escopo

- Tarefas já concluídas em `backlog.md` (`completed`, `done`, `implemented`, `cancelled`) ou descartadas na auditoria do PR #29 como `feita` (`TASK-100`, `TASK-101`, `TASK-104`, `TASK-108`, `TASK-508`) ou `sucedida` (`TASK-102`, `TASK-303`, `TASK-305`, `TASK-400`, `TASK-403`).
- Execução imediata de itens com `G-MIGRATION`, `G-WORKFLOW`, `G-HISTORY`, `G-SCOPE` ou `G-DEPLOY` sem aprovação explícita prévia do Vitor (PO).

## Notas e referências

- **Referências de contrato vigente da área (skill `dominio`):**
- `metodo/projeto/skills/dominio/referencias/work-item-management.md`
- `metodo/projeto/skills/dominio/referencias/quick-create-work-item.md`
- `metodo/projeto/skills/dominio/referencias/dependencies.md`
- `metodo/projeto/skills/dominio/referencias/attachments.md`
- **ADRs relacionadas:** `ADR-0004`, `ADR-0008`, `ADR-0021`, `ADR-0062`, `ADR-0083`, `ADR-0099`, `ADR-0103`
- **Origem:** migração sob demanda de `backlog.md` reconciliado pela auditoria do PR #29 (`origin/docs/backlog-audit-20261020:relatorios/oficina-backlog-audit.md`).
