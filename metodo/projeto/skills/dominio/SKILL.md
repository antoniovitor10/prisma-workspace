---
name: dominio
description: Regras de negócio vigentes, glossário ubíquo e mapa determinístico de domínio -> referência -> código -> testes do Prisma WorkSpace. Carregar ao escrever ou refinar HU, quebrar tasks, implementar ou revisar mudanças de domínio, aplicação, API, banco ou interface.
metadata:
  origem: projeto
  versao: "1"
  revisar_em: ""
---

# Skill de Projeto · Domínio do Prisma WorkSpace

## Quando carregar

- Ao escrever ou refinar uma História de Usuário (`escrever-hu`, `refinar-hu`).
- Ao quebrar uma HU em tasks (`quebrar-tasks`) para localizar contratos, código e testes da área.
- Ao implementar (`implementador`), testar (`testador`) ou revisar (`revisor-codigo`, `revisor-seguranca`) qualquer funcionalidade de produto.

## Contrato vigente e uso nas HUs

- Os **48 arquivos** em `metodo/projeto/skills/dominio/referencias/<arquivo>.md` são as especificações aprovadas herdadas do projeto e constituem o **contrato funcional e técnico vigente de cada área**.
- **Regra obrigatória:** toda História de Usuário nova (`HU-XXXX`) deve citar explicitamente na seção de referências/notas o(s) arquivo(s) `metodo/projeto/skills/dominio/referencias/<arquivo>.md` da área afetada (e as ADRs correspondentes em `docs/decisoes/`).
- Quando uma HU alterar comportamento já coberto por uma referência em `referencias/`, a implementação ou o fechamento da HU deve atualizar a referência correspondente para que o contrato da área nunca fique defasado em relação ao código.
- As 5 especificações antigas com status `superseded` (`multi-board-views.md`, `organization-switch-refresh.md`, `project-structure.md`, `projects-visual-refresh.md` e `work-items.md`) foram descartadas na migração por terem sido substituídas por contratos mais novos já presentes em `referencias/`.

## Glossário do domínio

| Termo no código (inglês) | Termo funcional (pt-BR) | Definição e papel no domínio |
|---|---|---|
| `Organization` | Organização | Fronteira raiz de isolamento multi-tenant (ADR-0017). Todo recurso de negócio pertence direta ou indiretamente a uma organização ativa (`X-Organization-Id`). |
| `OrganizationMembership` | Vínculo organizacional | Associação do usuário à organização com papel base (`OrgAdmin`, `ProjectManager`, `Collaborator`, `Viewer`), status e `DisplayName` funcional por organização (ADR-0046, ADR-0063). |
| `OrganizationInvitation` | Convite de organização | Convite auditado com token e expiração para ingresso de usuário em uma organização (`referencias/invitation-onboarding.md`). |
| `Team` / `TeamMember` | Equipe / Membro de equipe | Agrupamento organizacional de usuários com capacidade horária semanal e desativação lógica (ADR-0019). |
| `Project` | Projeto | Unidade de gestão dentro da organização, com chave única (`Key`, gerada automaticamente ou informada — ADR-0057), natureza de trabalho (`WorkNature` — ADR-0073), arquivamento reversível e configurações próprias (ADR-0020). |
| `ProjectRoleAssignment` / `ProjectPermissionGrant` | Papel e concessão no projeto | Autorização híbrida: papel no projeto somado a concessões explícitas por ação sensível (ADR-0018, ADR-0055). |
| `Board` | Quadro | Visão operacional opcional pertencente a um único `Project` (ADR-0083, ADR-0089). Um projeto pode ter zero, um ou vários quadros. |
| `Stage` | Coluna / Etapa do quadro | Coluna independente por `Board` (ADR-0089), com limite WIP (`WipLimit`), ordem (`Order`) e categoria explícita (`StageCategory`: aberta ou concluída — ADR-0082, sem inferência por nome). |
| `WorkItem` | Tarefa / Item de trabalho | Agregado único para demandas internas e externas (ADR-0021) e subtarefas via `ParentId` (ADR-0004). Nunca usar `Task` como nome de entidade para não colidir com `System.Threading.Tasks.Task`. |
| `WorkItemBoardPlacement` | Posição da tarefa no quadro | Vínculo explícito entre `WorkItem`, `Board` e `Stage` quando a tarefa participa de quadros com colunas independentes (ADR-0089). |
| `WorkItemDependency` | Dependência entre tarefas | Relação tipada (`Blocks`, `RelatesTo`, `Duplicates`) entre itens do mesmo projeto, com prevenção de ciclo e cálculo de bloqueio (ADR-0099). |
| `StageHistory` | Histórico de etapa (lead time) | Registro imutável gravado a cada mudança de etapa (`Stage`) de um `WorkItem`, base para cálculo de lead time e cycle time desde o dia 1 (ADR-0005). |
| `AuditLog` / `TaskHistory` | Auditoria / Linha do tempo | Registros imutáveis gravados na mesma transação da mutação de negócio, sem `Update`/`Delete` expostos (ADR-0034, ADR-0049, ADR-0061). |
| `Sprint` / `SprintItemSnapshot` | Sprint / Snapshot de sprint | Ciclo iterativo vinculado a um único `Project` (ADR-0084, revogando o N:N da ADR-0081), com estado calculado pelas datas (`Planned`, `Active`, `Completed`) e snapshot histórico de escopo. |
| `TimeEntry` | Apontamento de horas | Registro de esforço realizado (> 0h) em um `WorkItem`, por usuário e data, restrito a horas e capacidade (ADR-0030, sem conceitos de XP — ADR-0042). |
| `SlaPolicy` / `WorkItemSlaSnapshot` | Política e snapshot de SLA | Regras de prazo de resposta/resolução por projeto e prioridade, com pausa de relógio em etapas configuradas (ADR-0029). |
| `ExternalPortalConfig` / `ExternalRequest` | Portal externo / Solicitação | Canal público por projeto para abertura e acompanhamento por protocolo, separado das notas internas (`Comment` vs `ExternalMessage` — ADR-0026, ADR-0027, ADR-0028). |
| `Notification` | Notificação interna | Registro persistente por organização e destinatário, com fila durável de entrega in-app/e-mail e deduplicação (ADR-0032). |
| `SavedFilter` / `ProjectSavedQuery` | Filtro pessoal / Consulta salva | Filtros rápidos pessoais por quadro (ADR-0064) e consultas estruturadas do compositor visual por projeto (ADR-0071). |
| `AutomationRule` | Regra de automação | Regra declarativa no escopo do projeto com gatilho, condições e ação auditada e idempotente (ADR-0037). |
| `WikiPage` / `WikiPageVersion` | Página e versão de Wiki | Base de conhecimento em Markdown por projeto, com árvore hierárquica e versionamento imutável a cada edição (`referencias/wiki-knowledge.md`). |
| `AiProviderConnection` / `AiConversation` | Conexão e conversa de IA | Assistente de IA com modelo trazido pelo usuário (BYOM: API key ou CLI oficial por assinatura — ADR-0091, ADR-0094, ADR-0095). |
| `MethodGitIntegration` / `MethodDelivery` | Integração Git do método | Vínculo de projeto provisionado com GitHub App, tokens por papel de agente e webhooks assinados que movem cartões no quadro do método (ADR-0100, ADR-0101, ADR-0102). |

## Regras de negócio e invariantes vigentes

1. **Isolamento multi-tenant estrito (ADR-0017):** toda query e comando de negócio filtra por `OrganizationId` resolvido do contexto (`X-Organization-Id` validado contra `OrganizationMembership` ativo). Acesso cruzado entre organizações é proibido.
2. **Autorização híbrida e acesso para atribuição (ADR-0018, ADR-0055, ADR-0090):** permissões combinam perfil organizacional, papel no projeto e concessões explícitas. Um usuário só pode ser atribuído como responsável (`AssigneeId`) de um `WorkItem` se possuir acesso efetivo ao projeto.
3. **Nome de exibição funcional (ADR-0046, ADR-0063):** o cadastro exige nome completo; a UI prioriza `DisplayName` da organização sobre nome global, login ou e-mail.
4. **Projeto, metodologia e visões (ADR-0043, ADR-0056, ADR-0073, ADR-0099):** a escolha Kanban/Scrum fica oculta na criação do projeto (`project-methodology-hidden.md`); todas as visões do projeto (Lista, Quadro, Sprints, Gantt/Cronograma, Consultas, Wiki) coexistem. Story Points só aparecem quando habilitados no contexto Scrum.
5. **Quadro como visão opcional e colunas independentes (ADR-0062, ADR-0065, ADR-0082, ADR-0083, ADR-0089):**
   - Tarefas existem no projeto independentemente de haver quadro.
   - Cada `Board` possui suas próprias colunas (`Stage`) independentes; criar ou editar colunas em um quadro não altera outros quadros do projeto.
   - Toda coluna exige `StageCategory` explícita (aberta ou concluída), sem inferência silenciosa pelo nome (ADR-0082).
   - O status textual canônico da tarefa no contexto de um quadro é o nome da sua coluna atual; mover para coluna de categoria concluída preenche `CompletedAt` e reabrir limpa `CompletedAt` (ADR-0062, ADR-0065).
   - A ordem manual dos cartões dentro da coluna (`Position`) é compartilhada e persistida (ADR-0060).
6. **Sprints vinculadas a um único projeto (ADR-0084):** cada `Sprint` pertence a um único `Project` (`ProjectId` obrigatório) e só aceita tarefas desse projeto. O estado da sprint é calculado pelas datas de início e término (com fuso da organização), sem botões manuais de iniciar/encerrar.
7. **Dependências, subtarefas e exclusão lógica (ADR-0004, ADR-0021, ADR-0034, ADR-0099):**
   - Subtarefas usam `ParentId` no próprio `WorkItem`.
   - Dependências do tipo `Blocks` sinalizam impedimento enquanto a tarefa predecessora não estiver concluída; ciclos no grafo são rejeitados na validação.
   - Exclusão de entidades operacionais é lógica (`IsDeleted = true`), preservando histórico.
8. **Imutabilidade de auditoria e histórico (ADR-0005, ADR-0034, ADR-0049, ADR-0061):** `AuditLog`, `StageHistory` e eventos da Linha do Tempo (`TaskHistory`) são append-only na mesma transação da mudança; nenhum endpoint ou repositório pode expor edição ou remoção desses registros.
9. **Fronteira do Portal Externo (ADR-0026, ADR-0027, ADR-0028):** comentários internos (`Comment`) e anexos internos jamais vazam na consulta pública por protocolo; apenas `ExternalMessage` e campos públicos compõem a visão do solicitante externo.
10. **Setup inicial e seed de demonstração (ADR-0079, ADR-0086):** o setup inicial da Community Edition é de uso único protegido por `PRISMA_SETUP_TOKEN`; o seed de demonstração é idempotente e pula bancos já populados.

## Mapa de domínio -> referência -> código -> testes

Extraído de `context/index.yaml` e atualizado para apontar para as 48 referências aprovadas em `metodo/projeto/skills/dominio/referencias/`:

### 1. `auth` — Autenticação, Setup Inicial e Segurança
- **Referências:** `referencias/auth-security.md`, `referencias/installation-setup.md`, `referencias/open-source-distribution.md`, `referencias/test-report-2026-09-21-remediation.md`
- **Código:** `src/Prisma.Workspace.Domain/Entities/RefreshToken.cs`, `src/Prisma.Workspace.Application/Features/Auth/`, `src/Prisma.Workspace.Application/Features/Setup/`, `src/Prisma.Workspace.Infrastructure/Authentication/`, `src/Prisma.Workspace.Api/Controllers/AuthController.cs`, `src/Prisma.Workspace.Api/Controllers/SetupController.cs`, `src/Prisma.Workspace.Web/src/auth/`, `src/Prisma.Workspace.Web/src/pages/Login/`
- **Testes:** `tests/Prisma.Workspace.Tests/Auth/`, `src/Prisma.Workspace.Web/src/pages/Login/LoginPage.test.tsx`, `src/Prisma.Workspace.Web/e2e/tests/01-auth/`
- **Gates de atenção:** `G-MIGRATION` se alterar tabelas Identity ou `RefreshToken`.

### 2. `organizations` — Organizações, Multi-Tenancy e Convites
- **Referências:** `referencias/organizations.md`, `referencias/invitation-onboarding.md`, `referencias/top-navigation-shell.md`, `referencias/prisma-visual-system.md`
- **Código:** `src/Prisma.Workspace.Domain/Entities/Organization*.cs`, `src/Prisma.Workspace.Application/Features/Organizations/`, `src/Prisma.Workspace.Infrastructure/MultiTenancy/`, `src/Prisma.Workspace.Api/Controllers/OrganizationsController.cs`, `src/Prisma.Workspace.Api/Middleware/OrganizationContextMiddleware.cs`, `src/Prisma.Workspace.Web/src/pages/Organizations/`, `src/Prisma.Workspace.Web/src/pages/Invitations/`, `src/Prisma.Workspace.Web/src/layouts/`
- **Testes:** `tests/Prisma.Workspace.Tests/Organizations/`, `tests/Prisma.Workspace.Tests/MultiTenancy/`, `src/Prisma.Workspace.Web/src/pages/Organizations/OrganizationsPage.test.tsx`, `src/Prisma.Workspace.Web/e2e/tests/02-organizations/`

### 3. `permissions` — Controle de Acesso (RBAC), Concessões e Atribuição
- **Referências:** `referencias/user-access-permissions.md`, `referencias/project-assignment-access.md`, `referencias/responsible-display-name.md`
- **Código:** `src/Prisma.Workspace.Domain/Entities/ProjectRoleAssignment.cs`, `src/Prisma.Workspace.Domain/Entities/ProjectPermissionGrant.cs`, `src/Prisma.Workspace.Domain/Enums/OrganizationRole.cs`, `src/Prisma.Workspace.Domain/Enums/ProjectRole.cs`, `src/Prisma.Workspace.Domain/Enums/PermissionAction.cs`, `src/Prisma.Workspace.Application/Common/Security/`, `src/Prisma.Workspace.Application/Features/ProjectAccess/`, `src/Prisma.Workspace.Infrastructure/Authorization/`
- **Testes:** `tests/Prisma.Workspace.Tests/Authorization/`

### 4. `teams` — Equipes, Membros e Capacidade
- **Referências:** `referencias/teams.md`, `referencias/responsible-display-name.md`
- **Código:** `src/Prisma.Workspace.Domain/Entities/Team.cs`, `src/Prisma.Workspace.Domain/Entities/TeamMember.cs`, `src/Prisma.Workspace.Application/Features/Teams/`, `src/Prisma.Workspace.Api/Controllers/TeamsController.cs`, `src/Prisma.Workspace.Web/src/pages/Teams/`
- **Testes:** `tests/Prisma.Workspace.Tests/Teams/`, `src/Prisma.Workspace.Web/src/pages/Teams/TeamsPage.test.tsx`, `src/Prisma.Workspace.Web/e2e/tests/03-teams/`

### 5. `projects` — Gestão de Projetos, Chave e Natureza
- **Referências:** `referencias/project-management.md`, `referencias/project-key-auto-generation.md`, `referencias/project-methodology-hidden.md`, `referencias/work-nature.md`, `referencias/authenticated-home.md`
- **Código:** `src/Prisma.Workspace.Domain/Entities/Project*.cs`, `src/Prisma.Workspace.Application/Features/Projects/`, `src/Prisma.Workspace.Api/Controllers/ProjectsController.cs`, `src/Prisma.Workspace.Web/src/pages/Projects/`, `src/Prisma.Workspace.Web/src/pages/Home/`
- **Testes:** `tests/Prisma.Workspace.Tests/Projects/`, `src/Prisma.Workspace.Web/src/pages/Projects/ProjectsPage.test.tsx`, `src/Prisma.Workspace.Web/src/pages/Home/HomePage.test.tsx`, `src/Prisma.Workspace.Web/e2e/tests/04-projects/`

### 6. `boards_stages` — Quadros Kanban, Colunas Independentes e WIP
- **Referências:** `referencias/boards-stages-wip.md`, `referencias/board-as-view.md`, `referencias/independent-board-columns.md`, `referencias/kanban-visual-order.md`, `referencias/workflow-status.md`, `referencias/task-flow-ux.md`
- **Código:** `src/Prisma.Workspace.Domain/Entities/Board.cs`, `src/Prisma.Workspace.Domain/Entities/Stage.cs`, `src/Prisma.Workspace.Domain/Entities/WorkItemBoardPlacement.cs`, `src/Prisma.Workspace.Domain/Entities/WorkflowStatus.cs`, `src/Prisma.Workspace.Application/Features/Boards/`, `src/Prisma.Workspace.Application/Features/Stages/`, `src/Prisma.Workspace.Application/Features/WorkflowStatuses/`, `src/Prisma.Workspace.Api/Controllers/BoardsController.cs`, `src/Prisma.Workspace.Api/Controllers/StagesController.cs`, `src/Prisma.Workspace.Web/src/pages/Board/`, `src/Prisma.Workspace.Web/src/components/Kanban/`
- **Testes:** `tests/Prisma.Workspace.Tests/Boards/`, `tests/Prisma.Workspace.Tests/Stages/`, `tests/Prisma.Workspace.Tests/WorkflowStatuses/`, `src/Prisma.Workspace.Web/src/pages/Board/BoardPage.test.tsx`, `src/Prisma.Workspace.Web/e2e/tests/05-kanban/`
- **Gates de atenção:** `G-WORKFLOW` em mudanças de regras de transição/WIP; `G-MIGRATION` em schema.

### 7. `work_items` — Tarefas, Subtarefas, Dependências, Anexos e Meu Trabalho
- **Referências:** `referencias/work-item-management.md`, `referencias/quick-create-work-item.md`, `referencias/rich-task-description.md`, `referencias/dependencies.md`, `referencias/attachments.md`, `referencias/task-history.md`, `referencias/my-work-hub.md`
- **Código:** `src/Prisma.Workspace.Domain/Entities/WorkItem*.cs`, `src/Prisma.Workspace.Domain/Entities/Attachment.cs`, `src/Prisma.Workspace.Domain/Entities/Comment.cs`, `src/Prisma.Workspace.Application/Features/WorkItems/`, `src/Prisma.Workspace.Application/Features/Attachments/`, `src/Prisma.Workspace.Application/Features/Comments/`, `src/Prisma.Workspace.Application/Features/MyWork/`, `src/Prisma.Workspace.Api/Controllers/WorkItemsController.cs`, `src/Prisma.Workspace.Api/Controllers/MyWorkController.cs`, `src/Prisma.Workspace.Web/src/components/WorkItemDrawer/`, `src/Prisma.Workspace.Web/src/pages/MyWork/`
- **Testes:** `tests/Prisma.Workspace.Tests/WorkItems/`, `tests/Prisma.Workspace.Tests/Attachments/`, `tests/Prisma.Workspace.Tests/Comments/`, `tests/Prisma.Workspace.Tests/MyWork/`, `src/Prisma.Workspace.Web/src/pages/MyWork/MyWorkPage.test.tsx`, `src/Prisma.Workspace.Web/e2e/tests/06-work-items/`

### 8. `backlog_sprints` — Backlog e Sprints por Projeto
- **Referências:** `referencias/backlog.md`, `referencias/sprints.md`
- **Código:** `src/Prisma.Workspace.Domain/Entities/Sprint.cs`, `src/Prisma.Workspace.Domain/Entities/SprintItemSnapshot.cs`, `src/Prisma.Workspace.Application/Features/Backlog/`, `src/Prisma.Workspace.Application/Features/Sprints/`, `src/Prisma.Workspace.Api/Controllers/BacklogController.cs`, `src/Prisma.Workspace.Api/Controllers/SprintsController.cs`, `src/Prisma.Workspace.Web/src/pages/Backlog/`, `src/Prisma.Workspace.Web/src/pages/Sprints/`
- **Testes:** `tests/Prisma.Workspace.Tests/Backlog/`, `tests/Prisma.Workspace.Tests/Sprints/`, `src/Prisma.Workspace.Web/src/pages/Sprints/SprintsPage.test.tsx`, `src/Prisma.Workspace.Web/e2e/tests/07-sprints/`

### 9. `gantt` — Planejamento Gantt e Cronograma
- **Referências:** `referencias/gantt-planning.md`, `referencias/dependencies.md`
- **Código:** `src/Prisma.Workspace.Application/Features/Gantt/`, `src/Prisma.Workspace.Api/Controllers/GanttController.cs`, `src/Prisma.Workspace.Web/src/pages/Gantt/`, `src/Prisma.Workspace.Web/src/components/Gantt/`
- **Testes:** `tests/Prisma.Workspace.Tests/Gantt/`, `src/Prisma.Workspace.Web/src/pages/Gantt/GanttPage.test.tsx`, `src/Prisma.Workspace.Web/e2e/tests/09-gantt/`

### 10. `time_tracking` — Apontamento de Horas e Capacidade
- **Referências:** `referencias/time-tracking.md`
- **Código:** `src/Prisma.Workspace.Domain/Entities/TimeEntry.cs`, `src/Prisma.Workspace.Application/Features/TimeTracking/`, `src/Prisma.Workspace.Api/Controllers/TimeEntriesController.cs`, `src/Prisma.Workspace.Web/src/components/TimeTracking/`, `src/Prisma.Workspace.Web/src/pages/Timesheet/`
- **Testes:** `tests/Prisma.Workspace.Tests/TimeTracking/`, `src/Prisma.Workspace.Web/src/pages/Timesheet/TimesheetPage.test.tsx`, `src/Prisma.Workspace.Web/e2e/tests/08-time-tracking/`

### 11. `audit_history` — Auditoria Transversal e StageHistory (Lead Time)
- **Referências:** `referencias/audit-leadtime-history.md`, `referencias/task-history.md`
- **Código:** `src/Prisma.Workspace.Domain/Entities/AuditLog.cs`, `src/Prisma.Workspace.Domain/Entities/StageHistory.cs`, `src/Prisma.Workspace.Application/Features/Audit/`, `src/Prisma.Workspace.Infrastructure/Auditing/`, `src/Prisma.Workspace.Api/Controllers/AuditController.cs`, `src/Prisma.Workspace.Web/src/pages/Audit/`
- **Testes:** `tests/Prisma.Workspace.Tests/Audit/`, `tests/Prisma.Workspace.Tests/StageHistory/`
- **Gates de atenção:** `G-HISTORY` obrigatório se alterar estrutura imutável de auditoria ou histórico.

### 12. `sla_approvals` — Políticas de SLA e Aprovações
- **Referências:** `referencias/sla-approvals.md`
- **Código:** `src/Prisma.Workspace.Domain/Entities/SlaPolicy.cs`, `src/Prisma.Workspace.Domain/Entities/WorkItemSlaSnapshot.cs`, `src/Prisma.Workspace.Domain/Entities/WorkItemApproval.cs`, `src/Prisma.Workspace.Application/Features/Sla/`, `src/Prisma.Workspace.Application/Features/Approvals/`, `src/Prisma.Workspace.Api/Controllers/SlaController.cs`, `src/Prisma.Workspace.Web/src/components/Sla/`
- **Testes:** `tests/Prisma.Workspace.Tests/Sla/`, `tests/Prisma.Workspace.Tests/Approvals/`

### 13. `external_portal` — Portal Externo e Solicitações por Protocolo
- **Referências:** `referencias/external-portal.md`
- **Código:** `src/Prisma.Workspace.Domain/Entities/ExternalPortalConfig.cs`, `src/Prisma.Workspace.Domain/Entities/ExternalRequest.cs`, `src/Prisma.Workspace.Domain/Entities/ExternalMessage.cs`, `src/Prisma.Workspace.Application/Features/ExternalPortal/`, `src/Prisma.Workspace.Api/Controllers/ExternalPortalController.cs`, `src/Prisma.Workspace.Web/src/pages/Portal/`
- **Testes:** `tests/Prisma.Workspace.Tests/ExternalPortal/`, `src/Prisma.Workspace.Web/src/pages/Portal/PortalPage.test.tsx`, `src/Prisma.Workspace.Web/e2e/tests/11-portal/`

### 14. `dashboards_reports` — Dashboards Analíticos e Construtor de Relatórios
- **Referências:** `referencias/dashboards-reports.md`
- **Código:** `src/Prisma.Workspace.Domain/Entities/SavedReport.cs`, `src/Prisma.Workspace.Application/Features/Dashboards/`, `src/Prisma.Workspace.Application/Features/Reports/`, `src/Prisma.Workspace.Api/Controllers/DashboardsController.cs`, `src/Prisma.Workspace.Api/Controllers/ReportsController.cs`, `src/Prisma.Workspace.Web/src/pages/Dashboard/`, `src/Prisma.Workspace.Web/src/pages/Reports/`
- **Testes:** `tests/Prisma.Workspace.Tests/Dashboards/`, `tests/Prisma.Workspace.Tests/Reports/`, `src/Prisma.Workspace.Web/src/pages/Dashboard/DashboardPage.test.tsx`, `src/Prisma.Workspace.Web/src/pages/Reports/ReportsPage.test.tsx`, `src/Prisma.Workspace.Web/e2e/tests/10-dashboard/`

### 15. `notifications_realtime` — Notificações Persistentes e SignalR
- **Referências:** `referencias/notifications-realtime.md`
- **Código:** `src/Prisma.Workspace.Domain/Entities/Notification.cs`, `src/Prisma.Workspace.Domain/Entities/NotificationPreference.cs`, `src/Prisma.Workspace.Application/Features/Notifications/`, `src/Prisma.Workspace.Infrastructure/Realtime/`, `src/Prisma.Workspace.Api/Controllers/NotificationsController.cs`, `src/Prisma.Workspace.Api/Hubs/WorkspaceHub.cs`, `src/Prisma.Workspace.Web/src/components/Notifications/`, `src/Prisma.Workspace.Web/src/realtime/`
- **Testes:** `tests/Prisma.Workspace.Tests/Notifications/`, `src/Prisma.Workspace.Web/src/components/Notifications/NotificationBell.test.tsx`

### 16. `search_filters_queries` — Busca Global, Filtros Pessoais e Consultas de Projeto
- **Referências:** `referencias/search-saved-filters.md`, `referencias/project-work-item-queries.md`
- **Código:** `src/Prisma.Workspace.Domain/Entities/SavedFilter.cs`, `src/Prisma.Workspace.Domain/Entities/ProjectSavedQuery.cs`, `src/Prisma.Workspace.Application/Features/Search/`, `src/Prisma.Workspace.Application/Features/SavedFilters/`, `src/Prisma.Workspace.Application/Features/ProjectQueries/`, `src/Prisma.Workspace.Api/Controllers/SearchController.cs`, `src/Prisma.Workspace.Api/Controllers/SavedFiltersController.cs`, `src/Prisma.Workspace.Api/Controllers/ProjectQueriesController.cs`, `src/Prisma.Workspace.Web/src/components/Search/`, `src/Prisma.Workspace.Web/src/pages/ProjectQueries/`
- **Testes:** `tests/Prisma.Workspace.Tests/Search/`, `tests/Prisma.Workspace.Tests/SavedFilters/`, `tests/Prisma.Workspace.Tests/ProjectQueries/`, `src/Prisma.Workspace.Web/src/pages/ProjectQueries/ProjectQueriesPage.test.tsx`

### 17. `bulk_automations` — Ações em Massa e Regras de Automação
- **Referências:** `referencias/bulk-actions-automations.md`
- **Código:** `src/Prisma.Workspace.Domain/Entities/AutomationRule.cs`, `src/Prisma.Workspace.Domain/Entities/AutomationExecutionLog.cs`, `src/Prisma.Workspace.Application/Features/BulkActions/`, `src/Prisma.Workspace.Application/Features/Automations/`, `src/Prisma.Workspace.Api/Controllers/BulkActionsController.cs`, `src/Prisma.Workspace.Api/Controllers/AutomationsController.cs`, `src/Prisma.Workspace.Web/src/components/BulkActions/`, `src/Prisma.Workspace.Web/src/pages/Automations/`
- **Testes:** `tests/Prisma.Workspace.Tests/BulkActions/`, `tests/Prisma.Workspace.Tests/Automations/`, `src/Prisma.Workspace.Web/src/pages/Automations/AutomationsPage.test.tsx`, `src/Prisma.Workspace.Web/e2e/tests/12-bulk-automations/`

### 18. `wiki` — Base de Conhecimento (Wiki) por Projeto
- **Referências:** `referencias/wiki-knowledge.md`
- **Código:** `src/Prisma.Workspace.Domain/Entities/WikiPage.cs`, `src/Prisma.Workspace.Domain/Entities/WikiPageVersion.cs`, `src/Prisma.Workspace.Application/Features/Wiki/`, `src/Prisma.Workspace.Api/Controllers/WikiController.cs`, `src/Prisma.Workspace.Web/src/pages/Wiki/`
- **Testes:** `tests/Prisma.Workspace.Tests/Wiki/`, `src/Prisma.Workspace.Web/src/pages/Wiki/WikiPage.test.tsx`

### 19. `ai_assistant` — Assistente de IA no Prisma WorkSpace (BYOM)
- **Referências:** `referencias/ai-assistant-foundation.md`, `referencias/ai-connection-onboarding.md`, `referencias/ai-conversation-experience.md`, `referencias/ai-gemini-subscription.md`
- **Código:** `src/Prisma.Workspace.Domain/Entities/Ai*.cs`, `src/Prisma.Workspace.Application/Features/Ai/`, `src/Prisma.Workspace.Infrastructure/Ai/`, `src/Prisma.Workspace.Api/Controllers/Ai*.cs`, `src/Prisma.Workspace.Web/src/components/AiAssistant/`, `src/Prisma.Workspace.Web/src/pages/AiSettings/`, `tools/ai-cli-bridge/`
- **Testes:** `tests/Prisma.Workspace.Tests/Ai/`, `src/Prisma.Workspace.Web/src/components/AiAssistant/AiAssistantDrawer.test.tsx`, `src/Prisma.Workspace.Web/src/pages/AiSettings/AiSettingsPage.test.tsx`, `src/Prisma.Workspace.Web/e2e/tests/13-ai-assistant/`

### 20. `method_git` — Integração Git do Método (MCP + GitHub App + Webhooks)
- **Referências:** `referencias/metodo-git-integration.md`
- **Código:** `src/Prisma.Workspace.Domain/Entities/Method*.cs`, `src/Prisma.Workspace.Application/Features/MethodGit/`, `src/Prisma.Workspace.Infrastructure/MethodGit/`, `src/Prisma.Workspace.Api/Controllers/Method*.cs`, `tools/prisma-mcp/`
- **Testes:** `tests/Prisma.Workspace.Tests/MethodGit/`, `tools/prisma-mcp/test/`

## Exemplos certos vs errados

```csharp
// CERTO: filtrar por OrganizationId ativo e exigir StageCategory explicita
var stage = new Stage(board.Id, request.Name, request.Order, request.WipLimit, request.Category);
```

```csharp
// ERRADO: inferir categoria de coluna pelo nome ("Concluído", "Done") ou ignorar OrganizationId
var isDone = request.Name.Contains("Conclu", StringComparison.OrdinalIgnoreCase); // viola ADR-0082
```

## Checklist antes de concluir task

- [ ] A referência da área em `metodo/projeto/skills/dominio/referencias/` e as ADRs aplicáveis foram lidas e respeitadas.
- [ ] Isolamento multi-tenant (`OrganizationId`) e permissões de projeto (`ProjectPermissionGrant` / `ProjectRoleAssignment`) estão cobertos.
- [ ] Mutação relevante gera `AuditLog` / `StageHistory` / `TaskHistory` de forma imutável na mesma transação.
- [ ] Se a HU alterou regra funcional de uma área, o arquivo correspondente em `referencias/` foi atualizado.
