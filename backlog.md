# Backlog Implementável - Prisma WorkSpace

Este arquivo contém o backlog estruturado de tarefas derivadas das especificações técnicas do projeto.

---

## Tasks por prioridade

### P0 - Crítico / Desbloqueia outras tarefas

```yaml
- id: TASK-001
  spec: SPEC-PROJECT-STRUCTURE (specs/project-structure.md)
  requirement: Validar os valores existentes de ProjectMethodology na criação e atualização e manter os dropdowns restritos às quatro opções da D20; XP permanece oculto e fora do escopo por enquanto.
  domain: project
  type: feature
  risk: médio
  dependencies: []
  affected_areas:
    - src/Prisma.Workspace.Application/Features/Projects/ProjectsFeature.cs
    - src/Prisma.Workspace.Application/Features/Projects/ProjectManagementFeature.cs
    - src/Prisma.Workspace.Web/src/pages/Projects.tsx
    - src/Prisma.Workspace.Web/src/pages/ProjectSettings.tsx
  gates:
    - backend-build
    - backend-test
    - frontend-build
    - frontend-test
  human_gate: não
  status: completed
  priority: P0

- id: TASK-002
  spec: SPEC-PROJECT-STRUCTURE (specs/project-structure.md)
  requirement: Renomear visualmente os rótulos de interface de "Metodologia" para "Estrutura de Trabalho" nas telas de projetos e configurações.
  domain: project
  type: ui
  risk: baixo
  dependencies: []
  affected_areas:
    - src/Prisma.Workspace.Web/src/pages/Projects.tsx
    - src/Prisma.Workspace.Web/src/pages/ProjectSettings.tsx
  gates:
    - frontend-build
    - frontend-test
  human_gate: não
  status: completed
  priority: P0

- id: TASK-003
  spec: SPEC-PROJECT-STRUCTURE (specs/project-structure.md)
  requirement: Garantir que a inicialização de projetos com estrutura Kanban não crie Sprints automaticamente.
  domain: project
  type: feature
  risk: baixo
  dependencies: []
  affected_areas:
    - src/Prisma.Workspace.Application/Features/Projects/ProjectsFeature.cs
    - tests/Prisma.Workspace.Tests/ProjectFeatureTests.cs
  gates:
    - backend-build
    - backend-test
  human_gate: não
  status: completed
  priority: P0
```

---

### P1 - Alto impacto / Baixo risco

```yaml
- id: TASK-004
  spec: SPEC-WORK-ITEMS (specs/work-items.md)
  requirement: Ocultar exibição, edição e agregação visual de Story Points em todas as superfícies de projetos Kanban, preservando o campo backend somente por retrocompatibilidade.
  domain: work-item
  type: ui
  risk: baixo
  dependencies: []
  affected_areas:
    - src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx
    - src/Prisma.Workspace.Web/src/pages/Kanban.tsx
    - src/Prisma.Workspace.Web/src/features/scrum/BacklogPlanner.tsx
    - src/Prisma.Workspace.Web/src/features/scrum/SprintBacklogPanel.tsx
    - src/Prisma.Workspace.Web/src/features/scrum/SprintDashboard.tsx
    - src/Prisma.Workspace.Web/src/features/scrum/SprintHistoryPanel.tsx
    - src/Prisma.Workspace.Web/src/features/task/TaskTaxonomyPanel.tsx
  gates:
    - frontend-build
    - frontend-test
  human_gate: não
  status: completed
  priority: P1

- id: TASK-005
  spec: SPEC-B-001 (specs/backlog.md)
  requirement: Implementar exibição hierárquica com expansão e colapso no Product Backlog; respeitar a D36, salvo decisão sucessora aprovada para permitir N níveis.
  domain: backlog
  type: feature
  risk: médio
  dependencies: []
  affected_areas:
    - src/Prisma.Workspace.Application/Features/Backlog/BacklogFeature.cs
    - src/Prisma.Workspace.Web/src/features/scrum/BacklogPlanner.tsx
  gates:
    - backend-build
    - backend-test
    - frontend-build
    - frontend-test
  human_gate: sim se N níveis forem mantidos (G-SCOPE para suceder a D36)
  status: completed
  priority: P1

- id: TASK-006
  spec: SPEC-F-009 (specs/dependencies.md)
  requirement: Criar endpoint HTTP GET para busca/autocomplete de tarefas por código legível (ex: DET-42) ou título e integrar à UI de dependências.
  domain: dependency
  type: feature
  risk: médio
  dependencies: []
  affected_areas:
    - src/Prisma.Workspace.Api/Controllers/WorkItemsController.cs
    - src/Prisma.Workspace.Application/Features/WorkItems/Queries/SearchWorkItemsQueryHandler.cs
    - src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx
  gates:
    - backend-build
    - backend-test
    - frontend-build
    - frontend-test
  human_gate: não
  status: completed
  priority: P1

- id: TASK-007
  spec: SPEC-F-009 (specs/dependencies.md)
  requirement: Implementar algoritmo de busca em grafo (DFS) no backend para detectar e prevenir dependências cíclicas em vínculos DependsOn e Blocks.
  domain: dependency
  type: feature
  risk: médio
  dependencies: []
  affected_areas:
    - src/Prisma.Workspace.Application/Features/WorkItems/WorkItemManagementFeature.cs
    - src/Prisma.Workspace.Domain/Services/DependencyGraphService.cs
  gates:
    - backend-build
    - backend-test
  human_gate: não
  status: completed
  priority: P1

- id: TASK-017
  spec: SPEC-RESPONSIBLE-DISPLAY-NAME (specs/responsible-display-name.md)
  requirement: Persistir e exibir o nome funcional do membro da organização para responsáveis, participantes e atores, sem priorizar e-mail na interface.
  domain: identity
  type: feature
  risk: médio
  dependencies: []
  affected_areas:
    - src/Prisma.Workspace.Infrastructure/Identity/UserDirectory.cs
    - src/Prisma.Workspace.Infrastructure/Persistence/Migrations
    - src/Prisma.Workspace.Web/src/pages/OrganizationSettings.tsx
    - src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx
    - src/Prisma.Workspace.Web/src/pages/Kanban.tsx
  gates:
    - backend-build
    - backend-test
    - frontend-build
    - frontend-test
  human_gate: sim (G-MIGRATION aprovado para DisplayName em OrganizationMember)
  status: completed
  priority: P1

- id: TASK-018
  spec: SPEC-KANBAN-VISUAL-ORDER (specs/kanban-visual-order.md)
  requirement: Preservar colunas livres, inserir novas tarefas no topo e permitir ordem manual compartilhada, isolando a reordenação pessoal e temporária de visões filtradas.
  domain: work-item
  type: ui
  risk: baixo
  dependencies: []
  affected_areas:
    - migracao/build-sql.cjs
    - src/Prisma.Workspace.Web/src/pages/Kanban.tsx
    - src/Prisma.Workspace.Web/src/features/board/kanbanOrdering.ts
  gates:
    - frontend-build
    - frontend-test
    - frontend-e2e
  human_gate: não
  status: pending
  priority: P1

- id: TASK-019
  spec: SPEC-ORGANIZATION-SWITCH-REFRESH (specs/organization-switch-refresh.md)
  requirement: Histórico da troca de tenant agora restrita ao Administrador da plataforma; contrato incorporado pela SPEC-ORGANIZATIONS e complementado pela TASK-034.
  domain: organizations
  type: ui
  risk: baixo
  dependencies: []
  affected_areas:
    - src/Prisma.Workspace.Web/src/pages/Projects.tsx
    - src/Prisma.Workspace.Web/src/pages/Projects.test.tsx
    - src/Prisma.Workspace.Web/e2e/organization-switch.spec.ts
  gates:
    - frontend-build
    - frontend-test
    - frontend-e2e
  human_gate: não
  status: completed
  priority: P1

- id: TASK-020
  spec: SPEC-QUICK-CREATE-WORK-ITEM (specs/quick-create-work-item.md)
  requirement: Histórico implementado do modelo N:N sucedido pela D52; a adequação para posição singular e coluna obrigatória explícita permanece no gap D52 a decompor.
  domain: work-item
  type: feature
  risk: médio
  dependencies:
    - TASK-029
  affected_areas:
    - src/Prisma.Workspace.Web/src/components/GlobalActions.tsx
    - src/Prisma.Workspace.Application/Features/WorkItems/Commands/CreateWorkItemCommandHandler.cs
    - src/Prisma.Workspace.Api/Controllers/WorkItemsController.cs
  gates:
    - backend-build
    - backend-test
    - frontend-build
    - frontend-test
    - frontend-e2e
  human_gate: sim (G-SPEC da SPEC-QUICK-CREATE-WORK-ITEM)
  status: completed
  priority: P1

- id: TASK-021
  spec: SPEC-B-001 (specs/backlog.md)
  requirement: Implementar lixeira de 7 dias separada de Arquivar, opções para excluir pai com/sem subtarefas, reparenting à avó ou independência, restauração familiar e filtro/restauração de arquivadas.
  domain: backlog
  type: feature
  risk: médio
  dependencies: []
  affected_areas:
    - src/Prisma.Workspace.Web/src/features/scrum/BacklogPlanner.tsx
    - src/Prisma.Workspace.Application/Features/WorkItems/WorkItemManagementFeature.cs
    - src/Prisma.Workspace.Api/Controllers/WorkItemsController.cs
  gates:
    - backend-build
    - backend-test
    - frontend-build
    - frontend-test
    - frontend-e2e
  human_gate: sim (G-SPEC da revisão da SPEC-B-001)
  status: pending
  priority: P1

- id: TASK-022
  spec: SPEC-S-003 (specs/sprints.md)
  requirement: Aplicar fechamento hierárquico no planejamento da sprint e, quando nenhuma sprint existir, permitir criar a primeira preservando a seleção atual.
  domain: sprint
  type: feature
  risk: médio
  dependencies: []
  affected_areas:
    - src/Prisma.Workspace.Web/src/features/scrum/BacklogPlanner.tsx
    - src/Prisma.Workspace.Web/src/features/scrum/SprintBacklogPanel.tsx
    - src/Prisma.Workspace.Application/Features/Backlog/BacklogFeature.cs
  gates:
    - backend-build
    - backend-test
    - frontend-build
    - frontend-test
    - frontend-e2e
  human_gate: sim (G-SPEC da revisão da SPEC-S-003)
  status: pending
  priority: P1

- id: TASK-023
  spec: SPEC-S-003 (specs/sprints.md)
  requirement: Expor edição/exclusão segura de sprint, removendo somente SprintId e preservando quadro/coluna/tarefa conforme D54; recuperar conflitos 409 e remover iniciação manual após G-SCOPE/G-WORKFLOW.
  domain: sprint
  type: feature
  risk: médio
  dependencies: []
  affected_areas:
    - src/Prisma.Workspace.Web/src/features/scrum/SprintDashboard.tsx
    - src/Prisma.Workspace.Web/src/features/scrum/SprintBacklogPanel.tsx
    - src/Prisma.Workspace.Application/Features/Sprints/SprintsFeature.cs
    - src/Prisma.Workspace.Api/Controllers/SprintsController.cs
  gates:
    - backend-build
    - backend-test
    - frontend-build
    - frontend-test
    - frontend-e2e
  human_gate: sim (G-SPEC da revisão da SPEC-S-003 + G-SCOPE/G-WORKFLOW para ativação automática)
  status: pending
  priority: P1

- id: TASK-024
  spec: SPEC-PROJECT-KEY-AUTO-GENERATION (specs/project-key-auto-generation.md)
  requirement: Gerar a chave técnica exclusivamente no backend, sem entrada, edição ou exibição nas telas normais; preservar o valor interno somente para banco, unicidade, importação, busca técnica, compatibilidade e integrações necessárias.
  domain: project
  type: ui
  risk: baixo
  dependencies: []
  affected_areas:
    - src/Prisma.Workspace.Web/src/pages/Projects.tsx
    - src/Prisma.Workspace.Application/Features/Projects/ProjectsFeature.cs
  gates:
    - backend-test
    - frontend-build
    - frontend-test
    - frontend-e2e
  human_gate: não (decisão funcional aprovada; implementação ainda pendente)
  status: pending
  priority: P1

- id: TASK-025
  spec: SPEC-B-001 (specs/backlog.md)
  requirement: Criar navegação contextual Backlog ↔ Quadro e breadcrumbs clicáveis, preservando projeto, BoardId, item em foco, filtros e histórico do navegador.
  domain: backlog
  type: ui
  risk: baixo
  dependencies:
    - TASK-032
  affected_areas:
    - src/Prisma.Workspace.Web/src/pages/ProjectWorkspace.tsx
    - src/Prisma.Workspace.Web/src/features/scrum/BacklogPlanner.tsx
    - src/Prisma.Workspace.Web/src/pages/Kanban.tsx
    - src/Prisma.Workspace.Web/src/App.tsx
  gates:
    - frontend-build
    - frontend-test
    - frontend-e2e
  human_gate: sim (G-SPEC da revisão da SPEC-B-001)
  status: completed
  priority: P1

- id: TASK-026
  spec: SPEC-BOARDS-STAGES-WIP (specs/boards-stages-wip.md)
  requirement: Histórico implementado do quadro por projeto com Backlog padrão, sucedido pela D52 e pelo gap futuro correspondente.
  domain: board
  type: feature
  risk: médio
  dependencies: []
  affected_areas:
    - src/Prisma.Workspace.Web/src/pages/Kanban.tsx
    - src/Prisma.Workspace.Web/src/pages/ProjectWorkspace.tsx
    - src/Prisma.Workspace.Application/Features/Boards
    - src/Prisma.Workspace.Api/Controllers/BoardsController.cs
  gates:
    - backend-build
    - backend-test
    - frontend-build
    - frontend-test
    - frontend-e2e
  human_gate: sim (G-SPEC da SPEC-BOARDS-STAGES-WIP + G-WORKFLOW para a coluna padrão)
  status: completed
  priority: P1

- id: TASK-027
  spec: SPEC-BOARDS-STAGES-WIP (specs/boards-stages-wip.md)
  requirement: Permitir reordenar colunas por arraste e teclado, persistindo Stage.Position com atualização otimista e rollback.
  domain: board
  type: feature
  risk: médio
  dependencies: []
  affected_areas:
    - src/Prisma.Workspace.Web/src/pages/Kanban.tsx
    - src/Prisma.Workspace.Application/Features/Stages
    - src/Prisma.Workspace.Api/Controllers/StagesController.cs
  gates:
    - backend-build
    - backend-test
    - frontend-build
    - frontend-test
    - frontend-e2e
  human_gate: sim (G-SPEC da SPEC-BOARDS-STAGES-WIP + G-WORKFLOW)
  status: in_progress
  audit_2026_09_08: >-
    Confirmado in_progress. A reordenacao persistida ja existe por botoes com
    atualizacao otimista e rollback (Kanban.tsx:761-778, services/api.ts:765).
    Falta somente o arraste. Escopo remanescente coberto por TASK-014.
  priority: P1

- id: TASK-028
  spec: SPEC-BOARDS-STAGES-WIP (specs/boards-stages-wip.md)
  requirement: Histórico implementado de exclusão/realocação N:N, sucedido pelo arquivamento de quadro e tarefas definido na D52.
  domain: board
  type: feature
  risk: alto
  dependencies:
    - TASK-029
  affected_areas:
    - src/Prisma.Workspace.Web/src/pages/Kanban.tsx
    - src/Prisma.Workspace.Application/Features/Boards
    - src/Prisma.Workspace.Infrastructure/Persistence
    - src/Prisma.Workspace.Api/Controllers/BoardsController.cs
  gates:
    - backend-build
    - backend-test
    - frontend-build
    - frontend-test
    - frontend-e2e
  human_gate: sim (G-SPEC + G-MIGRATION/G-WORKFLOW herdados da TASK-029)
  status: completed
  priority: P1

- id: TASK-029
  spec: SPEC-MULTI-BOARD-VIEWS (specs/multi-board-views.md)
  requirement: Histórico da arquitetura N:N implantada e agora superseded; preservado para rastrear a futura conversão segura definida na D52.
  domain: board
  type: architecture
  risk: alto
  dependencies: []
  affected_areas:
    - src/Prisma.Workspace.Domain/Entities/WorkItem.cs
    - src/Prisma.Workspace.Domain/Entities/Board.cs
    - src/Prisma.Workspace.Infrastructure/Persistence
    - src/Prisma.Workspace.Infrastructure/Migrations
    - src/Prisma.Workspace.Application/Features/WorkItems
    - src/Prisma.Workspace.Web/src/pages/Kanban.tsx
    - src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx
  gates:
    - backend-build
    - backend-test
    - frontend-build
    - frontend-test
    - frontend-e2e
  human_gate: sim (G-SPEC + G-SCOPE + G-MIGRATION + G-WORKFLOW)
  status: completed
  priority: P1

- id: TASK-030
  spec: SPEC-TASK-HISTORY (specs/task-history.md)
  requirement: Substituir a gaveta lateral por modal centralizado, amplo, responsivo e acessível, preservando deep link, contexto de origem e as seis abas atualizadas pela D61.
  domain: work-item
  type: ui
  risk: médio
  dependencies: []
  affected_areas:
    - src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx
    - src/Prisma.Workspace.Web/src/pages/Kanban.tsx
    - src/Prisma.Workspace.Web/src/features/scrum/BacklogPlanner.tsx
    - src/Prisma.Workspace.Web/src/features/scrum/SprintBacklogPanel.tsx
  gates:
    - frontend-build
    - frontend-test
    - frontend-e2e
  human_gate: sim (G-SPEC da revisão da SPEC-TASK-HISTORY)
  status: completed
  priority: P1

- id: TASK-031
  spec: SPEC-SEARCH-SAVED-FILTERS (specs/search-saved-filters.md)
  requirement: Implementar painel lateral contextual com Aplicar/Salvar/Limpar e filtros exclusivamente pessoais de quadro ou globais; revalidar globais por quadro e sinalizar critérios ignorados, preservando ordem filtrada temporária e Product Backlog inicial Sem sprint.
  domain: search
  type: ui
  risk: médio
  dependencies:
    - TASK-025
    - TASK-032
  affected_areas:
    - src/Prisma.Workspace.Web/src/features/board/KanbanFilterBar.tsx
    - src/Prisma.Workspace.Web/src/features/board/KanbanFilters.ts
    - src/Prisma.Workspace.Web/src/features/scrum/BacklogFilters.ts
    - src/Prisma.Workspace.Web/src/pages/Kanban.tsx
  gates:
    - frontend-build
    - frontend-test
    - frontend-e2e
  human_gate: sim (G-SPEC da SPEC-SEARCH-SAVED-FILTERS)
  status: in_progress
  priority: P1

- id: TASK-032
  spec: SPEC-TOP-NAVIGATION-SHELL (specs/top-navigation-shell.md)
  requirement: Manter shell sem sidebar global, abrir no Kanban, incluir seletor superior do último quadro e estado Nenhum quadro selecionado com ações autorizadas.
  domain: project
  type: ui
  risk: alto
  dependencies: []
  affected_areas:
    - src/Prisma.Workspace.Web/src/layout/AppShell.tsx
    - src/Prisma.Workspace.Web/src/layout/Sidebar.tsx
    - src/Prisma.Workspace.Web/src/layout/Topbar.tsx
    - src/Prisma.Workspace.Web/src/pages/ProjectWorkspace.tsx
    - src/Prisma.Workspace.Web/src/pages/Kanban.tsx
    - src/Prisma.Workspace.Web/src/features/board/KanbanFilterBar.tsx
  gates:
    - frontend-build
    - frontend-test
    - frontend-e2e
  human_gate: sim (G-SPEC da SPEC-TOP-NAVIGATION-SHELL + G-SCOPE da D51)
  status: completed
  audit_2026_09_08: >-
    Corrigido de in_progress para completed. Shell superior entregue em
    layout/AppShell.tsx, Topbar.tsx e ContextBar.tsx; layout/Sidebar.tsx nao e
    importado em lugar nenhum. O arquivo morto residual vira TASK-403.
  priority: P1

- id: TASK-033
  spec: SPEC-PROJECTS-VISUAL-REFRESH (specs/projects-visual-refresh.md)
  requirement: Proposta visual cancelada por decisão humana; preservar o visual atual da listagem de projetos sem implementar o redesign anteriormente descrito.
  domain: project
  type: ui
  risk: baixo
  dependencies:
    - TASK-032
  affected_areas:
    - src/Prisma.Workspace.Web/src/pages/Projects.tsx
    - src/Prisma.Workspace.Web/src/pages/Projects.test.tsx
    - src/Prisma.Workspace.Web/e2e
  gates: []
  human_gate: nao (spec superseded; proposta cancelada pela D66)
  status: cancelled
  priority: P1

- id: TASK-034
  spec: SPEC-ORGANIZATIONS (specs/organizations.md)
  requirement: Consolidar organização como tenant raiz isolado, limitar usuário comum a uma única organização sem seletor e reservar ao Administrador da plataforma a seleção entre tenants, criação, edição, arquivamento, leitura de arquivadas e restauração.
  domain: organizations
  type: architecture
  risk: alto
  dependencies:
    - TASK-019
    - TASK-032
  affected_areas:
    - src/Prisma.Workspace.Domain/Entities/Organization.cs
    - src/Prisma.Workspace.Application/Features/Organizations/OrganizationsFeature.cs
    - src/Prisma.Workspace.Infrastructure/Persistence/AppDbContext.cs
    - src/Prisma.Workspace.Infrastructure/Repositories/OrganizationRepository.cs
    - src/Prisma.Workspace.Api/Middleware/OrganizationContextMiddleware.cs
    - src/Prisma.Workspace.Api/Controllers/OrganizationsController.cs
    - src/Prisma.Workspace.Web/src/features/organizations/OrganizationContext.tsx
    - src/Prisma.Workspace.Web/src/layout/Topbar.tsx
    - src/Prisma.Workspace.Web/src/pages/OrganizationSettings.tsx
    - tests/Prisma.Workspace.Tests/MultitenancyPersistenceTests.cs
    - src/Prisma.Workspace.Web/e2e/organization-switch.spec.ts
  gates:
    - backend-build
    - backend-test
    - frontend-build
    - frontend-test
    - frontend-e2e
  human_gate: sim (G-SCOPE para identidade do Administrador da plataforma; G-MIGRATION se o schema de controle precisar mudar)
  status: pending
  priority: P0

```

### Gap aprovado para implementação futura — D52

O código atual ainda precisa ser adequado ao quadro transversal por organização, vínculos de usuários/equipes,
acesso derivado aos projetos, posição singular com coluna obrigatória na criação, colunas livres
abertas/concluídas, transferência preservando histórico, permissão configurável `Administrar quadros` e
arquivamento/restauração do quadro com suas tarefas. A reclassificação aberta -> concluída também deve confirmar
o impacto e concluir atomicamente tarefas e descendentes recursivos autorizados, com histórico individual; o
sentido concluída -> aberta deve confirmar e reabrir atomicamente todas as tarefas da coluna. A decomposição em
tasks será feita após a revisão manual
do módulo; qualquer implementação exige G-WORKFLOW, exige G-MIGRATION se houver alteração real de schema e exige
G-HISTORY somente se a estrutura imutável do histórico mudar.

### Gap aprovado para implementação futura — D65

O status textual e operacional da tarefa deve convergir para a coluna atual: selecionar status move o cartão,
arrastar o cartão atualiza todas as telas e aberta/concluída permanece somente como classificação interna da
coluna. `WorkflowStatusId`, templates, transições e projeções por projeto são legado técnico a ocultar e migrar,
sem ampliar a TASK-009/TASK-015. A implementação exige G-WORKFLOW e, para remover ou alterar schema, G-MIGRATION.

### Gap aprovado para implementação futura — D55

O código atual ainda precisa ser adequado ao modelo único de acesso de `SPEC-USER-ACCESS-PERMISSIONS`: reduzir
os 10 perfis organizacionais aos cinco perfis-base aprovados, eliminar a hierarquia fixa duplicada de cinco
papéis por projeto, criar perfis personalizados reutilizáveis, adicionar o escopo `Board`, granularizar o
catálogo de permissões e implementar acesso derivado Quadro -> Projetos com deny explícito prevalente. O gap
deve ser decomposto somente depois da revisão manual do módulo e de uma auditoria de impacto sobre membros,
convites, grants e papéis existentes. Mudanças de schema ou enums persistidos exigirão `G-MIGRATION`; integração
com o quadro transversal deve acompanhar os gates da D52. Nenhuma task de implementação é criada nesta revisão.

### Gap aprovado para implementação futura — D57

O código atual precisa adequar a gestão de projetos a `SPEC-PROJECT-MANAGEMENT`: criação somente com nome e
descrição, chave técnica automática/oculta, ausência de quadro automático ou pertencente ao projeto, estados
funcionais apenas Ativo/Arquivado, criação/edição/arquivamento/restauração por Administrador e Gestor, múltiplas
equipes e membros individuais e herança de acesso Membro por equipe com revogação apenas da origem removida.
Projeto↔Quadro deve ser relação exclusivamente derivada das tarefas atuais, sem vínculo manual persistente: exige
acesso aos dois recursos, aparece no filtro do quadro enquanto houver tarefa e desaparece quando a última sair.
Arquivar deve ocultar transacionalmente o projeto e suas tarefas, sem apagar dados nem alterar os quadros
transversais independentes; restaurar não pode reativar tarefa previamente arquivada por outra causa. Projeto não
possui exclusão permanente nem lixeira, somente arquivamento/restauração. O gap deve
ser decomposto somente após revisão manual do módulo e auditoria conjunta das D52/D55/D57. Mudanças de schema,
enums persistidos, relações ou backfill exigirão `G-MIGRATION`; mudanças no histórico ou workflow exigirão os
gates correspondentes. Nenhuma task de implementação é criada nesta revisão.

### Gap aprovado para implementação futura — D58

O código atual possui `OrganizationId`, filtros globais, proteção de escrita, middleware do header
`X-Organization-Id`, associação de membros e seletor superior, mas não possui Administrador da plataforma nem
impede que o mesmo usuário seja membro de organizações diferentes.
Hoje qualquer usuário autenticado pode chamar `POST /api/organizations`, o onboarding oferece criação para quem
não tem tenant e `PUT /api/organizations/current` usa uma permissão interna da organização. Também não existem
arquivamento/restauração, leitura exclusiva de control plane nem bloqueio read-only de organização arquivada.
A `TASK-034` deve impor membership única para usuário comum, ocultar seu seletor e introduzir a fronteira de
plataforma sem permitir que perfis ou grants do tenant elevem essa autoridade. Também deve completar o isolamento
de caches/eventos/processos assíncronos e adaptar a troca administrativa para o Kanban/último quadro da D52.
Mudança de schema exige `G-MIGRATION`; a definição técnica da identidade de Platform Admin exige
`G-SCOPE` antes da implementação.

### Gap aprovado para implementação futura — D59

O código atual ainda expõe e usa dependências em `TaskDetailDrawer`, autocomplete, Product Backlog, Kanban,
Meu Trabalho, relatórios e contratos de API. A futura adequação a `SPEC-F-009` deve ocultar toda a experiência,
impedir mutação operacional de vínculos e eliminar efeitos como `IsBlocked`, filtros, badges, priorização,
notificações e automações, preservando os dados internos já existentes. `TASK-006`, `TASK-007` e `TASK-014`
permanecem como registro histórico da implementação anterior, não como requisito funcional vigente. Não remover
schema/código defensivo nem criar migration nesta revisão; qualquer limpeza futura exige `G-MIGRATION`, e qualquer
reativação exige `G-SCOPE`. A implementação deve ser decomposta somente após revisão manual do módulo.

### Gap aprovado para implementação futura — D61

O detalhe da tarefa ainda exibe `Grafo de estados` em React Flow e o endpoint retorna nós/arestas, visitas e
permanência agregada. A futura adequação a `SPEC-TASK-HISTORY` deve substituir essa experiência por `Linha do
tempo`: lista textual da movimentação mais recente para a mais antiga, somente criação/movimentos entre colunas,
com data/hora, pessoa, coluna anterior e nova, incluindo `Tarefa criada na coluna X`. Histórico geral continua
separado e Comentários permanecem exclusivamente em sua aba. `TASK-011` e a parcela de StateGraph da `TASK-016`
são registros históricos sucedidos pela D61; nenhuma task nova é criada antes da revisão manual. Persistir nomes
históricos de colunas ou alterar a estrutura imutável exige respectivamente `G-MIGRATION` e `G-HISTORY`.
Autoria deve seguir D63: nome completo, e-mail oculto quando houver nome e fallback de e-mail restrito ao legado.

---

### P2 - Importante / Dependências resolvidas

```yaml
- id: TASK-008
  spec: SPEC-S-003 (specs/sprints.md)
  requirement: Habilitar drag-and-drop na visão de Quadro do Sprint Dashboard com persistência real de StageId e WorkflowStatus.
  domain: sprint
  type: feature
  risk: médio
  dependencies: []
  affected_areas:
    - src/Prisma.Workspace.Web/src/features/scrum/SprintDashboard.tsx
  gates:
    - frontend-build
    - frontend-test
  human_gate: não
  status: completed
  priority: P2

- id: TASK-009
  spec: SPEC-WORKFLOW-STATUS (specs/workflow-status.md)
  requirement: Histórico da implementação de templates/status separados por projeto, agora sucedidos pela coluna como status canônico da D65; o gap futuro deve desativar e migrar o legado com segurança.
  domain: workflow
  type: architecture
  risk: alto
  dependencies: []
  affected_areas:
    - src/Prisma.Workspace.Domain/Entities/OrganizationWorkflowTemplate.cs
    - src/Prisma.Workspace.Domain/Entities/OrganizationWorkflowStatus.cs
    - src/Prisma.Workspace.Domain/Entities/OrganizationWorkflowTransition.cs
    - src/Prisma.Workspace.Domain/Entities/Project.cs
    - src/Prisma.Workspace.Domain/Entities/WorkflowStatus.cs
    - src/Prisma.Workspace.Infrastructure/Migrations
    - src/Prisma.Workspace.Api/Controllers/OrganizationWorkflowTemplatesController.cs
    - src/Prisma.Workspace.Application/Features/Projects/ProjectsFeature.cs
    - src/Prisma.Workspace.Web/src/features/organizations/WorkflowTemplatesPage.tsx
  gates:
    - backend-build
    - backend-test
    - frontend-build
    - frontend-test
  human_gate: sim (G-SCOPE porque templates estão fora do MVP atual, além de nova arquitetura, autorização RBAC e G-MIGRATION)
  status: completed
  priority: P2

- id: TASK-010
  spec: SPEC-TASK-HISTORY (specs/task-history.md)
  requirement: Padronizar schema JSON de TaskEvent.Payload (antes/depois), adicionar ActorId e Reason em StageHistory (migration) e separar abas na UI.
  domain: history
  type: architecture
  risk: alto
  dependencies: []
  affected_areas:
    - src/Prisma.Workspace.Domain/Entities/StageHistory.cs
    - src/Prisma.Workspace.Domain/Entities/TaskEvent.cs
    - src/Prisma.Workspace.Infrastructure/Migrations
    - src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx
    - src/Prisma.Workspace.Web/src/features/task/TaskFeed.tsx
  gates:
    - backend-build
    - backend-test
    - frontend-build
    - frontend-test
  human_gate: sim (mudança de schema de banco de dados e evento de auditoria)
  status: completed
  priority: P2

- id: TASK-011
  spec: SPEC-TASK-HISTORY (specs/task-history.md)
  requirement: Histórico da implementação do StateGraph, agora sucedido pela Linha do tempo textual definida na D61.
  domain: history
  type: ui
  risk: alto
  dependencies:
    - TASK-010
  affected_areas:
    - src/Prisma.Workspace.Web/src/components/StateGraph.tsx
    - src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx
  gates:
    - frontend-build
    - frontend-test
  human_gate: sim (definição de UX e escolha/integração de biblioteca visual de grafos)
  status: completed
  priority: P2
```

---

### P3 - Melhorias / Coverage

```yaml
- id: TASK-012
  spec: SPEC-WORK-ITEMS (specs/work-items.md)
  requirement: Adicionar testes automatizados garantindo a omissão de Story Points em todas as superfícies de projetos Kanban e sua preservação em Scrum/Scrumban.
  domain: work-item
  type: test
  risk: baixo
  dependencies:
    - TASK-004
  affected_areas:
    - src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.test.tsx
  gates:
    - frontend-test
  human_gate: não
  status: completed
  priority: P3

- id: TASK-013
  spec: SPEC-B-001 (specs/backlog.md)
  requirement: Adicionar testes de integração no backend para a query do backlog hierárquico e testes de componente React para BacklogPlanner.
  domain: backlog
  type: test
  risk: baixo
  dependencies:
    - TASK-005
  affected_areas:
    - tests/Prisma.Workspace.Tests/Features/Backlog/BacklogFeatureTests.cs
    - src/Prisma.Workspace.Web/src/pages/BacklogPlanner.test.tsx
  gates:
    - backend-test
    - frontend-test
  human_gate: não
  status: completed
  priority: P3

- id: TASK-014
  spec: SPEC-F-009 (specs/dependencies.md)
  requirement: Adicionar testes unitários do algoritmo DFS de detecção de ciclos, handlers de vinculação e endpoints de busca.
  domain: dependency
  type: test
  risk: baixo
  dependencies:
    - TASK-007
  affected_areas:
    - tests/Prisma.Workspace.Tests/Domain/WorkItemLinkDomainTests.cs
    - tests/Prisma.Workspace.Tests/Features/WorkItems/CreateWorkItemLinkCommandHandlerTests.cs
    - tests/Prisma.Workspace.Tests/Controllers/WorkItemsControllerTests.cs
  gates:
    - backend-test
  human_gate: não
  status: completed
  priority: P3

- id: TASK-015
  spec: SPEC-WORKFLOW-STATUS (specs/workflow-status.md)
  requirement: Histórico dos testes do modelo de templates/status separados, agora sucedido pela convergência para coluna canônica definida na D65.
  domain: workflow
  type: test
  risk: baixo
  dependencies:
    - TASK-009
  affected_areas:
    - tests/Prisma.Workspace.Tests/Domain/OrganizationWorkflowTemplateDomainTests.cs
    - tests/Prisma.Workspace.Tests/Features/Projects/CreateProjectCommandHandlerTests.cs
  gates:
    - backend-test
  human_gate: não
  status: completed
  priority: P3

- id: TASK-016
  spec: SPEC-TASK-HISTORY (specs/task-history.md)
  requirement: Adicionar testes unitários para o schema de Payload de TaskEvent, movimentação em StageHistory e testes de abas no detalhe da tarefa.
  domain: history
  type: test
  risk: baixo
  dependencies:
    - TASK-010
    - TASK-011
  affected_areas:
    - tests/Prisma.Workspace.Tests/Domain/TaskEventDomainTests.cs
    - tests/Prisma.Workspace.Tests/Features/WorkItems/MoveWorkItemCommandHandlerTests.cs
    - src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.test.tsx
  gates:
    - backend-test
    - frontend-test
  human_gate: não
  status: completed
  priority: P3
```

---

### NEEDS HUMAN PRIORITY

O PDF oficial revelou requisitos que não estavam mapeados pelas tarefas originais. As decisões foram
fechadas por PO e agora fazem parte da Fase 8:

- **Nome do responsável — TASK-017:** `OrganizationMember.DisplayName` é a fonte funcional por tenant.
- **Participantes — resolvido:** participantes selecionáveis são membros do projeto e podem editar, mover e apontar horas conforme o RBAC; ações administrativas continuam exclusivas dos papéis de gestão.
- **Abas do detalhe — D61:** o modal integra `Descrição`, `Comentários`, `Subtarefas`, `Anexos`, `Histórico` e
  `Linha do tempo`; o State Graph implantado é histórico a substituir.
- **Story Points em Kanban — resolvido:** ficam ocultos em todas as superfícies; backend preserva o campo por retrocompatibilidade.
- **Status globais — D45:** origem superior é a organização/tenant, com modos herdado e personalizado.

As specs e os gates correspondentes foram autorizados explicitamente por PO em 2026-08-20.

---

## Seção histórica de dependências entre tarefas

As relações abaixo registram o plano executado anteriormente. A D59 sucede seu uso funcional: dependências ficam
ocultas e sem efeito operacional; `TASK-006`, `TASK-007` e `TASK-014` não autorizam reexposição.

- **TASK-001 (Validação de Estrutura de Trabalho):** Independente; XP permanece fora do escopo.
- **TASK-002 (Renomear Rótulo):** Independente.
- **TASK-003 (Kanban sem Sprint):** Independente.
- **TASK-004 (Ocultar Story Points):** Independente.
- **TASK-005 (Backlog Hierárquico):** Independente.
- **TASK-006 (Busca Dependências):** Concluída historicamente; sua interface deve ficar oculta conforme D59.
- **TASK-007 (Detecção de Ciclos):** Concluída historicamente; o código defensivo pode ser preservado internamente,
  sem virar requisito operacional.
- **TASK-008 (Sprint Board DnD):** Independente.
- **TASK-009/TASK-015 (Workflow separado):** Histórico implantado; sucedidos pelo gap D65 de coluna canônica.
- **TASK-010 (Histórico Antes/Depois):** Independente.
- **TASK-011 (State Graph):** Histórico implantado, sucedido pela Linha do tempo textual da D61.
- **TASK-012 (Testes Story Points):** Depende de **TASK-004**.
- **TASK-013 (Testes Backlog Hierárquico):** Depende de **TASK-005**.
- **TASK-014 (Testes Detecção de Ciclos):** Registro histórico dependente da **TASK-007**; não reativa a função.
- **TASK-016 (Testes Histórico/StateGraph):** Histórico; a parcela de grafo deve ser substituída por cobertura da
  Linha do tempo da D61.
- **TASK-017 (Nome funcional do responsável):** Independente; exige migration aprovada.
- **TASK-018 (Ordem visual do Kanban):** Especializa a ordem livre da D52 sem alterar workflow nem presumir schema; deve coordenar posição compartilhada com a migração futura do modelo transversal.
- **TASK-020 (Criação rápida N:N):** Histórico implantado; sucedido pelo gap D52 ainda a decompor.
- **TASK-021 (Lixeira e arquivamento hierárquico):** Independente do modelo visual de quadro.
- **TASK-022 (Planejamento hierárquico):** Independente.
- **TASK-023 (Ciclo de sprint):** Depende de decisão G-SCOPE/G-WORKFLOW sobre ativação automática.
- **TASK-024 (Chave técnica interna):** Independente; exige mapear consumidores técnicos antes de remover exposições e não autoriza remover a coluna.
- **TASK-025 (Navegação e breadcrumbs):** Depende da **TASK-032** para usar a nova barra contextual; fornece
  preservação de contexto consumida pela TASK-031.
- **TASK-026 (Criar quadro por projeto):** Histórico implantado; sucedido pelo gap D52 ainda a decompor.
- **TASK-027 (Ordenar colunas):** Independente da TASK-029; não move tarefas nem muda status.
- **TASK-028/TASK-029:** Histórico do modelo N:N implantado; sucedido pelo gap D52 ainda a decompor.
- **TASK-030 (Modal de tarefa):** Independente do modelo de dados e preserva as seis abas existentes.
- **TASK-031 (Filtros amigáveis):** Depende da **TASK-032** para ocupar a barra contextual e da **TASK-025**
  para preservar critérios entre Backlog e Quadro.
- **TASK-032 (Shell superior):** Fundação visual de TASK-025 e TASK-031; não altera rotas nem RBAC.
- **Gap D52 (Quadros Runrun.it):** Depende da experiência de filtros e shell; substitui o modelo N:N
  implantado e exige decomposição futura, migration, workflow e revisão de autorização.

---

## Renovação visual Prisma — G-SPEC aprovado

```yaml
- id: TASK-035
  spec: SPEC-PRISMA-VISUAL-SYSTEM (specs/prisma-visual-system.md)
  requirement: Criar a fundação visual reutilizável e renovar o login responsivo conforme D68/D70, preservando todos os fluxos de autenticação.
  domain: visual-experience
  type: ui
  risk: médio
  dependencies: []
  affected_areas:
    - src/Prisma.Workspace.Web/src/styles/theme.ts
    - src/Prisma.Workspace.Web/src/styles/global.ts
    - src/Prisma.Workspace.Web/src/pages/Auth.tsx
    - src/Prisma.Workspace.Web/src/components/ui
  gates:
    - frontend-build
    - frontend-test
    - frontend-lint
    - frontend-e2e
  human_gate: sim (G-SPEC aprovado em 2026-09-03)
  status: completed
  priority: P0

- id: TASK-036
  spec: SPEC-PRISMA-VISUAL-SYSTEM (specs/prisma-visual-system.md)
  requirement: Refinar o shell superior e aplicar as primitivas visuais a Kanban, Meu trabalho e Projetos sem alterar navegação ou regras.
  domain: visual-experience
  type: ui
  risk: médio
  dependencies: [TASK-035]
  affected_areas:
    - src/Prisma.Workspace.Web/src/layout/AppShell.tsx
    - src/Prisma.Workspace.Web/src/layout/Topbar.tsx
    - src/Prisma.Workspace.Web/src/layout/ContextBar.tsx
    - src/Prisma.Workspace.Web/src/pages/Kanban.tsx
    - src/Prisma.Workspace.Web/src/pages/MinhasTarefas.tsx
    - src/Prisma.Workspace.Web/src/pages/Projects.tsx
  gates:
    - frontend-build
    - frontend-test
    - frontend-lint
    - frontend-e2e
  human_gate: sim (G-SPEC aprovado em 2026-09-03)
  status: completed
  priority: P1

- id: TASK-037
  spec: SPEC-PRISMA-VISUAL-SYSTEM (specs/prisma-visual-system.md)
  requirement: Aplicar as primitivas a Relatórios e Configurações e concluir QA visual claro/escuro e responsivo.
  domain: visual-experience
  type: ui
  risk: médio
  dependencies: [TASK-035, TASK-036]
  affected_areas:
    - src/Prisma.Workspace.Web/src/pages/Reports.tsx
    - src/Prisma.Workspace.Web/src/features/reports
    - src/Prisma.Workspace.Web/src/pages/OrganizationSettings.tsx
    - src/Prisma.Workspace.Web/src/pages/ProjectSettings.tsx
  gates:
    - frontend-build
    - frontend-test
    - frontend-lint
    - frontend-e2e
  human_gate: sim (G-SPEC aprovado em 2026-09-03)
  status: completed
  priority: P1
```

## Consultas segmentadas por projeto — escopo adicionado pelo PO

```yaml
- id: TASK-038
  spec: SPEC-PROJECT-WORK-ITEM-QUERIES (specs/project-work-item-queries.md)
  requirement: Criar visão segmentada de Epic, Bug, Feature, Product Backlog Item e Task e permitir consultas por projeto conforme o significado de query aprovado.
  domain: project-queries
  type: feature
  risk: alto
  dependencies: [TASK-031]
  affected_areas:
    - src/Prisma.Workspace.Application/Features
    - src/Prisma.Workspace.Api/Controllers
    - src/Prisma.Workspace.Web/src/features
    - src/Prisma.Workspace.Web/src/pages
  gates:
    - G-SCOPE
    - G-SPEC
    - backend-build
    - backend-test
    - frontend-build
    - frontend-test
    - frontend-lint
    - frontend-e2e
  human_gate: sim (G-SCOPE e G-SPEC aprovados em 2026-09-03)
  status: completed
  audit_2026_09_08: >-
    Corrigido de in_progress para completed. ROADMAP marca a Fase 10 como
    concluida e PROGRESS de 2026-09-04 registra publicacao e validacao visual.
    Evidencia: pages/ProjectItemsQuery.tsx, ProjectItemsQuery.logic.ts,
    ProjectItemsQuery.test.ts e e2e/project-items-query.spec.ts.
  priority: P1
```

## Home autenticada Prisma

```yaml
- id: TASK-039
  spec: SPEC-AUTHENTICATED-HOME (specs/authenticated-home.md)
  requirement: Criar uma página inicial intuitiva e visualmente forte depois do login, usando prioridades e projetos reais.
  domain: authenticated-home
  type: feature
  risk: médio
  dependencies: [TASK-036]
  affected_areas:
    - src/Prisma.Workspace.Web/src/pages/Home.tsx
    - src/Prisma.Workspace.Web/src/App.tsx
    - src/Prisma.Workspace.Web/src/layout/Topbar.tsx
  gates:
    - G-SCOPE
    - G-SPEC
    - frontend-build
    - frontend-test
    - frontend-lint
    - frontend-e2e
    - G-DEPLOY
  human_gate: sim (G-SCOPE, G-SPEC e G-DEPLOY aprovados pela instrução direta do PO em 2026-09-03)
  status: completed
  priority: P1
```

## Natureza da estrutura de trabalho

```yaml
- id: TASK-040
  spec: SPEC-WORK-NATURE (specs/work-nature.md)
  requirement: Classificar cada projeto por Natureza e Tipo de Trabalho predefinidos, sem entidade Demand e sem IA.
  domain: projects
  type: feature
  risk: alto
  dependencies: []
  affected_areas:
    - src/Prisma.Workspace.Domain/Entities/Project.cs
    - src/Prisma.Workspace.Application/Features/Projects/ProjectManagementFeature.cs
    - src/Prisma.Workspace.Infrastructure/Persistence/AppDbContext.cs
    - src/Prisma.Workspace.Infrastructure/Persistence/Migrations
    - src/Prisma.Workspace.Api/Controllers/ProjectsController.cs
    - src/Prisma.Workspace.Web/src/pages/Projects.tsx
  gates:
    - G-SCOPE
    - G-SPEC
    - G-MIGRATION
    - G-WORKFLOW (condicional)
    - backend-build
    - backend-test
    - frontend-build
    - frontend-test
    - frontend-lint
    - frontend-e2e
    - G-DEPLOY
  human_gate: sim (G-SCOPE, G-SPEC, G-MIGRATION e G-DEPLOY aprovados; G-WORKFLOW não aplicável)
  status: completed
  priority: P1
```

## Distribuição open source e instalação independente

```yaml
- id: TASK-041
  spec: SPEC-OPEN-SOURCE-DISTRIBUTION (specs/open-source-distribution.md)
  requirement: Criar uma distribuição Community Edition sanitizada, instalável, documentada e segura em novo repositório.
  domain: open-source-distribution
  type: productization
  risk: alto
  dependencies: []
  affected_areas:
    - docs
    - .github
    - docker-compose.yml
    - scripts
    - src
    - tests
  gates:
    - G-SCOPE
    - G-SPEC
    - security-audit
    - backend-build
    - backend-test
    - frontend-build
    - frontend-test
    - frontend-lint
    - frontend-e2e
    - clean-install
    - G-MIGRATION (condicional)
    - G-DEPLOY
  human_gate: sim (G-SCOPE e G-SPEC aprovados em 2026-09-05; licença, G-MIGRATION condicional e G-DEPLOY pendentes)
  status: in_progress
  priority: P0

- id: TASK-042
  spec: SPEC-INSTALLATION-SETUP (specs/installation-setup.md)
  requirement: Implementar, após aprovação humana, o setup único e atômico da primeira administração Community, status mínimo, token externo redigido, seed demo Development opt-in e UI/E2E de primeiro acesso.
  domain: installation_setup
  type: feature
  risk: alto
  dependencies:
    - TASK-041
  affected_areas:
    - src/Prisma.Workspace.Api/Program.cs
    - src/Prisma.Workspace.Api/Controllers
    - src/Prisma.Workspace.Infrastructure/Persistence/AppDbContext.cs
    - src/Prisma.Workspace.Infrastructure/Persistence/DbInitializer.cs
    - src/Prisma.Workspace.Domain/Entities/Organization.cs
    - src/Prisma.Workspace.Web/src/pages/Auth.tsx
    - src/Prisma.Workspace.Web/e2e
    - tests/Prisma.Workspace.Tests
  gates:
    - G-SPEC
    - backend-build
    - backend-test
    - frontend-build
    - frontend-test
    - frontend-lint
    - frontend-e2e
    - G-MIGRATION
  human_gate: sim (G-SPEC e G-MIGRATION aprovados diretamente pelo PO em 2026-09-05)
  status: completed
  priority: P0
```

---

## Seção de Human Gates

As tarefas listadas abaixo requerem validação ou aprovação humana (Human Gate):

1. **Gap D65 (Coluna como status canônico; TASK-009 histórica):**
   - **Motivo:** Ocultar/desativar templates e status paralelos e migrar suas FKs/dados exige G-WORKFLOW e,
     havendo mudança física, G-MIGRATION com preservação de coluna e histórico.
2. **TASK-010 (Histórico Separado e Schema de Eventos):**
   - **Motivo:** Envolve alteração na entidade `StageHistory` (novas colunas `ActorId` e `Reason`), migration do banco e definição de schema estrito em JSON no `TaskEvent.Payload`.
3. **Gap D61 (Linha do tempo de colunas):**
   - **Motivo:** Substitui o State Graph implantado; exige G-HISTORY/G-MIGRATION somente se a implementação precisar
     alterar estrutura histórica ou persistir snapshots adicionais.
4. **TASK-017 (Nome funcional por organização):**
   - **Motivo:** Adiciona `DisplayName` à associação do membro com a organização e exige migration.
5. **TASK-026/TASK-027 (Criação de quadro e ordem de colunas):**
   - **Motivo:** Alteram a configuração operacional do workflow e exigem G-WORKFLOW após G-SPEC.
6. **TASK-028/TASK-029 (Exclusão e projeções multi-quadro):**
   - **Motivo histórico:** Introduziram relação N:N, quadro padrão, backfill e nova semântica de workflow.
     O modelo foi implantado e agora é entrada do gap de migração da D52.
7. **TASK-030 (Modal centralizado):**
   - **Motivo:** A revisão da SPEC-TASK-HISTORY sucede o contêiner visual da D44 e requer novo G-SPEC.
8. **TASK-031 (Filtros amigáveis):**
   - **Motivo:** A SPEC-SEARCH-SAVED-FILTERS está aprovada; a tarefa permanece em andamento pelas diferenças
     as-built documentadas na própria spec.
9. **TASK-032 (Navegação superior sem sidebar):**
   - **Motivo:** Substitui estruturalmente o shell persistente e exige G-SPEC/G-SCOPE, preservando RBAC e rotas.
10. **Gap D52 (Quadro transversal e posição singular):**
   - **Motivo:** Converterá o N:N implantado, altera escopo/autorização, conclusão por coluna e arquivamento do
     quadro com tarefas; deve ser decomposto antes de executar e exigirá G-MIGRATION/G-WORKFLOW.
11. **TASK-034 (Organizações e control plane):**
   - **Motivo:** Introduz autoridade de Administrador da plataforma e ciclo de arquivamento/restauração do tenant;
     exige G-SCOPE e, caso persista novos dados de controle, G-MIGRATION.
12. **TASK-035/TASK-036/TASK-037 (Renovação visual Prisma):**
   - **Motivo:** Alteram login, shell e múltiplas superfícies do produto. `G-SPEC` aprovado pelo PO em 2026-09-03;
     execução concluída na ordem de dependência, com build, unitários, lint, inspeção visual e E2E aprovados.
13. **TASK-038 (Visões segmentadas e queries por projeto):**
   - **Motivo:** o termo query pode representar filtro visual salvo, linguagem textual ou contrato externo. Exige
     `G-SCOPE` para o recorte e novo `G-SPEC` antes de qualquer código de produto.
14. **TASK-040 (Natureza da estrutura de trabalho):**
   - **Motivo:** adiciona classificação persistida e obrigatória à criação. Exige `G-SPEC`, `G-MIGRATION` com backfill
     seguro e `G-WORKFLOW` somente se houver automação de etapas.
   - **Situação:** concluída e publicada em 2026-09-04; legados permanecem `Não classificado` e novos projetos exigem
     Natureza e Tipo de Trabalho. `G-WORKFLOW` não se aplicou porque nenhuma automação foi criada.
15. **TASK-042 (Setup inicial da instalação):**
   - **Motivo:** cria a primeira identidade, organização e membership Administrator e expõe endpoints anônimos de
     bootstrap; `G-SPEC` e `G-MIGRATION` foram aprovados diretamente pelo PO em 2026-09-05.

---

## Seção de Riscos

- **Migrações de Banco de Dados (gap D65 e TASK-010):** Risco médio/alto de incompatibilidade de dados existentes ou falha de aplicação em ambientes locais ou de produção.
- **Nome funcional (TASK-017):** Registros legados precisam de fallback seguro até o preenchimento de `DisplayName`.
- **Dependências ocultas (D59):** remover a exposição e todo efeito operacional sem apagar dados internos; consumidores
  residuais de `IsBlocked`, filtros, relatórios, notificações e automações precisam ser auditados.
- **Performance de Renderização da Árvore no Backlog (TASK-005):** Risco de lentidão na UI React em projetos com milhares de subtarefas se a montagem recursiva não for otimizada com memoização (`useMemo`).
- **Complexidade de Renderização Visual (TASK-011):** Risco de degradação da UI ou dependência de pacotes externos pesados para renderização de grafos direcionados no frontend.
- **Migração para quadro transversal/posição singular (gap D52):** Risco alto ao escolher uma posição canônica
  para placements N:N legados, derivar acessos e distinguir tarefas arquivadas pelo quadro; exige relatório de
  ambiguidades, backfill verificável, backup e rollback.
- **Filtros (TASK-031):** Critérios demais podem recriar uma interface densa; a divulgação progressiva e os chips
  devem ser validados em desktop e mobile.
- **Shell superior (TASK-032):** Excesso de destinos e utilitários pode sobrecarregar a topbar; prioridade,
  overflow e comportamento responsivo devem ser cobertos em todas as permissões.
- **Control plane multi-tenant (TASK-034):** Falha na separação entre Administrador da plataforma e perfis do
  tenant pode permitir criação ou arquivamento indevido; arquivamento concorrente não pode aceitar escritas nem
  expor dados a membros da organização arquivada.

---

## Ordem sugerida de execução

1. **Fase 1: Quick Wins P0 / P1 Frontend & Backend Base**
   - TASK-002: Renomear rótulo UI "Metodologia" -> "Estrutura de Trabalho"
   - TASK-003: Garantir que Kanban não cria Sprint automaticamente
   - TASK-004: Ocultar Story Points na UI para Kanban
   - TASK-012: Testes para Story Points em Kanban
   - TASK-017: Nome funcional do responsável e participantes
   - TASK-020: Histórico da criação rápida N:N implantada (sucedida pela D52)
   - TASK-021: Lixeira de 7 dias, exclusão hierárquica, reparenting e arquivamento separado
   - TASK-022: Preservar hierarquia ao planejar itens na sprint
   - TASK-023: Editar e excluir sprint conforme o ciclo de vida
   - TASK-024: Gerar chave técnica apenas no backend e ocultá-la de criação, edição e telas normais
   - TASK-025: Navegação contextual entre Product Backlog e Quadro
   - TASK-026: Histórico da criação por projeto com Backlog padrão (sucedida pela D52)
   - TASK-027: Reordenar colunas com persistência e acessibilidade
   - TASK-030: Substituir gaveta de tarefa por modal centralizado
   - TASK-031: Redesenhar filtros com divulgação progressiva e chips
   - TASK-032: Remover sidebar e criar navegação global/contextual superior

2. **Fase 2: Ajustes de Modelo P0 / P1**
   - TASK-001: Validar as quatro opções atuais de ProjectMethodology; XP permanece oculto e adiado
   - TASK-005: Implementar visualização hierárquica conforme D36, ou obter G-SCOPE para N níveis
   - TASK-013: Testes para visualização hierárquica
   - TASK-029: Histórico da migration N:N implantada
   - Gap D52: decompor a migração para quadro transversal, posição singular, acesso derivado e arquivamento conjunto
   - TASK-034: Criar fronteira de Administrador da plataforma e ciclo seguro de organização

3. **Fase 3: Dependências ocultas e Sprint Board P1 / P2**
   - TASK-006/TASK-007/TASK-014: histórico implantado, sucedido pela ocultação aprovada na D59
   - Gap D59: decompor ocultação integral e remoção de efeitos operacionais sem migration destrutiva
   - TASK-008: Implementar drag-and-drop no Sprint Board

4. **Fase 4: Arquitetura Avançada e Histórico P2 / P3**
   - TASK-009/TASK-015: histórico do workflow separado implantado, sucedido pela D65
   - Gap D65: convergir status para coluna canônica e migrar legado com G-WORKFLOW/G-MIGRATION
   - TASK-010: Implementar histórico separado com antes/depois (Human Gate: Migration + Audit)
   - TASK-011: Histórico do State Graph implantado, sucedido pela D61
   - TASK-016: Histórico de testes; substituir parcela do grafo por cobertura da Linha do tempo
   - Gap D61: decompor a Linha do tempo após revisão manual

---

# Auditoria de specs contra o código real — 2026-09-08

Auditoria feita **contra o código em `src/`** e os testes em `tests/`,
`src/Prisma.Workspace.Web/src/**/*.test.*` e `src/Prisma.Workspace.Web/e2e/`. O status declarado antes neste
arquivo **não foi tratado como verdade**; onde ele divergiu da realidade, a divergência está anotada e o
backlog foi corrigido.

Legenda: `implemented` = contrato atendido; `partially_implemented` = parte do contrato existe e há gap
comprovado; `not_implemented` = contrato vigente não existe no código; `blocked_by_gate` = implementação
proibida até aprovação humana; `superseded` = spec encerrada, sem implementação própria.

| # | Spec | Status declarado | Classificação auditada | Evidência (arquivo:linha) |
|---|------|------------------|------------------------|---------------------------|
| 1 | `attachments.md` | approved | `partially_implemented` | Exclusão com confirmação existe: `src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx:241,334`; `src/Prisma.Workspace.Web/src/services/api.ts:818`. Lixeira de 7 dias ausente: `src/Prisma.Workspace.Application/Features/Attachments/AttachmentsFeature.cs:172` declara o gap e aguarda `G-MIGRATION`. |
| 2 | `audit-leadtime-history.md` | approved | `partially_implemented` | Auditoria e histórico existem (`src/Prisma.Workspace.Domain/Entities/AuditLog.cs`, `StageHistory.cs`, `TaskEvent.cs`). Contrato exige lead time **oculto** na UI, mas o Kanban ainda expõe: `src/Prisma.Workspace.Web/src/pages/Kanban.tsx:1340,2202,2223`. |
| 3 | `authenticated-home.md` | approved | `implemented` | `src/Prisma.Workspace.Web/src/pages/Home.tsx`, `Home.test.tsx`, entrada `Início` em `src/Prisma.Workspace.Web/src/layout/Topbar.tsx`. |
| 4 | `auth-security.md` | approved | `partially_implemented` | Refresh rotativo, confirmação, reset e lockout existem (`src/Prisma.Workspace.Domain/Entities/RefreshToken.cs`, `src/Prisma.Workspace.Api/Controllers/AuthController.cs`). Gap: `RegisterRequest` não pede nome completo — `AuthController.cs:230-232` — contra D63. |
| 5 | `backlog.md` (SPEC-B-001) | approved | `partially_implemented` | Backlog hierárquico em `src/Prisma.Workspace.Web/src/features/scrum/BacklogPlanner.tsx`. Gaps: lixeira de 7 dias e reparenting (TASK-021) sem `DeletedAt` no domínio — a busca por `DeletedAt` só encontra `src/Prisma.Workspace.Domain/Entities/WikiPage.cs:35`. |
| 6 | `boards-stages-wip.md` | approved | `partially_implemented` | `Board.OrganizationId` e `ProjectId` nulo existem (`src/Prisma.Workspace.Domain/Entities/Board.cs:13,16`). Gaps: `Board` não tem `IsArchived`; `PlatformPermission` não tem `Administrar quadros` (`src/Prisma.Workspace.Domain/Enums/PlatformPermission.cs`); `PermissionScope` não tem `Board` (`src/Prisma.Workspace.Domain/Enums/PermissionScope.cs`); `StageDto` não expõe `Category` (`src/Prisma.Workspace.Application/Features/Stages/Dtos/StageDto.cs`), logo não existe classificação aberta/concluída na UI nem a operação atômica da D62. |
| 7 | `bulk-actions-automations.md` | approved | `partially_implemented` | Ações em massa implementadas (`src/Prisma.Workspace.Web/src/features/board/KanbanBulkActions.ts`, `KanbanBulkToolbar.tsx`). O contrato exige automações **ocultas**, mas `features/board/AutomationManager.tsx` continua montado no Kanban. |
| 8 | `dashboards-reports.md` | approved | `implemented` | `src/Prisma.Workspace.Application/Features/Reports/*`, `src/Prisma.Workspace.Web/src/pages/Reports.tsx`, `Dashboards.tsx`. Homologação manual segue pendente. |
| 9 | `dependencies.md` (SPEC-F-009) | approved | `not_implemented` | O contrato vigente é **ocultar** dependências (D59). A UI continua expondo criação, listagem e remoção: `src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx:34,337` e `src/Prisma.Workspace.Web/src/features/task/DependencyAutocomplete.tsx`. |
| 10 | `external-portal.md` | approved | `implemented` | `src/Prisma.Workspace.Application/Features/ExternalPortal/*`, `src/Prisma.Workspace.Web/src/pages/PublicPortal.tsx`, `Requests.tsx`. |
| 11 | `gantt-planning.md` | approved | `not_implemented` | O contrato vigente é **ocultar** Gantt e calendário. O seletor de visões ainda oferece ambos: `src/Prisma.Workspace.Web/src/pages/Kanban.tsx:1326`, com `BoardGantt` montado em `Kanban.tsx:1401`. |
| 12 | `installation-setup.md` | approved | `implemented` | `src/Prisma.Workspace.Api/Controllers/SetupController.cs`, `src/Prisma.Workspace.Domain/Entities/InstallationState.cs`, `src/Prisma.Workspace.Web/src/pages/Setup.tsx`, `tests/Prisma.Workspace.Tests/InstallationSetupTests.cs`, `e2e/installation-setup.spec.ts`. |
| 13 | `kanban-visual-order.md` | approved | `partially_implemented` | Ordem manual compartilhada e ordenação filtrada temporária existem (`src/Prisma.Workspace.Web/src/features/board/kanbanOrdering.ts`). Gap D60: tarefa nova entra no **fim** da coluna — `src/Prisma.Workspace.Web/src/pages/Kanban.tsx:798` usa `Math.max(position)+100`. |
| 14 | `multi-board-views.md` | superseded | `superseded` | Sucessor: `SPEC-BOARDS-STAGES-WIP` + D52. Sem implementação própria. |
| 15 | `notifications-realtime.md` | approved | `implemented` | `src/Prisma.Workspace.Domain/Entities/Notification.cs`, `src/Prisma.Workspace.Application/Features/Notifications/NotificationsFeature.cs`, centro no `Topbar.tsx`, SignalR em `src/Prisma.Workspace.Web/src/features/board/useBoardRealtime.ts`. |
| 16 | `open-source-distribution.md` | approved | `partially_implemented` | `compose.yaml`, `Dockerfile`, `.github/workflows/ci.yml`, `CONTRIBUTING.md`, `SECURITY.md`, `GOVERNANCE.md` entregues. Gaps: licença, SBOM, proveniência, SemVer, backup/restauração e passivo npm/NuGet. |
| 17 | `organizations.md` | approved | `partially_implemented` | Multitenancy e membros existem (`src/Prisma.Workspace.Api/Middleware/OrganizationContextMiddleware.cs`, `src/Prisma.Workspace.Domain/Entities/Organization.cs:58`). Gaps D58: sem Administrador da plataforma, sem membership única e sem arquivamento/restauração — `Organization.cs:10-18` só tem `IsActive`. |
| 18 | `organization-switch-refresh.md` | superseded | `superseded` | Incorporada por `SPEC-ORGANIZATIONS`. |
| 19 | `prisma-visual-system.md` | approved | `implemented` | Fase 9 concluída; `src/Prisma.Workspace.Web/src/styles/*`, `layout/AppShell.tsx`, `Topbar.tsx`, `ContextBar.tsx`. |
| 20 | `project-key-auto-generation.md` | approved | `partially_implemented` | A chave saiu do formulário de projeto (`src/Prisma.Workspace.Web/src/pages/Projects.tsx` não referencia `key`). Gap: ainda aparece como prefixo de referência da tarefa — `src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx:175,332` e `src/Prisma.Workspace.Web/src/pages/Kanban.tsx:543`. |
| 21 | `project-management.md` | approved | `partially_implemented` | Projeto com equipes, membros, etiquetas e classificação existe. Gap D57: `ProjectStatus` ainda tem cinco estados (`src/Prisma.Workspace.Domain/Enums/ProjectStatus.cs`) em vez de apenas Ativo/Arquivado. |
| 22 | `project-methodology-hidden.md` | approved | `implemented` | Criação fixa `methodology: 1` sem seletor: `src/Prisma.Workspace.Web/src/pages/Projects.tsx:110`. O enum permanece por compatibilidade conforme D48. |
| 23 | `project-structure.md` | superseded | `superseded` | Sucessor: `SPEC-PROJECT-METHODOLOGY-HIDDEN` (D48). |
| 24 | `projects-visual-refresh.md` | superseded | `superseded` | Encerrada por D66; a evolução visual vigente é `SPEC-PRISMA-VISUAL-SYSTEM` (D70). |
| 25 | `project-work-item-queries.md` | approved | `implemented` | `src/Prisma.Workspace.Web/src/pages/ProjectItemsQuery.tsx`, `ProjectItemsQuery.logic.ts`, `ProjectItemsQuery.test.ts`, `e2e/project-items-query.spec.ts`. |
| 26 | `quick-create-work-item.md` | approved | `partially_implemented` | Criação rápida existe, mas o contrato exige título, projeto, responsável, quadro e coluna obrigatórios: `src/Prisma.Workspace.Application/Features/WorkItems/Commands/CreateWorkItemCommandValidator.cs` só exige título e um destino qualquer; `CreateWorkItemCommand.cs` mantém `StageId`, `ResponsibleId` e `ProjectId` opcionais e ainda aceita `BoardIds[]` do modelo N:N sucedido pela D52. |
| 27 | `responsible-display-name.md` | approved | `partially_implemented` | `OrganizationMember.DisplayName` e resolução canônica existem (`src/Prisma.Workspace.Domain/Entities/Organization.cs:62`, `src/Prisma.Workspace.Infrastructure/Identity/UserDirectory.cs:126`, `tests/Prisma.Workspace.Tests/CanonicalPersonNameTests.cs`). Gap D63: nome completo não é obrigatório no cadastro — `src/Prisma.Workspace.Api/Controllers/AuthController.cs:230`. |
| 28 | `search-saved-filters.md` | approved | `partially_implemented` | `SavedFilter` é pessoal por `UserId` (`src/Prisma.Workspace.Domain/Entities/SavedFilter.cs:11-14`). Gaps D64: não existe campo de escopo `Quadro atual` versus `Qualquer quadro acessível`, nem revalidação por quadro com chips de critério ignorado. |
| 29 | `sla-approvals.md` | approved | `implemented` | `src/Prisma.Workspace.Application/Features/Sla/SlaFeature.cs`, `Features/Approvals/*`, `src/Prisma.Workspace.Domain/Entities/ProjectSlaPolicy.cs`. |
| 30 | `sprints.md` (SPEC-S-003 v2) | draft | `blocked_by_gate` | Rebaixada a `draft` por decisão do PO em 2026-09-08. `Sprint.ProjectId` 1:N e `Status` manual persistem (`src/Prisma.Workspace.Domain/Entities/Sprint.cs:10,16,74-91`); `SprintProject` não existe. Implementação bloqueada até `G-SPEC`. |
| 31 | `task-history.md` | approved | `partially_implemented` | Modal com seis abas e histórico separado existem (`src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx:172`, `features/task/TaskFeed.tsx`). Gap D61: a sexta aba ainda é `Grafo de estados` com React Flow — `TaskDetailDrawer.tsx:297,352` e `features/task/TaskStateGraph.tsx` — em vez da `Linha do tempo` textual. |
| 32 | `teams.md` | approved | `implemented` | `src/Prisma.Workspace.Application/Features/Teams/TeamsFeature.cs`, `src/Prisma.Workspace.Web/src/pages/Teams.tsx`. |
| 33 | `time-tracking.md` | approved | `partially_implemented` | Timer, lançamento manual, agregações e `DayJustification` existem (`Features/TimeEntries/*`, `Features/MeTime/*`). As regras novas de precisão, jornada, sobreposição, timezone e apontamento em tarefa concluída não estão comprovadas em teste e seguem sem homologação. |
| 34 | `top-navigation-shell.md` | approved | `implemented` | Shell superior sem sidebar global: `layout/AppShell.tsx`, `Topbar.tsx`, `ContextBar.tsx`; `layout/Sidebar.tsx` permanece como arquivo morto, sem import em nenhum lugar. |
| 35 | `user-access-permissions.md` | approved | `not_implemented` | O contrato D55 exige cinco perfis-base, perfis personalizados, fim da hierarquia fixa por projeto e escopo `Board`. O código mantém dez perfis (`src/Prisma.Workspace.Domain/Enums/OrganizationRole.cs`), cinco papéis fixos de projeto (`ProjectRole.cs`) e sete escopos sem `Board` (`PermissionScope.cs`). |
| 36 | `wiki-knowledge.md` | approved | `implemented` | `src/Prisma.Workspace.Application/Features/Wiki/*`, `src/Prisma.Workspace.Domain/Entities/WikiPage.cs`, `src/Prisma.Workspace.Web/src/pages/ProjectWiki.tsx`. |
| 37 | `workflow-status.md` | approved | `not_implemented` | O contrato D65 é coluna como status canônico único. O código mantém `WorkItem.WorkflowStatusId` paralelo (`src/Prisma.Workspace.Domain/Entities/WorkItem.cs:27`), a UI ainda prefere `workflowStatusName` (`TaskDetailDrawer.tsx:288`) e a conclusão depende de `Stage.Category` (`Features/WorkItems/Commands/MoveWorkItemCommandHandler.cs:144`). Três fontes de verdade concorrentes. Ver `TASK-BUG-001`. |
| 38 | `work-item-management.md` | approved | `partially_implemented` | Criação, edição, movimentação e arquivamento existem. Gaps D53: sem lixeira de 7 dias (`DeletedAt` não existe em `WorkItem.cs`), sem regra de conclusão do pai condicionada às subtarefas e sem responsável obrigatório na criação. |
| 39 | `work-items.md` | superseded | `superseded` | Sucessores: `SPEC-WORK-ITEM-MANAGEMENT` e `SPEC-PROJECT-METHODOLOGY-HIDDEN`. |
| 40 | `work-nature.md` | approved | `implemented` | Natureza e Tipo de Trabalho persistidos e filtráveis; migration `20260904154816_Add_Project_Work_Classification`; `src/Prisma.Workspace.Web/src/pages/projectClassification.ts`. |

Resumo: 13 `implemented`, 15 `partially_implemented`, 4 `not_implemented`, 1 `blocked_by_gate`,
6 `superseded`, além de `_template.md`, que não é uma spec.

## Divergências entre o backlog e a realidade — corrigidas

1. **TASK-027 (reordenar colunas)** — o backlog dizia `in_progress` sem qualificação. Auditoria: a persistência
   existe e funciona por botões com atualização otimista e rollback
   (`src/Prisma.Workspace.Web/src/pages/Kanban.tsx:761-778`, `services/api.ts:765`). O que falta é somente o
   arraste. Status mantido `in_progress`, agora com a evidência registrada.
2. **TASK-032 (shell superior)** — estava `in_progress`; a auditoria comprova entrega
   (`layout/AppShell.tsx`, `Topbar.tsx`, `ContextBar.tsx`, sem nenhum import de `layout/Sidebar.tsx`).
   Corrigido para `completed`; o arquivo morto vira `TASK-403`.
3. **TASK-038 (consultas segmentadas)** — estava `in_progress`; o `ROADMAP.md` marca a Fase 10 como concluída e
   o `PROGRESS.md` de 2026-09-04 registra publicação e validação visual. Corrigido para `completed`.
4. **TASK-020 (criação rápida)** — marcada `completed`, mas a validação de backend não exige projeto,
   responsável, quadro e coluna. Permanece `completed` como registro histórico do modelo N:N; o contrato
   vigente passa a ser coberto por `TASK-107` do lote `core-domain-v2`.
5. **TASK-024 (chave técnica)** — `pending` está correto, mas a auditoria mostra entrega parcial: a chave já
   saiu do formulário de projeto e resta apenas o prefixo de referência da tarefa.
6. **TASK-031 (filtros salvos)** — `in_progress` está correto; a auditoria acrescenta que o gap é estrutural
   (`SavedFilter` não tem campo de escopo), e não apenas visual.
