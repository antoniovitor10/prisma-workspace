---
id: HU-0009
titulo: "Auditoria, linha do tempo da tarefa e apontamento de horas (pendências do backlog)"
trilha: funcionalidade
status: aberta
prd: nao
criado_em: 2026-10-21
atualizado_em: 2026-10-21
---

# HU-0009 · Auditoria, linha do tempo da tarefa e apontamento de horas (pendências do backlog)

## História

**Como** gestor e membro de equipe que audita histórico e registra horas trabalhadas no Prisma WorkSpace
**Quero** concluir a cobertura de eventos na aba Linha do Tempo, remover referências residuais a XP em auditoria/métricas e finalizar os ajustes da frente de apontamento de horas
**Para que** o histórico da tarefa e o controle de horas/capacidade reflitam fielmente task-history.md, audit-leadtime-history.md e time-tracking.md.

## Critérios de aceite

### CA-01 · Linha do tempo da tarefa e limpeza de resíduos de XP (TASK-302, TASK-304)
- **Dado que** o detalhe da tarefa e as projeções de auditoria/métricas
- **Quando** a aba Linha do Tempo for aberta ou relatórios de histórico forem consultados
- **Então** a timeline exibe criação, movimentações com duração na coluna anterior e eventos principais sem grafo legado e sem qualquer campo ou rótulo de XP (ADR-0042).

### CA-02 · Regras e permissões de apontamento de horas (TASK-307, TASK-308)
- **Dado que** o registro, edição e exclusão de lançamentos em TimeEntry
- **Quando** um membro ou gestor lançar ou editar horas em tarefas abertas ou concluídas
- **Então** as validações de autoria, permissão de gestão, valores positivos e regras de tarefa concluída de time-tracking.md são integralmente cumpridas.

## Pendências herdadas do `backlog.md` (auditoria do PR #29)

Esta HU agrupa as pendências vivas (`parcial`, `não feita` e `blocked`) da área, migradas sem tasks filhas pré-criadas conforme `ADR-0103`. Ao puxar esta HU para refinamento/execução, cada pendência elegível será quebrada em tasks (`T-XXXX`) respeitando os gates humanos listados abaixo:

| ID original | Estado no backlog / auditoria PR #29 | Requisito original | O que falta / evidência da auditoria | Gate humano pendente | Referência da área |
|---|---|---|---|---|---|
| `TASK-302` | pending (auditoria PR #29: **não feita**) | Substituir a aba Grafo de estados pela Linha do tempo textual das movimentacoes entre colunas (D61). | [TaskDetailDrawer.tsx:469](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx#L469) só apresenta Grafo de estados; não há aba Linha do tempo no baseline. — D99 aprova Linha do tempo principal e grafo complementar; C5 autorizado sem alteração estrutural de histórico. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/task-history.md` |
| `TASK-304` | pending (auditoria PR #29: **não feita**) | Ocultar lead time e cycle time da interface, preservando a coleta interna. | [Kanban.tsx:1265](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/src/pages/Kanban.tsx#L1265) oferece LeadTime e:2047 monta modal. — D99 confirma ocultação; preservar coleta/backend. C5 autorizado. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/audit-leadtime-history.md` |
| `TASK-307` | pending (auditoria PR #29: **parcial**) | Aplicar as regras aprovadas de precisao, jornada diaria, sobreposicao e timezone no apontamento. | [CreateManualTimeEntryCommandHandler.cs:40](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Application/Features/TimeEntries/Commands/CreateManualTimeEntryCommandHandler.cs#L40) só cria/adiciona; diário em UTC [GetMyDailyByTaskQueryHandler.cs:26](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Application/Features/MeTime/Queries/GetMyDailyByTaskQueryHandler.cs#L26); semanal não divide meia-noite. — Decisões de horas em GOALS existem; implementação está em branch de horas fora da main. Comparar/revalidar sem duplicar ou publicar automaticamente. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/time-tracking.md` |
| `TASK-308` | pending (auditoria PR #29: **parcial**) | Definir e implementar o comportamento do apontamento em tarefa concluida. | [Spec aprovada:39](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/specs/time-tracking.md#L39) permite lançar/corrigir horas em tarefa concluída. [StartTimerCommandHandler.cs:36](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Application/Features/TimeEntries/Commands/StartTimerCommandHandler.cs#L36) não bloqueia CompletedAt, coerente com a decisão. Correção/exclusão e homologação dependem do restante de horas, fora da main. — A pergunta antiga foi respondida na spec aprovada: permitir, sem exigir justificativa extra. Não há G-SCOPE novo para escolher essa regra. TASK-307 e TASK-BUG-001 continuam dependências; preservar status pending até cobertura integral. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/time-tracking.md` |

## Fora de escopo

- Tarefas já concluídas em `backlog.md` (`completed`, `done`, `implemented`, `cancelled`) ou descartadas na auditoria do PR #29 como `feita` (`TASK-100`, `TASK-101`, `TASK-104`, `TASK-108`, `TASK-508`) ou `sucedida` (`TASK-102`, `TASK-303`, `TASK-305`, `TASK-400`, `TASK-403`).
- Execução imediata de itens com `G-MIGRATION`, `G-WORKFLOW`, `G-HISTORY`, `G-SCOPE` ou `G-DEPLOY` sem aprovação explícita prévia do Vitor (PO).

## Notas e referências

- **Referências de contrato vigente da área (skill `dominio`):**
- `metodo/projeto/skills/dominio/referencias/task-history.md`
- `metodo/projeto/skills/dominio/referencias/audit-leadtime-history.md`
- `metodo/projeto/skills/dominio/referencias/time-tracking.md`
- **ADRs relacionadas:** `ADR-0005`, `ADR-0030`, `ADR-0034`, `ADR-0042`, `ADR-0049`, `ADR-0061`, `ADR-0103`
- **Origem:** migração sob demanda de `backlog.md` reconciliado pela auditoria do PR #29 (`origin/docs/backlog-audit-20261020:relatorios/oficina-backlog-audit.md`).
