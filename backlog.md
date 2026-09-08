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
    Falta somente o arraste. Escopo remanescente (arraste) coberto por TASK-124.
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

---

# TASK-BUG-001 — "clico em concluído no kanban e não atualiza"

**Origem:** relato direto do PO em 2026-09-08.
**Specs cruzadas:** `SPEC-WORKFLOW-STATUS` (item "Stage como única fonte funcional de status", D65),
`SPEC-BOARDS-STAGES-WIP` (colunas classificadas como abertas/concluídas, D52/D62),
`SPEC-WORK-ITEM-MANAGEMENT` (conclusão da tarefa).
**Prioridade:** P0.
**Status:** `pending` — investigação documental concluída, correção não iniciada.

## Cadeia de código auditada

Backend:

- `src/Prisma.Workspace.Application/Features/WorkItems/Commands/MoveWorkItemCommandHandler.cs:144`
  `workItem.CompletedAt = destStage?.Category == StageCategory.Done ? workItem.CompletedAt ?? now : null;`
- `src/Prisma.Workspace.Application/Features/WorkItems/WorkItemManagementFeature.cs:376` — o `UpdateWorkItem`
  aplica exatamente a mesma condição, então os dois caminhos dependem de `Stage.Category`.
- `src/Prisma.Workspace.Domain/Entities/Stage.cs:29`
  `public StageCategory Category { get; set; } = StageCategory.InProgress;`
- `src/Prisma.Workspace.Application/Features/Stages/Commands/CreateStageCommandHandler.cs:60-101`
- `src/Prisma.Workspace.Api/Controllers/StagesController.cs:78-81` —
  `CreateStageRequest(..., StageCategory Category = StageCategory.InProgress, ...)`
- `src/Prisma.Workspace.Application/Features/Stages/Dtos/StageDto.cs` — **não expõe `Category`**.

Frontend:

- `src/Prisma.Workspace.Web/src/pages/Kanban.tsx:784` —
  `await api.createStage(selectedBoardId, newStageName, nextPos);` sem `options`.
- `src/Prisma.Workspace.Web/src/services/api.ts:647-655` — `category` só viaja se vier em `options`.
- `src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx:288` —
  `{details.completedAt?'Concluído':details.workflowStatusName||details.stageName||'Backlog'}`
- `src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx:305` — o dropdown `Status` grava `stageId` e
  chama `api.updateWorkItem`, não o endpoint de movimentação.
- `src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx:196-221` — `invalidate()` e o `onMutate`
  otimista.
- `src/Prisma.Workspace.Web/src/pages/Kanban.tsx:489,1892-1919` — o Kanban não usa React Query e só recarrega
  ao fechar a gaveta.

## Hipóteses ranqueadas

### H1 — A coluna "Concluído" criada pela UI nasce com `Category = InProgress` (mais provável)

O Kanban cria colunas sem informar `category`. O backend assume `InProgress`. Em projeto com workflow
`Inherited`, `CreateStageCommandHandler.cs:70` procura um status ativo cujo `Category == request.Category`,
ou seja, casa a coluna "Concluído" com o status "Em andamento". Em seguida
`CreateStageCommandHandler.cs:101` grava `Stage.Category = workflowStatus?.Category ?? request.Category`,
resultando em `InProgress`.

Consequência: mover ou selecionar a coluna "Concluído" **nunca** satisfaz
`destStage.Category == StageCategory.Done`, logo `CompletedAt` continua nulo, o chip nunca vira "Concluído",
o badge do Sprint Backlog (`features/scrum/SprintBacklogPanel.tsx:255`) nunca vira positivo, a contagem de
`unfinishedItemCount` (`features/scrum/SprintDashboard.tsx:488`) nunca cai e a operação coletiva da D62 nunca
dispara. É exatamente o sintoma relatado.

Agravante: `StageDto` não expõe `Category` e não existe UI para classificar coluna como aberta/concluída, então
o usuário não tem nem como corrigir o dado pela interface.

### H2 — O rótulo tem duas fontes de verdade e o `WorkflowStatus` vence a coluna

`TaskDetailDrawer.tsx:288` prefere `workflowStatusName` sobre `stageName`. Como H1 liga a coluna "Concluído"
ao status "Em andamento", a tarefa aparece como "Em andamento" mesmo estando visualmente na coluna
"Concluído". Isso viola diretamente o item "Stage como única fonte funcional de status" da
`SPEC-WORKFLOW-STATUS` e a D65. Mesmo corrigindo H1, esta divergência de rótulo continuaria.

### H3 — A atualização otimista não escreve `completedAt`

`TaskDetailDrawer.tsx:207-221` reescreve `stageId`, `stageName`, `workflowStatusId` e `workflowStatusName`,
mas nunca `completedAt`. Entre o clique e o refetch, o chip permanece com o valor antigo. Como
`detailsQuery` usa `retry:false` (`TaskDetailDrawer.tsx:185`), uma falha de rede deixa o valor errado até a
gaveta ser reaberta. Sozinha, esta hipótese explicaria uma demora perceptível, não um erro permanente.

### H4 — O Kanban por trás da gaveta não é invalidado

`invalidate()` (`TaskDetailDrawer.tsx:196-200`) invalida apenas `['work-item', id]`, `['project-backlog']` e
`['project-sprints']`. O Kanban não usa React Query: carrega por `loadBoardData` (`Kanban.tsx:489`) e só
recarrega quando a gaveta fecha (`Kanban.tsx:1919`). Quem muda o status pelo dropdown do detalhe vê o card
parado na coluna antiga atrás do modal. Explica bem a frase "não atualiza".

### H5 — O item que o Kanban entrega à gaveta não carrega estado de conclusão

`Kanban.tsx:1892-1916` monta o `BacklogItem` sem `completedAt`, `workflowStatusId` e `workflowStatusName`. O
`fallback` de `TaskDetailDrawer.tsx:174-183` nasce, portanto, como não concluído. Enquanto `detailsQuery` não
responde — ou se falhar, dado o `retry:false` — é esse fallback que a tela mostra.

### H6 — Quadro transversal sem projeto cai no mesmo defeito por outro caminho

`CreateStageCommandHandler.cs:60` só entra no ramo de workflow quando `board.ProjectId.HasValue`. Num quadro
transversal da D52, sem projeto, `workflowStatus` fica nulo e `Stage.Category = request.Category = InProgress`.
Além disso, `MoveWorkItemCommandHandler.cs:95,164` não sincroniza placements quando `WorkflowStatusId` é nulo.
Mesmo desfecho de H1, origem diferente — e é o caminho que tende a dominar conforme a D52 avança.

## Testes que provam cada hipótese

| Hipótese | Teste | Camada | Resultado esperado hoje |
|---|---|---|---|
| H1 | Criar coluna via `POST /api/Stages` com apenas `boardId`, `name` e `position`; ler `Stage.Category` do banco | .NET integração | `InProgress`, mesmo com `name = "Concluído"` |
| H1 | Mover a tarefa para essa coluna e assertar `WorkItem.CompletedAt` | .NET (`MoveWorkItemCommandHandlerTests`) | permanece `null` |
| H1 | Mesmo cenário via `UpdateWorkItemCommand` | .NET | permanece `null` |
| H2 | Renderizar a gaveta com `stageName = "Concluído"` e `workflowStatusName = "Em andamento"` | Vitest (`TaskDetailDrawer.test.tsx`) | o chip mostra "Em andamento" |
| H3 | Disparar `change('stageId', <coluna Done>, true)` e inspecionar o cache `['work-item', id]` antes do refetch | Vitest | `completedAt` inalterado |
| H4 | Abrir a gaveta a partir do Kanban, mudar o status e assertar a coluna do card sem fechar o modal | Playwright | o card não muda de coluna |
| H5 | Abrir a gaveta com `getWorkItemDetails` falhando e assertar o chip | Vitest | mostra o fallback, nunca "Concluído" |
| H6 | Criar quadro sem `ProjectId`, criar coluna e assertar `Stage.Category` e `Stage.WorkflowStatusId` | .NET integração | `InProgress` e `null` |

## Correção proposta (a implementar após os gates)

1. Tornar a **classificação da coluna** um dado de primeira classe: expor `Category` em `StageDto`, aceitar e
   editar `aberta`/`concluída` na UI de colunas do Kanban e parar de derivá-la do `WorkflowStatus`.
2. Fazer o Kanban enviar a classificação ao criar coluna, eliminando o default silencioso `InProgress`.
3. Trocar `TaskDetailDrawer.tsx:288` para usar **exclusivamente** o nome da coluna como status textual, com
   `aberta/concluída` como classificação interna, conforme D65.
4. Incluir `completedAt` na atualização otimista e no `BacklogItem` que o Kanban passa à gaveta.
5. Fazer a gaveta invalidar também a leitura do quadro, ou migrar o Kanban para React Query, de modo que a
   mudança de status atualize o card sem fechar o modal.
6. Backfill de `Stage.Category` para as colunas já existentes que deveriam ser concluídas — **não** por
   heurística de nome sem confirmação humana; ver a pergunta de gate abaixo.

## Gates exigidos

- **`G-WORKFLOW`** — obrigatório. Muda a regra de conclusão da tarefa e a classificação da coluna.
- **`G-MIGRATION`** — obrigatório se houver backfill de `Stage.Category` em dados existentes.
- **`G-SPEC`** — não exigido: `SPEC-WORKFLOW-STATUS` e `SPEC-BOARDS-STAGES-WIP` já estão `approved` e já
  contratam este comportamento.
- **`G-HISTORY`** — não aplicável enquanto a estrutura de `StageHistory`/`TaskEvent` não mudar.

## Pergunta de gate para o PO

O backfill de `Stage.Category` das colunas já existentes deve ser feito como?

- **Opção A — relatório primeiro, decisão humana depois.** O sistema lista as colunas candidatas por quadro e o
  PO ou um administrador confirma quais são "concluídas". Impacto: mais lento, zero risco de concluir tarefa
  errada.
- **Opção B — heurística por nome** (`Concluído`, `Done`, `Finalizado`, ...) aplicada automaticamente. Impacto:
  rápido, mas conclui em massa tarefas reais de produção sem revisão e dispara a operação coletiva da D62.
- **Opção C — última coluna de cada quadro** vira concluída. Impacto: simples, mas errado em quadros que
  terminam com coluna de espera ou de arquivamento.

A recomendação técnica é a Opção A. Nenhuma delas foi escolhida; o backfill fica bloqueado até a resposta.

## Entrada estruturada no backlog

```yaml
- id: TASK-BUG-001
  spec: SPEC-WORKFLOW-STATUS (specs/workflow-status.md) + SPEC-BOARDS-STAGES-WIP (specs/boards-stages-wip.md)
  requirement: Fazer a coluna ser a unica fonte funcional de status e de conclusao da tarefa, corrigindo o relato do PO de que concluir no Kanban nao atualiza.
  lote: core-domain-v2
  domain: workflow
  type: bugfix
  risk: alto
  dependencies: []
  gates:
    - G-WORKFLOW
    - G-MIGRATION (condicional ao backfill)
    - backend-build
    - backend-test
    - frontend-build
    - frontend-test
    - frontend-lint
    - frontend-e2e
  human_gate: sim (G-WORKFLOW; G-MIGRATION se houver backfill; escolha da estrategia de backfill em aberto)
  affected_areas:
    - src/Prisma.Workspace.Application/Features/Stages/Dtos/StageDto.cs
    - src/Prisma.Workspace.Application/Features/Stages/Commands/CreateStageCommandHandler.cs
    - src/Prisma.Workspace.Api/Controllers/StagesController.cs
    - src/Prisma.Workspace.Application/Features/WorkItems/Commands/MoveWorkItemCommandHandler.cs
    - src/Prisma.Workspace.Application/Features/WorkItems/WorkItemManagementFeature.cs
    - src/Prisma.Workspace.Web/src/services/api.ts
    - src/Prisma.Workspace.Web/src/pages/Kanban.tsx
    - src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx
  tests:
    - tests/Prisma.Workspace.Tests/MoveWorkItemCommandHandlerTests.cs
    - tests/Prisma.Workspace.Tests/WorkflowDomainTests.cs
    - src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.test.tsx
    - src/Prisma.Workspace.Web/e2e/task-detail-flow.spec.ts
  status: pending
  priority: P0
```

---

# Plano de execução — Prisma WorkSpace v2 (2026-09-08)

Todos os gaps da auditoria acima viraram tarefas pequenas, agrupadas nos lotes definidos pelo PO. Cada tarefa
traz id, spec de origem, dependências, risco, gates exigidos, arquivos previstos e testes exigidos.

Regras invariantes do programa:

- A cadeia de migrations permanece **incremental** do início ao fim (D80). Nenhum lote consolida migrations;
  isso é exclusividade do lote `migrations-consolidation`, executado **por último**.
- Nenhum agente aprova Human Gate. Onde falta decisão, a pergunta está registrada na seção final.
- Lotes que tocam o mesmo arquivo central precisam ser serializados; ver "Arquivos centrais disputados".

## Lote `core-domain-v2`

Specs: `organizations`, `user-access-permissions`, `boards-stages-wip`, `workflow-status`,
`project-management`, `work-item-management`, `quick-create-work-item`, `project-key-auto-generation`,
`project-methodology-hidden`.

```yaml
- id: TASK-100
  spec: SPEC-BOARDS-STAGES-WIP
  requirement: Expor Stage.Category nos contratos de leitura de coluna (StageDto e consumidores).
  dependencies: []
  risk: baixo
  gates: [backend-build, backend-test, frontend-build, frontend-test]
  files:
    - src/Prisma.Workspace.Application/Features/Stages/Dtos/StageDto.cs
    - src/Prisma.Workspace.Application/Features/Stages/Queries
    - src/Prisma.Workspace.Web/src/services/api.ts
  tests: [tests/Prisma.Workspace.Tests/WorkflowDomainTests.cs, src/Prisma.Workspace.Web/src/pages/Kanban.test.tsx]
  status: pending
  priority: P0

- id: TASK-101
  spec: SPEC-BOARDS-STAGES-WIP
  requirement: Permitir classificar a coluna como aberta ou concluida na gestao de colunas do Kanban.
  dependencies: [TASK-100]
  risk: medio
  gates: [G-WORKFLOW, backend-build, backend-test, frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Api/Controllers/StagesController.cs
    - src/Prisma.Workspace.Application/Features/Stages/Commands
    - src/Prisma.Workspace.Web/src/pages/Kanban.tsx
  tests: [tests/Prisma.Workspace.Tests/WorkflowDomainTests.cs, src/Prisma.Workspace.Web/e2e/task-detail-flow.spec.ts]
  status: pending
  priority: P0

- id: TASK-102
  spec: SPEC-BOARDS-STAGES-WIP
  requirement: Fazer o Kanban enviar a classificacao ao criar coluna e eliminar o default silencioso InProgress.
  dependencies: [TASK-101]
  risk: medio
  gates: [G-WORKFLOW, backend-test, frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Web/src/pages/Kanban.tsx
    - src/Prisma.Workspace.Web/src/services/api.ts
    - src/Prisma.Workspace.Application/Features/Stages/Commands/CreateStageCommandHandler.cs
  tests: [tests/Prisma.Workspace.Tests/WorkflowDomainTests.cs]
  status: pending
  priority: P0

- id: TASK-103
  spec: SPEC-WORKFLOW-STATUS
  requirement: Usar exclusivamente o nome da coluna como status textual da tarefa em todas as superficies.
  dependencies: [TASK-100, TASK-BUG-001]
  risk: alto
  gates: [G-WORKFLOW, backend-build, backend-test, frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx
    - src/Prisma.Workspace.Web/src/features/scrum/SprintBacklogPanel.tsx
    - src/Prisma.Workspace.Application/Features/WorkItemDetails/WorkItemDetailsFeature.cs
  tests: [src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.test.tsx, src/Prisma.Workspace.Web/e2e/task-detail-flow.spec.ts]
  status: pending
  priority: P0

- id: TASK-104
  spec: SPEC-BOARDS-STAGES-WIP
  requirement: Reclassificar coluna com confirmacao de impacto, conclusao atomica e descendencia recursiva (D62), nos dois sentidos.
  dependencies: [TASK-101, TASK-121]
  risk: alto
  gates: [G-WORKFLOW, backend-build, backend-test, frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Application/Features/Stages/Commands
    - src/Prisma.Workspace.Application/Features/WorkItems/WorkItemManagementFeature.cs
    - src/Prisma.Workspace.Web/src/pages/Kanban.tsx
  tests: [tests/Prisma.Workspace.Tests/WorkflowDomainTests.cs, tests/Prisma.Workspace.Tests/MoveWorkItemCommandHandlerTests.cs]
  status: pending
  priority: P1

- id: TASK-105
  spec: SPEC-BOARDS-STAGES-WIP
  requirement: Produzir relatorio de colunas candidatas a concluida e, apos decisao humana, aplicar o backfill de Stage.Category.
  dependencies: [TASK-101]
  risk: alto
  gates: [G-MIGRATION, backend-build, backend-test]
  files:
    - src/Prisma.Workspace.Infrastructure/Persistence/Migrations
    - scripts
  tests: [tests/Prisma.Workspace.Tests/WorkflowDomainTests.cs]
  human_gate: sim (estrategia de backfill em aberto; ver perguntas de gate)
  status: blocked
  priority: P1

- id: TASK-106
  spec: SPEC-WORKFLOW-STATUS
  requirement: Retirar WorkflowStatus, templates e transicoes do governo do status, preservando dados e codigo como legado tecnico.
  dependencies: [TASK-103]
  risk: alto
  gates: [G-WORKFLOW, backend-build, backend-test, frontend-build, frontend-test]
  files:
    - src/Prisma.Workspace.Application/Features/Workflow
    - src/Prisma.Workspace.Api/Controllers/WorkflowController.cs
    - src/Prisma.Workspace.Api/Controllers/OrganizationWorkflowTemplatesController.cs
    - src/Prisma.Workspace.Web/src/features/workflow/ProjectWorkflowSettings.tsx
    - src/Prisma.Workspace.Web/src/features/organizations/WorkflowTemplatesPanel.tsx
  tests: [tests/Prisma.Workspace.Tests/OrganizationWorkflowTests.cs]
  status: pending
  priority: P1

- id: TASK-107
  spec: SPEC-QUICK-CREATE-WORK-ITEM
  requirement: Exigir titulo, projeto, responsavel, quadro e coluna na criacao de tarefa, na API e na UI.
  dependencies: [TASK-101]
  risk: alto
  gates: [backend-build, backend-test, frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Application/Features/WorkItems/Commands/CreateWorkItemCommand.cs
    - src/Prisma.Workspace.Application/Features/WorkItems/Commands/CreateWorkItemCommandValidator.cs
    - src/Prisma.Workspace.Application/Features/WorkItems/Commands/CreateWorkItemCommandHandler.cs
    - src/Prisma.Workspace.Web/src/components/GlobalActions.tsx
    - src/Prisma.Workspace.Web/src/pages/Kanban.tsx
  tests: [tests/Prisma.Workspace.Tests/MultiBoardPlacementTests.cs, src/Prisma.Workspace.Web/e2e/quick-create-layout.spec.ts]
  status: pending
  priority: P0

- id: TASK-108
  spec: SPEC-QUICK-CREATE-WORK-ITEM
  requirement: Remover BoardIds[] do contrato de criacao e consolidar a posicao operacional singular da D52.
  dependencies: [TASK-107]
  risk: alto
  gates: [G-MIGRATION (condicional), backend-build, backend-test, frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Application/Features/WorkItems
    - src/Prisma.Workspace.Domain/Entities/WorkItemBoardPlacement.cs
    - src/Prisma.Workspace.Web/src/services/api.ts
  tests: [tests/Prisma.Workspace.Tests/MultiBoardPlacementTests.cs, tests/Prisma.Workspace.Tests/BoardProjectionCommandTests.cs]
  human_gate: sim (G-MIGRATION se a conversao dos placements legados alterar dados)
  status: pending
  priority: P1

- id: TASK-109
  spec: SPEC-BOARDS-STAGES-WIP
  requirement: Adicionar IsArchived/ArchivedAt ao Board e arquivar o quadro junto com suas tarefas, com restauracao por administrador.
  dependencies: [TASK-110]
  risk: alto
  gates: [G-MIGRATION, backend-build, backend-test, frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Domain/Entities/Board.cs
    - src/Prisma.Workspace.Infrastructure/Persistence/Configurations
    - src/Prisma.Workspace.Infrastructure/Persistence/Migrations
    - src/Prisma.Workspace.Application/Features/Boards
  tests: [tests/Prisma.Workspace.Tests/BoardProjectionCommandTests.cs]
  status: pending
  priority: P1

- id: TASK-110
  spec: SPEC-USER-ACCESS-PERMISSIONS
  requirement: Criar a permissao configuravel Administrar quadros em PlatformPermission e aplica-la nas operacoes de quadro e coluna.
  dependencies: []
  risk: medio
  gates: [backend-build, backend-test, frontend-test]
  files:
    - src/Prisma.Workspace.Domain/Enums/PlatformPermission.cs
    - src/Prisma.Workspace.Domain/Authorization/RolePermissionCatalog.cs
    - src/Prisma.Workspace.Infrastructure/Identity/PermissionService.cs
  tests: [tests/Prisma.Workspace.Tests/OrganizationDomainTests.cs]
  status: pending
  priority: P0

- id: TASK-111
  spec: SPEC-USER-ACCESS-PERMISSIONS
  requirement: Adicionar o escopo Board a PermissionScope e implementar o acesso derivado Quadro para Projetos.
  dependencies: [TASK-110]
  risk: alto
  gates: [G-MIGRATION (condicional), backend-build, backend-test]
  files:
    - src/Prisma.Workspace.Domain/Enums/PermissionScope.cs
    - src/Prisma.Workspace.Infrastructure/Identity/BoardAccessService.cs
    - src/Prisma.Workspace.Infrastructure/Identity/ProjectAccessService.cs
  tests: [tests/Prisma.Workspace.Tests/MultitenancyPersistenceTests.cs]
  human_gate: sim (G-MIGRATION se o enum persistido mudar de dominio)
  status: pending
  priority: P0

- id: TASK-112
  spec: SPEC-USER-ACCESS-PERMISSIONS
  requirement: Reduzir OrganizationRole aos cinco perfis-base da D55, com mapeamento verificavel dos dez perfis atuais.
  dependencies: [TASK-111]
  risk: alto
  gates: [G-MIGRATION, backend-build, backend-test, frontend-build, frontend-test]
  files:
    - src/Prisma.Workspace.Domain/Enums/OrganizationRole.cs
    - src/Prisma.Workspace.Domain/Authorization/RolePermissionCatalog.cs
    - src/Prisma.Workspace.Infrastructure/Persistence/Migrations
    - src/Prisma.Workspace.Web/src/pages/OrganizationSettings.tsx
  tests: [tests/Prisma.Workspace.Tests/OrganizationDomainTests.cs]
  human_gate: sim (mapeamento de perfis legados exige decisao humana)
  status: blocked
  priority: P0

- id: TASK-113
  spec: SPEC-USER-ACCESS-PERMISSIONS
  requirement: Eliminar a hierarquia fixa ProjectRole e migrar suas concessoes para o modelo de escopos.
  dependencies: [TASK-112]
  risk: alto
  gates: [G-MIGRATION, backend-build, backend-test]
  files:
    - src/Prisma.Workspace.Domain/Enums/ProjectRole.cs
    - src/Prisma.Workspace.Infrastructure/Identity/ProjectAccessService.cs
    - src/Prisma.Workspace.Infrastructure/Persistence/Migrations
  tests: [tests/Prisma.Workspace.Tests/MultitenancyPersistenceTests.cs]
  status: blocked
  priority: P1

- id: TASK-114
  spec: SPEC-USER-ACCESS-PERMISSIONS
  requirement: Permitir perfis personalizados reutilizaveis por conjunto de permissoes, sem conceder autoridade que o criador nao possui.
  dependencies: [TASK-112]
  risk: alto
  gates: [G-MIGRATION, backend-build, backend-test, frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Domain/Authorization
    - src/Prisma.Workspace.Infrastructure/Persistence/Migrations
    - src/Prisma.Workspace.Web/src/pages/OrganizationSettings.tsx
  tests: [tests/Prisma.Workspace.Tests/OrganizationDomainTests.cs]
  status: pending
  priority: P1

- id: TASK-115
  spec: SPEC-PROJECT-MANAGEMENT
  requirement: Reduzir ProjectStatus a Ativo e Arquivado com backfill dos cinco estados atuais.
  dependencies: []
  risk: alto
  gates: [G-MIGRATION, backend-build, backend-test, frontend-build, frontend-test]
  files:
    - src/Prisma.Workspace.Domain/Enums/ProjectStatus.cs
    - src/Prisma.Workspace.Infrastructure/Persistence/Migrations
    - src/Prisma.Workspace.Web/src/pages/Projects.tsx
    - src/Prisma.Workspace.Web/src/pages/ProjectSettings.tsx
  tests: [tests/Prisma.Workspace.Tests/ProjectFeatureTests.cs]
  human_gate: sim (mapeamento de Planning/OnHold/Completed/Cancelled exige decisao humana)
  status: blocked
  priority: P1

- id: TASK-116
  spec: SPEC-PROJECT-MANAGEMENT
  requirement: Arquivar e restaurar projeto com suas tarefas de forma transacional, sem arquivar os quadros transversais.
  dependencies: [TASK-115, TASK-109]
  risk: alto
  gates: [backend-build, backend-test, frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Application/Features/Projects/ProjectManagementFeature.cs
    - src/Prisma.Workspace.Web/src/pages/Projects.tsx
  tests: [tests/Prisma.Workspace.Tests/ProjectFeatureTests.cs, src/Prisma.Workspace.Web/e2e/project-management.spec.ts]
  status: pending
  priority: P1

- id: TASK-117
  spec: SPEC-ORGANIZATIONS
  requirement: Criar a autoridade de Administrador da plataforma fora dos perfis do tenant, com seletor de organizacoes exclusivo.
  dependencies: [TASK-112]
  risk: alto
  gates: [G-SCOPE, G-MIGRATION, backend-build, backend-test, frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Domain/Entities/Organization.cs
    - src/Prisma.Workspace.Api/Middleware/OrganizationContextMiddleware.cs
    - src/Prisma.Workspace.Api/Controllers/OrganizationsController.cs
    - src/Prisma.Workspace.Web/src/layout/Topbar.tsx
  tests: [tests/Prisma.Workspace.Tests/MultitenancyPersistenceTests.cs, src/Prisma.Workspace.Web/e2e/organization-switch.spec.ts]
  human_gate: sim (G-SCOPE para a identidade tecnica do Platform Admin)
  status: blocked
  priority: P0

- id: TASK-118
  spec: SPEC-ORGANIZATIONS
  requirement: Impor membership unica para usuario comum e ocultar o seletor de organizacao para quem nao e Administrador da plataforma.
  dependencies: [TASK-117]
  risk: alto
  gates: [G-MIGRATION, backend-build, backend-test, frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Application/Features/Organizations/OrganizationsFeature.cs
    - src/Prisma.Workspace.Web/src/features/organizations/OrganizationContext.tsx
  tests: [tests/Prisma.Workspace.Tests/MultitenancyPersistenceTests.cs]
  status: pending
  priority: P0

- id: TASK-119
  spec: SPEC-ORGANIZATIONS
  requirement: Arquivar e restaurar organizacao com leitura restrita ao control plane e bloqueio de escrita.
  dependencies: [TASK-117]
  risk: alto
  gates: [G-MIGRATION, backend-build, backend-test, frontend-build, frontend-test]
  files:
    - src/Prisma.Workspace.Domain/Entities/Organization.cs
    - src/Prisma.Workspace.Infrastructure/Persistence/Migrations
  tests: [tests/Prisma.Workspace.Tests/MultitenancyPersistenceTests.cs]
  status: pending
  priority: P1

- id: TASK-120
  spec: SPEC-WORK-ITEM-MANAGEMENT
  requirement: Criar lixeira de 7 dias do WorkItem distinta de arquivar, com escolha sobre subtarefas, reparenting a avo e restauracao familiar.
  dependencies: []
  risk: alto
  gates: [G-MIGRATION, backend-build, backend-test, frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Domain/Entities/WorkItem.cs
    - src/Prisma.Workspace.Infrastructure/Persistence/Migrations
    - src/Prisma.Workspace.Application/Features/WorkItems/WorkItemManagementFeature.cs
    - src/Prisma.Workspace.Web/src/features/scrum/BacklogPlanner.tsx
  tests: [tests/Prisma.Workspace.Tests/BacklogFeatureTests.cs]
  status: pending
  priority: P1

- id: TASK-121
  spec: SPEC-WORK-ITEM-MANAGEMENT
  requirement: Condicionar a conclusao do pai a todas as subtarefas concluidas e oferecer acao atomica de concluir a familia.
  dependencies: [TASK-BUG-001]
  risk: alto
  gates: [G-WORKFLOW, backend-build, backend-test, frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Application/Features/WorkItems/WorkItemManagementFeature.cs
    - src/Prisma.Workspace.Application/Features/WorkItems/Commands/MoveWorkItemCommandHandler.cs
  tests: [tests/Prisma.Workspace.Tests/MoveWorkItemCommandHandlerTests.cs]
  status: pending
  priority: P0

- id: TASK-122
  spec: SPEC-PROJECT-KEY-AUTO-GENERATION
  requirement: Remover o prefixo de chave tecnica das referencias de tarefa exibidas na interface, preservando o uso interno.
  dependencies: []
  risk: baixo
  gates: [frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx
    - src/Prisma.Workspace.Web/src/pages/Kanban.tsx
  tests: [src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.test.tsx]
  status: pending
  priority: P2

- id: TASK-123
  spec: SPEC-PROJECT-METHODOLOGY-HIDDEN
  requirement: Remover methodology dos contratos de leitura consumidos pela UI, mantendo a coluna por compatibilidade.
  dependencies: []
  risk: baixo
  gates: [backend-build, backend-test, frontend-build, frontend-test]
  files:
    - src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx
    - src/Prisma.Workspace.Web/src/features/reports/ReportsHub.tsx
    - src/Prisma.Workspace.Web/src/features/scrum/BacklogPlanner.tsx
  tests: [src/Prisma.Workspace.Web/src/pages/ProjectStructureLabels.test.tsx]
  status: pending
  priority: P2

- id: TASK-124
  spec: SPEC-BOARDS-STAGES-WIP
  requirement: Completar a reordenacao de colunas com arraste, preservando os botoes acessiveis ja existentes.
  dependencies: [TASK-101]
  risk: baixo
  gates: [frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Web/src/pages/Kanban.tsx
  tests: [src/Prisma.Workspace.Web/e2e/release-shell-navigation.spec.ts]
  status: pending
  priority: P2
```

## Lote `sprint-planning-v2`

Specs: `sprints` (v2 em `draft`), `backlog`, `kanban-visual-order`, `search-saved-filters`.

**Bloqueio de lote:** TASK-200 a TASK-209 dependem do `G-SPEC` da `SPEC-S-003 v2`. Nenhuma delas pode começar
antes da aprovação humana.

```yaml
- id: TASK-200
  spec: SPEC-S-003 v2
  requirement: Criar SprintProject e Sprint.OrganizationId no dominio, na configuracao EF e nos indices.
  dependencies: []
  risk: alto
  gates: [G-SPEC, G-MIGRATION, backend-build, backend-test]
  files:
    - src/Prisma.Workspace.Domain/Entities/Sprint.cs
    - src/Prisma.Workspace.Infrastructure/Persistence/Configurations
    - src/Prisma.Workspace.Infrastructure/Persistence/AppDbContext.cs
  tests: [tests/Prisma.Workspace.Tests/ScrumDomainTests.cs]
  human_gate: sim (G-SPEC pendente + G-MIGRATION)
  status: blocked
  priority: P0

- id: TASK-201
  spec: SPEC-S-003 v2
  requirement: Migration incremental com backfill idempotente e verificavel de Sprint.ProjectId para SprintProject.
  dependencies: [TASK-200]
  risk: alto
  gates: [G-SPEC, G-MIGRATION, backend-build, backend-test]
  files:
    - src/Prisma.Workspace.Infrastructure/Persistence/Migrations
    - scripts
  tests: [tests/Prisma.Workspace.Tests/ScrumApplicationTests.cs]
  human_gate: sim
  status: blocked
  priority: P0

- id: TASK-202
  spec: SPEC-S-003 v2
  requirement: Calcular o estado da sprint pelas datas, remover Iniciar sprint e a exclusividade de sprint ativa.
  dependencies: [TASK-201]
  risk: alto
  gates: [G-SPEC, G-WORKFLOW, backend-build, backend-test, frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Domain/Entities/Sprint.cs
    - src/Prisma.Workspace.Application/Features/Sprints/SprintsFeature.cs
    - src/Prisma.Workspace.Web/src/features/scrum/SprintDashboard.tsx
  tests: [tests/Prisma.Workspace.Tests/ScrumDomainTests.cs]
  human_gate: sim
  status: blocked
  priority: P0

- id: TASK-203
  spec: SPEC-S-003 v2
  requirement: Endpoints de projetos participantes, com remocao exigindo destino atomico das tarefas vinculadas.
  dependencies: [TASK-201]
  risk: alto
  gates: [G-SPEC, backend-build, backend-test, frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Api/Controllers/SprintsController.cs
    - src/Prisma.Workspace.Application/Features/Sprints/SprintsFeature.cs
  tests: [tests/Prisma.Workspace.Tests/ScrumApplicationTests.cs]
  human_gate: sim
  status: blocked
  priority: P0

- id: TASK-204
  spec: SPEC-S-003 v2
  requirement: Validar que o projeto da tarefa participa da sprint antes de aceitar o vinculo.
  dependencies: [TASK-203]
  risk: medio
  gates: [G-SPEC, backend-build, backend-test]
  files:
    - src/Prisma.Workspace.Application/Features/Backlog/BacklogFeature.cs
    - src/Prisma.Workspace.Application/Features/Sprints/SprintsFeature.cs
  tests: [tests/Prisma.Workspace.Tests/ScrumApplicationTests.cs]
  human_gate: sim
  status: blocked
  priority: P0

- id: TASK-205
  spec: SPEC-S-003 v2
  requirement: Implementar DELETE de sprint transacional que remove somente o SprintId das tarefas.
  dependencies: [TASK-203]
  risk: medio
  gates: [G-SPEC, backend-build, backend-test, frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Api/Controllers/SprintsController.cs
    - src/Prisma.Workspace.Application/Features/Sprints/SprintsFeature.cs
  tests: [tests/Prisma.Workspace.Tests/ScrumApplicationTests.cs]
  human_gate: sim
  status: blocked
  priority: P1

- id: TASK-206
  spec: SPEC-S-003 v2
  requirement: Trocar a autorizacao fixa por permissao configuravel avaliada em todos os projetos participantes.
  dependencies: [TASK-203, TASK-112, TASK-114]
  risk: alto
  gates: [G-SPEC, backend-build, backend-test]
  files:
    - src/Prisma.Workspace.Infrastructure/Identity/PermissionService.cs
    - src/Prisma.Workspace.Application/Features/Sprints/SprintsFeature.cs
  tests: [tests/Prisma.Workspace.Tests/ScrumApplicationTests.cs]
  human_gate: sim
  status: blocked
  priority: P1

- id: TASK-207
  spec: SPEC-S-003 v2
  requirement: Metricas consolidadas e segmentaveis por projeto no dashboard e no quadro da sprint.
  dependencies: [TASK-204]
  risk: medio
  gates: [G-SPEC, backend-build, backend-test, frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Web/src/features/scrum/SprintDashboard.tsx
    - src/Prisma.Workspace.Web/src/utils/scrumAnalytics.ts
  tests: [src/Prisma.Workspace.Web/src/utils/scrumAnalytics.test.ts]
  human_gate: sim
  status: blocked
  priority: P1

- id: TASK-208
  spec: SPEC-S-003 v2
  requirement: Planejamento hierarquico atomico com criacao de sprint preservando a selecao atual.
  dependencies: [TASK-204]
  risk: medio
  gates: [G-SPEC, backend-build, backend-test, frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Application/Features/Backlog/BacklogFeature.cs
    - src/Prisma.Workspace.Web/src/features/scrum/BacklogPlanner.tsx
  tests: [tests/Prisma.Workspace.Tests/BacklogFeatureTests.cs]
  human_gate: sim
  status: blocked
  priority: P1

- id: TASK-209
  spec: SPEC-S-003 v2
  requirement: Encerramento da sprint exigindo destino explicito para as tarefas abertas, com rollback integral em falha.
  dependencies: [TASK-205]
  risk: alto
  gates: [G-SPEC, G-HISTORY (condicional), backend-build, backend-test, frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Application/Features/Sprints/SprintsFeature.cs
    - src/Prisma.Workspace.Domain/Entities/Sprint.cs
  tests: [tests/Prisma.Workspace.Tests/ScrumDomainTests.cs]
  human_gate: sim (G-HISTORY se SprintItemSnapshot mudar de estrutura)
  status: blocked
  priority: P1

- id: TASK-210
  spec: SPEC-KANBAN-VISUAL-ORDER
  requirement: Fazer a tarefa recem-criada entrar no topo da coluna escolhida, conforme D60.
  dependencies: []
  risk: baixo
  gates: [frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Web/src/pages/Kanban.tsx
    - src/Prisma.Workspace.Web/src/features/board/kanbanOrdering.ts
  tests: [src/Prisma.Workspace.Web/src/features/board/kanbanOrdering.test.ts]
  status: pending
  priority: P1

- id: TASK-211
  spec: SPEC-SEARCH-SAVED-FILTERS
  requirement: Adicionar escopo Quadro atual ou Qualquer quadro acessivel ao SavedFilter.
  dependencies: []
  risk: medio
  gates: [G-MIGRATION, backend-build, backend-test, frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Domain/Entities/SavedFilter.cs
    - src/Prisma.Workspace.Infrastructure/Persistence/Migrations
    - src/Prisma.Workspace.Web/src/features/board/KanbanFilterBar.tsx
  tests: [src/Prisma.Workspace.Web/src/features/board/KanbanFilterBar.test.ts]
  status: pending
  priority: P1

- id: TASK-212
  spec: SPEC-SEARCH-SAVED-FILTERS
  requirement: Revalidar criterios do filtro global por quadro e sinalizar por chip os criterios ignorados.
  dependencies: [TASK-211]
  risk: medio
  gates: [frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Web/src/features/board/KanbanFilters.ts
    - src/Prisma.Workspace.Web/src/features/board/KanbanFilterBar.tsx
  tests: [src/Prisma.Workspace.Web/src/features/board/KanbanFilterBar.test.ts]
  status: pending
  priority: P1

- id: TASK-213
  spec: SPEC-B-001
  requirement: Serializar e restaurar filtros, item aberto, expansao e rolagem na URL do Product Backlog.
  dependencies: []
  risk: baixo
  gates: [frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Web/src/features/scrum/BacklogPlanner.tsx
    - src/Prisma.Workspace.Web/src/pages/ProjectWorkspace.tsx
  tests: [src/Prisma.Workspace.Web/src/pages/ProjectWorkspace.test.tsx]
  status: pending
  priority: P2
```

## Lote `identity-time-history-v2`

Specs: `auth-security`, `responsible-display-name`, `time-tracking`, `task-history`,
`audit-leadtime-history`, `dependencies`, `notifications-realtime`, `attachments`.

```yaml
- id: TASK-300
  spec: SPEC-RESPONSIBLE-DISPLAY-NAME
  requirement: Exigir nome completo no cadastro de novos usuarios, com fallback restrito a dados legados (D63).
  dependencies: []
  risk: medio
  gates: [G-MIGRATION (condicional), backend-build, backend-test, frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Api/Controllers/AuthController.cs
    - src/Prisma.Workspace.Infrastructure/Identity
    - src/Prisma.Workspace.Web/src/pages/Auth.tsx
  tests: [tests/Prisma.Workspace.Tests/UserDirectoryTests.cs, tests/Prisma.Workspace.Tests/CanonicalPersonNameTests.cs]
  status: pending
  priority: P1

- id: TASK-301
  spec: SPEC-RESPONSIBLE-DISPLAY-NAME
  requirement: Garantir que novos snapshots historicos preservem o nome completo observado no momento do evento.
  dependencies: [TASK-300]
  risk: medio
  gates: [backend-build, backend-test]
  files:
    - src/Prisma.Workspace.Domain/Entities/StageHistory.cs
    - src/Prisma.Workspace.Domain/Entities/TaskEvent.cs
  tests: [tests/Prisma.Workspace.Tests/PlatformCrossCuttingTests.cs]
  status: pending
  priority: P2

- id: TASK-302
  spec: SPEC-TASK-HISTORY
  requirement: Substituir a aba Grafo de estados pela Linha do tempo textual das movimentacoes entre colunas (D61).
  dependencies: []
  risk: medio
  gates: [frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx
    - src/Prisma.Workspace.Web/src/features/task
  tests: [src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.test.tsx, src/Prisma.Workspace.Web/e2e/task-detail-flow.spec.ts]
  status: pending
  priority: P1

- id: TASK-303
  spec: SPEC-TASK-HISTORY
  requirement: Remover TaskStateGraph e o endpoint de nos/arestas apos a Linha do tempo estar em producao.
  dependencies: [TASK-302]
  risk: baixo
  gates: [backend-build, backend-test, frontend-build, frontend-test]
  files:
    - src/Prisma.Workspace.Web/src/features/task/TaskStateGraph.tsx
    - src/Prisma.Workspace.Api/Controllers/TaskFeedController.cs
  tests: [tests/Prisma.Workspace.Tests/TaskStateGraphTests.cs]
  status: pending
  priority: P2

- id: TASK-304
  spec: SPEC-AUDIT-LEADTIME-HISTORY
  requirement: Ocultar lead time e cycle time da interface, preservando a coleta interna.
  dependencies: []
  risk: baixo
  gates: [frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Web/src/pages/Kanban.tsx
    - src/Prisma.Workspace.Web/src/features/board/BoardDashboard.tsx
  tests: [src/Prisma.Workspace.Web/e2e/smoke.spec.ts]
  status: pending
  priority: P1

- id: TASK-305
  spec: SPEC-F-009
  requirement: Ocultar dependencias, pre-requisitos e bloqueios da interface, sem apagar dados (D59).
  dependencies: []
  risk: medio
  gates: [frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx
    - src/Prisma.Workspace.Web/src/features/task/DependencyAutocomplete.tsx
    - src/Prisma.Workspace.Web/src/features/scrum/BacklogPlanner.tsx
  tests: [src/Prisma.Workspace.Web/src/features/task/DependencyAutocomplete.test.tsx]
  status: pending
  priority: P1

- id: TASK-306
  spec: SPEC-F-009
  requirement: Remover o efeito operacional de IsBlocked em filtros, badges, priorizacao, notificacoes, relatorios e automacoes.
  dependencies: [TASK-305]
  risk: alto
  gates: [backend-build, backend-test, frontend-build, frontend-test]
  files:
    - src/Prisma.Workspace.Application/Features/Backlog/BacklogFeature.cs
    - src/Prisma.Workspace.Application/Features/Me/MyWorkDashboardFeature.cs
    - src/Prisma.Workspace.Application/Features/Reports/ReportBuilderFeature.cs
  tests: [tests/Prisma.Workspace.Tests/DependencyGraphTests.cs]
  status: pending
  priority: P1

- id: TASK-307
  spec: SPEC-TIME-TRACKING
  requirement: Aplicar as regras aprovadas de precisao, jornada diaria, sobreposicao e timezone no apontamento.
  dependencies: []
  risk: medio
  gates: [backend-build, backend-test, frontend-build, frontend-test]
  files:
    - src/Prisma.Workspace.Application/Features/TimeEntries
    - src/Prisma.Workspace.Application/Features/MeTime
  tests: [tests/Prisma.Workspace.Tests/ProductivityFeatureTests.cs]
  status: pending
  priority: P1

- id: TASK-308
  spec: SPEC-TIME-TRACKING
  requirement: Definir e implementar o comportamento do apontamento em tarefa concluida.
  dependencies: [TASK-307, TASK-BUG-001]
  risk: medio
  gates: [backend-build, backend-test, frontend-test]
  files:
    - src/Prisma.Workspace.Application/Features/TimeEntries
  tests: [tests/Prisma.Workspace.Tests/ProductivityFeatureTests.cs]
  status: pending
  priority: P2

- id: TASK-309
  spec: SPEC-ATTACHMENTS
  requirement: Implementar lixeira de 7 dias para anexos de tarefa, distinta da exclusao imediata atual.
  dependencies: []
  risk: medio
  gates: [G-MIGRATION, backend-build, backend-test, frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Domain/Entities/Attachment.cs
    - src/Prisma.Workspace.Infrastructure/Persistence/Migrations
    - src/Prisma.Workspace.Application/Features/Attachments/AttachmentsFeature.cs
  tests: [tests/Prisma.Workspace.Tests/PlatformCrossCuttingTests.cs]
  status: pending
  priority: P2

- id: TASK-310
  spec: SPEC-NOTIF-001
  requirement: Homologar notificacoes, preferencias por evento/canal e deduplicacao de lembretes.
  dependencies: []
  risk: baixo
  gates: [backend-test, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Application/Features/Notifications/NotificationsFeature.cs
    - src/Prisma.Workspace.Web/src/layout/Topbar.tsx
  tests: [tests/Prisma.Workspace.Tests/PlatformCrossCuttingTests.cs]
  status: pending
  priority: P2
```

## Lote `community-modules-v2`

Specs: `teams`, `wiki-knowledge`, `external-portal`, `dashboards-reports`, `sla-approvals`,
`bulk-actions-automations`, `gantt-planning`, `prisma-visual-system`, `authenticated-home`,
`project-work-item-queries`, `top-navigation-shell`.

```yaml
- id: TASK-400
  spec: SPEC-GANTT-001
  requirement: Ocultar Gantt e calendario do seletor de visoes do quadro, preservando componentes e dados.
  dependencies: []
  risk: baixo
  gates: [frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Web/src/pages/Kanban.tsx
    - src/Prisma.Workspace.Web/src/features/board/BoardGantt.tsx
    - src/Prisma.Workspace.Web/src/features/board/BoardCalendar.tsx
  tests: [src/Prisma.Workspace.Web/e2e/smoke.spec.ts]
  status: pending
  priority: P1

- id: TASK-401
  spec: SPEC-BULK-ACTIONS-AUTOMATIONS
  requirement: Ocultar o editor de automacoes, preservando execucao, validacao e auditoria internas.
  dependencies: []
  risk: baixo
  gates: [frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Web/src/pages/Kanban.tsx
    - src/Prisma.Workspace.Web/src/features/board/AutomationManager.tsx
  tests: [src/Prisma.Workspace.Web/src/features/board/AutomationManager.test.tsx]
  status: pending
  priority: P1

- id: TASK-402
  spec: SPEC-BULK-ACTIONS-AUTOMATIONS
  requirement: Definir o comportamento hierarquico das acoes em massa entre tarefa e subtarefa.
  dependencies: [TASK-121]
  risk: medio
  gates: [backend-build, backend-test, frontend-build, frontend-test, frontend-e2e]
  files:
    - src/Prisma.Workspace.Web/src/features/board/KanbanBulkActions.ts
    - src/Prisma.Workspace.Application/Features/WorkItems/WorkItemManagementFeature.cs
  tests: [src/Prisma.Workspace.Web/src/features/board/KanbanBulkToolbar.test.tsx]
  status: pending
  priority: P2

- id: TASK-403
  spec: SPEC-TOP-NAVIGATION-SHELL
  requirement: Remover layout/Sidebar.tsx, arquivo morto do shell anterior sem nenhum import.
  dependencies: []
  risk: baixo
  gates: [frontend-build, frontend-lint, frontend-test]
  files:
    - src/Prisma.Workspace.Web/src/layout/Sidebar.tsx
  tests: [src/Prisma.Workspace.Web/src/layout/Topbar.test.tsx]
  status: pending
  priority: P3

- id: TASK-404
  spec: SPEC-TEAMS
  requirement: Homologar equipes, lideranca, capacidade e desativacao logica com preservacao de vinculos.
  dependencies: []
  risk: baixo
  gates: [backend-test, frontend-test, frontend-e2e]
  files: [src/Prisma.Workspace.Application/Features/Teams/TeamsFeature.cs, src/Prisma.Workspace.Web/src/pages/Teams.tsx]
  tests: [tests/Prisma.Workspace.Tests/OrganizationDomainTests.cs]
  status: pending
  priority: P3

- id: TASK-405
  spec: SPEC-WIKI-001
  requirement: Homologar wiki, anexos, organizacao de paginas, busca e restauracao de revisao.
  dependencies: []
  risk: baixo
  gates: [backend-test, frontend-test, frontend-e2e]
  files: [src/Prisma.Workspace.Application/Features/Wiki, src/Prisma.Workspace.Web/src/pages/ProjectWiki.tsx]
  tests: [tests/Prisma.Workspace.Tests/PlatformCrossCuttingTests.cs]
  status: pending
  priority: P3

- id: TASK-406
  spec: SPEC-EXT-001 + SPEC-SLA-001
  requirement: Homologar portal externo, protocolo, triagem, SLA e aprovacoes com dados reais.
  dependencies: []
  risk: medio
  gates: [backend-test, frontend-test, frontend-e2e]
  files: [src/Prisma.Workspace.Application/Features/ExternalPortal, src/Prisma.Workspace.Application/Features/Sla]
  tests: [tests/Prisma.Workspace.Tests/ExternalPortalDomainTests.cs, tests/Prisma.Workspace.Tests/SlaReportingAndCustomFieldTests.cs]
  status: pending
  priority: P3

- id: TASK-407
  spec: SPEC-DASHBOARDS-REPORTS
  requirement: Homologar dashboards, construtor de relatorios e exportacao CSV sob autorizacao.
  dependencies: []
  risk: baixo
  gates: [backend-test, frontend-test, frontend-e2e]
  files: [src/Prisma.Workspace.Application/Features/Reports, src/Prisma.Workspace.Web/src/pages/Reports.tsx]
  tests: [tests/Prisma.Workspace.Tests/SlaReportingAndCustomFieldTests.cs]
  status: pending
  priority: P3

- id: TASK-408
  spec: SPEC-PRISMA-VISUAL-SYSTEM + SPEC-AUTHENTICATED-HOME + SPEC-PROJECT-WORK-ITEM-QUERIES
  requirement: Regressao visual e de acessibilidade das rotas principais depois das mudancas dos lotes anteriores.
  dependencies: [TASK-103, TASK-302, TASK-400, TASK-401]
  risk: medio
  gates: [frontend-build, frontend-lint, frontend-test, frontend-e2e]
  files: [src/Prisma.Workspace.Web/src/pages, src/Prisma.Workspace.Web/src/layout]
  tests: [src/Prisma.Workspace.Web/e2e]
  status: pending
  priority: P2
```

## Lote `productization`

```yaml
- id: TASK-500
  spec: SPEC-OPEN-SOURCE-DISTRIBUTION
  requirement: Neutralizar os residuos institucionais Detran/runrun remanescentes em seed, documentacao e nomes visiveis.
  dependencies: []
  risk: medio
  gates: [backend-build, backend-test, frontend-build, frontend-test, security-audit]
  files: [src/Prisma.Workspace.Infrastructure/Persistence/DbInitializer.cs, docs, README.md]
  tests: [tests/Prisma.Workspace.Tests/InstallationSetupTests.cs]
  status: pending
  priority: P1

- id: TASK-501
  spec: SPEC-OPEN-SOURCE-DISTRIBUTION
  requirement: Escolher e aplicar a licenca do projeto; bloqueia a promocao publica.
  dependencies: []
  risk: alto
  gates: [G-SCOPE]
  files: [LICENSE, README.md, GOVERNANCE.md]
  tests: []
  human_gate: sim (decisao humana obrigatoria; nenhum agente escolhe licenca)
  status: blocked
  priority: P0

- id: TASK-502
  spec: SPEC-OPEN-SOURCE-DISTRIBUTION
  requirement: Corrigir o passivo npm de 5 altas e 28 moderadas em lotes testados, sem audit fix major automatico (D78).
  dependencies: []
  risk: medio
  gates: [frontend-build, frontend-test, frontend-lint, frontend-e2e, security-audit]
  files: [src/Prisma.Workspace.Web/package.json, src/Prisma.Workspace.Web/package-lock.json]
  tests: [src/Prisma.Workspace.Web/e2e]
  status: pending
  priority: P1

- id: TASK-503
  spec: SPEC-OPEN-SOURCE-DISTRIBUTION
  requirement: Fixar a dependencia transitiva moderada do NuGet (AngleSharp) na menor familia estavel compativel (D38).
  dependencies: []
  risk: baixo
  gates: [backend-build, backend-test, security-audit]
  files: [src/Prisma.Workspace.Api, tests/Prisma.Workspace.Tests/Prisma.Workspace.Tests.csproj]
  tests: [tests/Prisma.Workspace.Tests]
  status: pending
  priority: P2

- id: TASK-504
  spec: SPEC-OPEN-SOURCE-DISTRIBUTION
  requirement: Escrever e verificar a documentacao de instalacao, backup e restauracao da Community Edition.
  dependencies: []
  risk: medio
  gates: [clean-install]
  files: [docs/installation, scripts, README.md]
  tests: []
  status: pending
  priority: P1

- id: TASK-505
  spec: SPEC-OPEN-SOURCE-DISTRIBUTION
  requirement: Gerar SBOM e proveniencia no CI, publicados junto aos artefatos.
  dependencies: [TASK-502, TASK-503]
  risk: baixo
  gates: [security-audit]
  files: [.github/workflows/ci.yml, docs/maintenance]
  tests: []
  status: pending
  priority: P2

- id: TASK-506
  spec: SPEC-OPEN-SOURCE-DISTRIBUTION
  requirement: Adotar SemVer e preparar a primeira release v0.1.0.
  dependencies: [TASK-501, TASK-505]
  risk: medio
  gates: [G-DEPLOY]
  files: [.github/workflows, docs, README.md]
  tests: []
  human_gate: sim (G-DEPLOY e licenca)
  status: blocked
  priority: P1

- id: TASK-507
  spec: SPEC-OPEN-SOURCE-DISTRIBUTION
  requirement: Ensaiar instalacao limpa e upgrade em ambiente novo, comprovando preservacao de dados.
  dependencies: [TASK-504]
  risk: alto
  gates: [clean-install, backend-test, frontend-e2e]
  files: [compose.yaml, scripts, docs/installation]
  tests: [src/Prisma.Workspace.Web/e2e/installation-setup.spec.ts]
  status: pending
  priority: P1

- id: TASK-508
  spec: D80
  requirement: Publicar o codigo do prisma-workspace em prisma.nordevs.com.br com backup previo, verificacao e rollback documentado.
  dependencies: [TASK-507]
  risk: alto
  gates: [G-DEPLOY, backend-build, backend-test, frontend-build, frontend-test, frontend-e2e, clean-install]
  files: [compose.yaml, scripts, docs/installation]
  tests: [src/Prisma.Workspace.Web/e2e]
  human_gate: sim (G-DEPLOY autorizado pelo PO em 2026-09-08; a execucao ainda exige backup e verificacao)
  status: pending
  priority: P0
```

## Lote `migrations-consolidation` — POR ÚLTIMO

Este lote **não pode** começar antes de todos os anteriores. A base real de produção (`DetranKanban`, 20
migrations aplicadas, dados reais) é incompatível com a hipótese de migration inicial única
(fresh-install-only). Durante todo o programa a cadeia permanece incremental, para que produção continue
subindo com os dados preservados.

```yaml
- id: TASK-600
  spec: SPEC-OPEN-SOURCE-DISTRIBUTION
  requirement: Inventariar as migrations aplicadas em producao e as criadas durante o programa, com hashes e ordem.
  dependencies: [TASK-508]
  risk: baixo
  gates: [backend-build]
  files: [src/Prisma.Workspace.Infrastructure/Persistence/Migrations, docs/maintenance]
  tests: []
  status: pending
  priority: P1

- id: TASK-601
  spec: SPEC-OPEN-SOURCE-DISTRIBUTION
  requirement: Definir a estrategia de consolidacao preservando __EFMigrationsHistory e os dados reais.
  dependencies: [TASK-600]
  risk: alto
  gates: [G-MIGRATION]
  files: [docs/maintenance]
  tests: []
  human_gate: sim (estrategia exige aprovacao humana antes de qualquer script)
  status: blocked
  priority: P0

- id: TASK-602
  spec: SPEC-OPEN-SOURCE-DISTRIBUTION
  requirement: Ensaiar a consolidacao em copia restaurada da base de producao, com verificacao de dados antes e depois.
  dependencies: [TASK-601]
  risk: alto
  gates: [G-MIGRATION, backend-test, clean-install]
  files: [scripts, src/Prisma.Workspace.Infrastructure/Persistence/Migrations]
  tests: [tests/Prisma.Workspace.Tests]
  status: blocked
  priority: P0

- id: TASK-603
  spec: SPEC-OPEN-SOURCE-DISTRIBUTION
  requirement: Aplicar a consolidacao com backup, verificacao pos-aplicacao e rollback documentado.
  dependencies: [TASK-602]
  risk: alto
  gates: [G-MIGRATION, G-DEPLOY, backend-test, frontend-e2e]
  files: [src/Prisma.Workspace.Infrastructure/Persistence/Migrations, scripts, docs/maintenance]
  tests: [src/Prisma.Workspace.Web/e2e]
  human_gate: sim
  status: blocked
  priority: P0
```

---

## Arquivos centrais disputados entre lotes

Lista para o coordenador serializar a ordem. Cada linha é um ponto onde dois ou mais lotes escrevem no mesmo
arquivo, com risco real de conflito semântico — não apenas textual.

| Arquivo central | Lotes que o tocam | Tarefas em disputa | Regra de serialização |
|---|---|---|---|
| `src/Prisma.Workspace.Infrastructure/Persistence/Migrations/*` e o snapshot do modelo | core-domain-v2, sprint-planning-v2, identity-time-history-v2, migrations-consolidation | TASK-105, 108, 109, 111, 112, 113, 114, 115, 117, 118, 119, 120, 200, 201, 211, 300, 309, 601-603 | **Uma migration por vez, em série.** Duas migrations criadas em paralelo colidem no snapshot e produzem cadeia divergente. Nenhum lote gera migration enquanto outro tiver migration não integrada. |
| `src/Prisma.Workspace.Infrastructure/Persistence/AppDbContext.cs` | core-domain-v2, sprint-planning-v2, identity-time-history-v2 | TASK-109, 120, 200, 309 | Serializar por lote; integrar core-domain-v2 antes de sprint-planning-v2. |
| `src/Prisma.Workspace.Domain/Entities/WorkItem.cs` | core-domain-v2, sprint-planning-v2, identity-time-history-v2 | TASK-108, 120, 121, 204, 301 | core-domain-v2 primeiro (lixeira e conclusão), depois sprint-planning-v2 (vínculo), por último identity. |
| `src/Prisma.Workspace.Domain/Entities/Stage.cs` e sua configuração EF | core-domain-v2, sprint-planning-v2 | TASK-100, 101, 105, 207 | core-domain-v2 é dono; sprint-planning-v2 só consome. |
| `src/Prisma.Workspace.Domain/Entities/Sprint.cs` | sprint-planning-v2, migrations-consolidation | TASK-200, 202, 209, 601 | sprint-planning-v2 é dono até a consolidação. |
| `src/Prisma.Workspace.Domain/Enums/PermissionScope.cs`, `PlatformPermission.cs`, `OrganizationRole.cs`, `ProjectRole.cs` | core-domain-v2, sprint-planning-v2 | TASK-110, 111, 112, 113, 114, 206 | **TASK-206 é bloqueada por TASK-112 e TASK-114.** Não mexer no modelo de permissão em dois lotes ao mesmo tempo. |
| `src/Prisma.Workspace.Infrastructure/Identity/PermissionService.cs`, `ProjectAccessService.cs`, `BoardAccessService.cs` | core-domain-v2, sprint-planning-v2 | TASK-110, 111, 113, 206 | Mesma regra da linha anterior. |
| `src/Prisma.Workspace.Application/Features/WorkItems/WorkItemManagementFeature.cs` | core-domain-v2, identity-time-history-v2, community-modules-v2 | TASK-104, 107, 120, 121, 306, 402 | Arquivo grande e central; integrar core-domain-v2 inteiro antes de abrir os outros lotes nele. |
| `src/Prisma.Workspace.Application/Features/WorkItems/Commands/MoveWorkItemCommandHandler.cs` | TASK-BUG-001, core-domain-v2, sprint-planning-v2 | TASK-BUG-001, 104, 121, 207 | TASK-BUG-001 vai primeiro; é a base do resto. |
| `src/Prisma.Workspace.Application/Features/WorkItems/Dtos/WorkItemDto.cs` e `Features/WorkItemDetails/WorkItemDetailsFeature.cs` | core-domain-v2, sprint-planning-v2, identity-time-history-v2 | TASK-103, 108, 204, 302 | **Contrato de DTO consumido por Kanban, backlog, sprint, relatórios e portal.** Toda mudança aqui quebra vários consumidores ao mesmo tempo; alterar em uma única passada por lote. |
| `src/Prisma.Workspace.Web/src/pages/Kanban.tsx` (2409 linhas) | core-domain-v2, sprint-planning-v2, identity-time-history-v2, community-modules-v2 | TASK-101, 102, 104, 107, 122, 124, 210, 304, 400, 401 | Arquivo mais disputado do frontend. Serializar por lote e considerar quebrá-lo antes de abrir dois lotes nele. |
| `src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx` | core-domain-v2, identity-time-history-v2, community-modules-v2 | TASK-103, 122, 123, 302, 305 | Segundo arquivo mais disputado; core-domain-v2 antes de identity. |
| `src/Prisma.Workspace.Web/src/services/api.ts` | todos os lotes | quase todas | Alterações aditivas apenas; evitar renomear métodos existentes fora de uma tarefa dedicada. |
| `src/Prisma.Workspace.Web/src/types/scrum.ts` | core-domain-v2, sprint-planning-v2, identity-time-history-v2 | TASK-103, 204, 207, 302 | Tipos compartilhados entre Kanban, backlog, sprint e gaveta; segue a mesma ordem do DTO backend. |
| `src/Prisma.Workspace.Domain/Entities/StageHistory.cs`, `TaskEvent.cs`, `AuditLog.cs` e `SprintItemSnapshot` (em `Sprint.cs`) | identity-time-history-v2, sprint-planning-v2 | TASK-209, 301, 302 | **Estruturas de histórico: qualquer mudança aciona `G-HISTORY`.** Não abrir duas frentes simultâneas aqui. |
| `tests/Prisma.Workspace.Tests/MoveWorkItemCommandHandlerTests.cs` e `MultitenancyPersistenceTests.cs` | core-domain-v2, sprint-planning-v2 | TASK-BUG-001, 104, 111, 113, 118, 121 | Reescritos por mais de um lote; consolidar por lote, não por tarefa. |

### Ordem recomendada de execução dos lotes

1. `TASK-BUG-001` isolado — é o defeito relatado pelo PO e a base de `TASK-103`, `TASK-104` e `TASK-121`.
2. `core-domain-v2` — dono de `Stage`, `WorkItem`, permissões, projeto e organização.
3. `sprint-planning-v2` — depende de permissões e de `Stage` estabilizados; bloqueado pelo `G-SPEC`.
4. `identity-time-history-v2` — depende da gaveta e do `WorkItem` estabilizados.
5. `community-modules-v2` — ocultações e homologações, majoritariamente frontend.
6. `productization` — pode correr em paralelo com 3-5, exceto `TASK-508`, que vem depois de tudo.
7. `migrations-consolidation` — por último, sempre.

---

## Perguntas de gate abertas para o PO

1. **`G-SPEC` da `SPEC-S-003 v2`** — aprovar, ajustar ou rejeitar o contrato Sprint ↔ Project N:N. Todo o lote
   `sprint-planning-v2` está parado até aqui. As cinco perguntas específicas da spec estão em `specs/sprints.md`.
2. **Backfill de `Stage.Category` (TASK-105)** — Opção A (relatório e confirmação humana), Opção B (heurística
   por nome, conclui em massa sem revisão) ou Opção C (última coluna de cada quadro). Recomendação técnica: A.
3. **Mapeamento dos dez perfis para cinco (TASK-112)** — para onde vão `ProjectManager`, `ScrumMaster`,
   `ProductOwner`, `Developer`, `TeamMember` e `Client`? Sem essa tabela, a migration não pode ser escrita.
4. **Mapeamento de `ProjectStatus` (TASK-115)** — `Planning`, `OnHold`, `Completed` e `Cancelled` viram `Ativo`
   ou `Arquivado`? A escolha muda a visibilidade de projetos reais em produção.
5. **Identidade técnica do Administrador da plataforma (TASK-117)** — `G-SCOPE` ainda pendente desde a D58:
   flag no Identity, tabela de control plane separada, ou configuração externa?
6. **Licença do projeto (TASK-501)** — decisão exclusivamente humana; bloqueia a promoção pública e a
   `v0.1.0`.
7. **Apontamento em tarefa concluída (TASK-308)** — permitido, permitido com justificativa, ou bloqueado?
8. **Capacidade da sprint (SPEC-S-003 v2)** — permanece informativa e visível, ou fica oculta até nova decisão?
9. **`G-HISTORY` do `SprintItemSnapshot` (TASK-209)** — aceitar acionar o gate para incluir `ProjectId` no
   snapshot, ou derivar o projeto por join e manter o snapshot intacto?

Nenhuma dessas perguntas foi respondida por agente. Onde não há decisão registrada em `DECISIONS.md` nem nas
specs, a tarefa correspondente está com `status: blocked`.
