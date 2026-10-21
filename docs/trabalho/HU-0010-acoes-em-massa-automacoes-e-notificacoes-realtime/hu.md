---
id: HU-0010
titulo: "Ações em massa, automações de projeto e notificações em tempo real (pendências do backlog)"
trilha: funcionalidade
status: aberta
prd: nao
criado_em: 2026-10-21
atualizado_em: 2026-10-21
---

# HU-0010 · Ações em massa, automações de projeto e notificações em tempo real (pendências do backlog)

## História

**Como** usuário operacional e gestor de projeto no Prisma WorkSpace
**Quero** concluir a barra de ações em massa, o motor de regras de automação por projeto e a homologação das notificações persistentes e eventos SignalR
**Para que** operações repetitivas e alertas em tempo real funcionem com atomicidade, auditoria e isolamento multi-tenant.

## Critérios de aceite

### CA-01 · Seleção múltipla, ações em massa auditadas e regras de automação (TASK-401, TASK-402)
- **Dado que** múltiplas tarefas selecionadas em um quadro/lista ou uma regra de automação ativa no projeto
- **Quando** uma operação em lote ou gatilho de automação for disparado
- **Então** as mutações respeitam permissões e limites do projeto, geram registros de auditoria e evitam loops infinitos conforme bulk-actions-automations.md.

### CA-02 · Homologação de notificações in-app e eventos SignalR (TASK-310)
- **Dado que** eventos de atribuição, menção, mudança de etapa ou comentário em um projeto
- **Quando** a entrega de notificações e a sincronização em tempo real do quadro forem homologadas
- **Então** o sino de notificações, a marcação de leitura e a atualização via SignalR atendem todos os critérios de notifications-realtime.md.

## Pendências herdadas do `backlog.md` (auditoria do PR #29)

Esta HU agrupa as pendências vivas (`parcial`, `não feita` e `blocked`) da área, migradas sem tasks filhas pré-criadas conforme `ADR-0103`. Ao puxar esta HU para refinamento/execução, cada pendência elegível será quebrada em tasks (`T-XXXX`) respeitando os gates humanos listados abaixo:

| ID original | Estado no backlog / auditoria PR #29 | Requisito original | O que falta / evidência da auditoria | Gate humano pendente | Referência da área |
|---|---|---|---|---|---|
| `TASK-401` | pending (auditoria PR #29: **não feita**) | Ocultar o editor de automacoes, preservando execucao, validacao e auditoria internas. | [Kanban.tsx:1267](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/src/pages/Kanban.tsx#L1267) ainda monta AutomationManager. — D99 confirma ocultação; C5 autorizado, preservar execução/validação internas. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/bulk-actions-automations.md` |
| `TASK-402` | pending (auditoria PR #29: **não feita**) | Definir o comportamento hierarquico das acoes em massa entre tarefa e subtarefa. | [ProductivityFeature.cs:457](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Application/Features/Productivity/ProductivityFeature.cs#L457) usa IDs selecionados; não fecha hierarquia recursiva. — Depende de TASK-121 e definição compatível com workflow/hierarquia; não inventar regra coletiva. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/bulk-actions-automations.md` |
| `TASK-310` | pending (auditoria PR #29: **parcial**) | Homologar notificacoes, preferencias por evento/canal e deduplicacao de lembretes. | [PlatformCrossCuttingTests.cs:17](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/tests/Prisma.Workspace.Tests/PlatformCrossCuttingTests.cs#L17) cobre publisher/preferências/deduplicação; presença técnica não comprova homologação da task. — Sem novo código funcional obrigatório identificado; homologação oficial não comprovada. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/notifications-realtime.md` |

## Fora de escopo

- Tarefas já concluídas em `backlog.md` (`completed`, `done`, `implemented`, `cancelled`) ou descartadas na auditoria do PR #29 como `feita` (`TASK-100`, `TASK-101`, `TASK-104`, `TASK-108`, `TASK-508`) ou `sucedida` (`TASK-102`, `TASK-303`, `TASK-305`, `TASK-400`, `TASK-403`).
- Execução imediata de itens com `G-MIGRATION`, `G-WORKFLOW`, `G-HISTORY`, `G-SCOPE` ou `G-DEPLOY` sem aprovação explícita prévia do Vitor (PO).

## Notas e referências

- **Referências de contrato vigente da área (skill `dominio`):**
- `metodo/projeto/skills/dominio/referencias/bulk-actions-automations.md`
- `metodo/projeto/skills/dominio/referencias/notifications-realtime.md`
- **ADRs relacionadas:** `ADR-0024`, `ADR-0032`, `ADR-0037`, `ADR-0103`
- **Origem:** migração sob demanda de `backlog.md` reconciliado pela auditoria do PR #29 (`origin/docs/backlog-audit-20261020:relatorios/oficina-backlog-audit.md`).
