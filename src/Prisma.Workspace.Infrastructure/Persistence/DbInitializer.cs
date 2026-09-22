using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Threading.Tasks;

namespace Prisma.Workspace.Infrastructure.Persistence;

/// <summary>
/// Inicializador e populador de dados de demonstração (Seed Data) para o Prisma WorkSpace.
/// </summary>
public static class DbInitializer
{
    public static readonly Guid DefaultOrganizationId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    /// <summary>
    /// Semeia os dados de demonstração. Devolve <c>false</c> quando a instalação já tem
    /// dados e o seed é pulado, para que quem chamou possa avisar — ver D86.
    /// </summary>
    public static async Task<bool> SeedDataAsync(
        AppDbContext context,
        UserManager<IdentityUser> userManager,
        string demoPassword)
    {
        if (string.IsNullOrWhiteSpace(demoPassword))
            throw new InvalidOperationException("A senha do seed de desenvolvimento não foi configurada.");
        // 1. Garantir que o banco de dados está criado e com as migrations mais recentes aplicadas
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var installationState = await context.InstallationStates
            .FromSqlRaw(
                "SELECT * FROM [InstallationStates] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {0}",
                InstallationState.SingletonId)
            .SingleAsync();
        var hasExistingData = installationState.IsInitialized
            || await context.Users.AnyAsync()
            || await context.Organizations.IgnoreQueryFilters().AnyAsync()
            || await context.OrganizationMembers.IgnoreQueryFilters().AnyAsync()
            || await context.Boards.IgnoreQueryFilters().AnyAsync()
            || await context.Projects.IgnoreQueryFilters().AnyAsync()
            || await context.WorkItems.IgnoreQueryFilters().AnyAsync()
            || await context.Teams.IgnoreQueryFilters().AnyAsync()
            || await context.Clients.IgnoreQueryFilters().AnyAsync()
            || await context.TaskTypes.IgnoreQueryFilters().AnyAsync()
            || await context.Tags.IgnoreQueryFilters().AnyAsync()
            || await context.ExternalPortals.IgnoreQueryFilters().AnyAsync()
            || await context.ExternalRequests.IgnoreQueryFilters().AnyAsync()
            || await context.WikiPages.IgnoreQueryFilters().AnyAsync()
            || await context.SavedReports.IgnoreQueryFilters().AnyAsync();
        // D86: instalação com dados não é erro, é motivo para pular. O seed nunca altera
        // dado existente, e derrubar a aplicação no boot por causa disso era pior. Quem
        // chamou recebe false e registra o aviso, para que o pulo não seja silencioso.
        if (hasExistingData)
            return false;

        // 2. Criar usuários de demonstração
        var user1 = new IdentityUser { UserName = "admin@prisma.example.invalid", Email = "admin@prisma.example.invalid", EmailConfirmed = true };
        var adminResult = await userManager.CreateAsync(user1, demoPassword);
        if (!adminResult.Succeeded)
            throw new InvalidOperationException("Não foi possível criar o administrador da demonstração.");

        var user2 = new IdentityUser { UserName = "manager@prisma.example.invalid", Email = "manager@prisma.example.invalid", EmailConfirmed = true };
        var managerResult = await userManager.CreateAsync(user2, demoPassword);
        if (!managerResult.Succeeded)
            throw new InvalidOperationException("Não foi possível criar o gerente da demonstração.");

        var user3 = new IdentityUser { UserName = "member@prisma.example.invalid", Email = "member@prisma.example.invalid", EmailConfirmed = true };
        var memberResult = await userManager.CreateAsync(user3, demoPassword);
        if (!memberResult.Succeeded)
            throw new InvalidOperationException("Não foi possível criar o membro da demonstração.");

        // Organização padrão dos dados de demonstração e respectivas associações.
        var organization = new Organization
        {
            Id = DefaultOrganizationId,
            Name = "Prisma Demo",
            Slug = "prisma-demo",
            Locale = "pt-BR",
            TimeZone = "America/Sao_Paulo",
            WeekStartDay = DayOfWeek.Monday,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await context.Organizations.AddAsync(organization);

        organization.Members.Add(OrganizationMember.Create(organization.Id, user1.Id, OrganizationRole.Administrator));
        organization.Members.Add(OrganizationMember.Create(organization.Id, user2.Id, OrganizationRole.ProjectManager));
        organization.Members.Add(OrganizationMember.Create(organization.Id, user3.Id, OrganizationRole.TeamMember));
        await context.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;

        // 3. Criar Projeto e Quadro (Board)
        var project = Project.Criar("DEMO", "Produto Demo", user1.Id,
            WorkNature.Project, WorkType.Development,
            "Exemplo genérico para explorar planejamento e execução de trabalho.");
        project.OrganizationId = organization.Id;
        await context.Projects.AddAsync(project);
        await context.SaveChangesAsync();

        var board = new Board
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            ProjectId = project.Id,
            Name = "Planejamento do Produto Demo",
            OwnerId = user1.Id,
            CreatedAt = now.AddDays(-15)
        };
        await context.Boards.AddAsync(board);
        await context.SaveChangesAsync();

        // 4. Criar Colunas (Stages) no fluxo do projeto
        var stage1 = new Stage { Id = Guid.NewGuid(), ProjectId = project.Id, BoardId = board.Id, Name = "Ideias", Category = StageCategory.Backlog, Position = 100, CreatedAt = now.AddDays(-15) };
        var stage2 = new Stage { Id = Guid.NewGuid(), ProjectId = project.Id, BoardId = board.Id, Name = "Em andamento", Category = StageCategory.InProgress, Position = 200, CreatedAt = now.AddDays(-15) };
        var stage3 = new Stage { Id = Guid.NewGuid(), ProjectId = project.Id, BoardId = board.Id, Name = "Revisão", Category = StageCategory.Review, Position = 300, CreatedAt = now.AddDays(-15) };
        var stage4 = new Stage { Id = Guid.NewGuid(), ProjectId = project.Id, BoardId = board.Id, Name = "Concluído", Category = StageCategory.Done, Position = 400, CreatedAt = now.AddDays(-15) };

        await context.Stages.AddRangeAsync(stage1, stage2, stage3, stage4);

        // 5. Criar Tarefas Principais (WorkItems onde ParentId == null)
        var card1 = new WorkItem
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            StageId = stage2.Id,
            ParentId = null,
            Title = "Definir objetivos da próxima versão",
            Subtitle = "Planejamento trimestral",
            Description = "Consolidar objetivos, resultados esperados e critérios de sucesso da próxima versão.",
            Priority = Priority.High,
            EstimatedHours = 8.0m,
            DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(5)),
            Position = 100,
            CreatedBy = user1.Id,
            CreatedAt = now.AddDays(-5),
            UpdatedAt = now
        };

        var card2 = new WorkItem
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            StageId = stage3.Id,
            ParentId = null,
            Title = "Revisar protótipo da área de relatórios",
            Subtitle = "Revisão de experiência",
            Description = "Validar clareza, navegação e critérios de aceite do protótipo com a equipe.",
            Priority = Priority.Medium,
            EstimatedHours = 16.0m,
            DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
            Position = 200,
            CreatedBy = user2.Id,
            CreatedAt = now.AddDays(-8),
            UpdatedAt = now
        };

        var card3 = new WorkItem
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            StageId = stage1.Id,
            ParentId = null,
            Title = "Mapear melhorias do fluxo de trabalho",
            Subtitle = "Descoberta de produto",
            Description = "Organizar sugestões genéricas de melhoria para priorização no backlog.",
            Priority = Priority.Low,
            EstimatedHours = 40.0m,
            DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(12)),
            Position = 300,
            CreatedBy = user1.Id,
            CreatedAt = now.AddDays(-10),
            UpdatedAt = now
        };

        await context.WorkItems.AddRangeAsync(card1, card2, card3);

        // 6. Criar Subtarefas (WorkItems onde ParentId == card1.Id)
        var subCard1 = new WorkItem
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            StageId = stage2.Id,
            ParentId = card1.Id,
            Title = "Documentar critérios de sucesso",
            Subtitle = "Checklist de planejamento",
            Description = "Registrar métricas e evidências necessárias para avaliar o objetivo.",
            Priority = Priority.Low,
            EstimatedHours = 2.0m,
            Position = 10,
            CreatedBy = user1.Id,
            CreatedAt = now.AddDays(-4),
            UpdatedAt = now
        };

        var subCard2 = new WorkItem
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            StageId = stage2.Id,
            ParentId = card1.Id,
            Title = "Validar dependências com a equipe",
            Subtitle = "Alinhamento técnico",
            Description = "Confirmar dependências, responsáveis e riscos antes do início da execução.",
            Priority = Priority.Medium,
            EstimatedHours = 4.0m,
            Position = 20,
            CreatedBy = user1.Id,
            CreatedAt = now.AddDays(-4),
            UpdatedAt = now
        };

        await context.WorkItems.AddRangeAsync(subCard1, subCard2);

        // 7. Alocar usuários nas tarefas principais
        var assign1 = new WorkItemAssignee { WorkItemId = card1.Id, UserId = user1.Id, AssignedAt = now.AddDays(-4) };
        var assign2 = new WorkItemAssignee { WorkItemId = card1.Id, UserId = user2.Id, AssignedAt = now.AddDays(-3) };
        var assign3 = new WorkItemAssignee { WorkItemId = card2.Id, UserId = user3.Id, AssignedAt = now.AddDays(-7) };

        await context.WorkItemAssignees.AddRangeAsync(assign1, assign2, assign3);

        // 8. Criar Histórico de Estágios para cálculos do Lead Time
        // Card 1: Passou por Triagem e está em Análise Técnica
        var hist1 = new StageHistory { Id = Guid.NewGuid(), WorkItemId = card1.Id, StageId = stage1.Id, EnteredAt = now.AddDays(-5), LeftAt = now.AddDays(-3) };
        var hist2 = new StageHistory { Id = Guid.NewGuid(), WorkItemId = card1.Id, StageId = stage2.Id, EnteredAt = now.AddDays(-3), LeftAt = null };

        // Card 2: passou por triagem e análise técnica; está em validação.
        var hist3 = new StageHistory { Id = Guid.NewGuid(), WorkItemId = card2.Id, StageId = stage1.Id, EnteredAt = now.AddDays(-8), LeftAt = now.AddDays(-7) };
        var hist4 = new StageHistory { Id = Guid.NewGuid(), WorkItemId = card2.Id, StageId = stage2.Id, EnteredAt = now.AddDays(-7), LeftAt = now.AddDays(-4) };
        var hist5 = new StageHistory { Id = Guid.NewGuid(), WorkItemId = card2.Id, StageId = stage3.Id, EnteredAt = now.AddDays(-4), LeftAt = null };

        // Card 3: Permanece na Triagem desde a criação
        var hist6 = new StageHistory { Id = Guid.NewGuid(), WorkItemId = card3.Id, StageId = stage1.Id, EnteredAt = now.AddDays(-10), LeftAt = null };

        await context.StageHistories.AddRangeAsync(hist1, hist2, hist3, hist4, hist5, hist6);

        // 9. Lançar horas de exemplo (TimeEntry)
        var time1 = TimeEntry.Manual(card1.Id, user1.Id, now.AddDays(-3).AddHours(9), now.AddDays(-3).AddHours(12), "Definição dos objetivos.");
        var time2 = TimeEntry.Manual(card1.Id, user2.Id, now.AddDays(-2).AddHours(14), now.AddDays(-2).AddHours(17), "Revisão dos critérios de sucesso.");
        var time3 = TimeEntry.Manual(card2.Id, user3.Id, now.AddDays(-6).AddHours(8), now.AddDays(-6).AddHours(16), "Avaliação do protótipo.");

        await context.TimeEntries.AddRangeAsync(time1, time2, time3);

        // 10. Conjunto de demonstracao que mostra o produto de verdade: hierarquia completa,
        // os tipos de item, sprints nos tres estados e uma tarefa arquivada.
        var hoje = DateOnly.FromDateTime(now.UtcDateTime);

        // 10.1 Sprints. O estado e derivado das datas (D84), entao basta posicionar o
        // periodo: encerrada, em curso e planejada, sem nenhuma acao manual.
        var sprintEncerrada = Sprint.Criar(project.Id, null, "Sprint 1 — Fundacao",
            hoje.AddDays(-42), hoje.AddDays(-28), "Estruturar o cadastro e o fluxo basico.");
        var sprintAtual = Sprint.Criar(project.Id, null, "Sprint 2 — Relatorios",
            hoje.AddDays(-4), hoje.AddDays(10), "Entregar os relatorios previstos e o portal externo.");
        var sprintFutura = Sprint.Criar(project.Id, null, "Sprint 3 — Automacao",
            hoje.AddDays(11), hoje.AddDays(25), "Automatizar triagem e notificacoes.");
        await context.Sprints.AddRangeAsync(sprintEncerrada, sprintAtual, sprintFutura);

        WorkItem Item(
            string titulo, WorkItemKind tipo, Stage etapa, double posicao,
            Guid? pai = null, Guid? sprint = null, string? descricao = null,
            Priority prioridade = Priority.Medium, int? pontos = null,
            decimal? horas = null, int criadoHaDias = 20, int? concluidoHaDias = null,
            bool arquivada = false, string? responsavel = null)
            => new()
            {
                Id = Guid.NewGuid(), BoardId = board.Id, StageId = etapa.Id, ParentId = pai,
                SprintId = sprint, Kind = tipo, Title = titulo, Description = descricao,
                Priority = prioridade, Points = pontos, EstimatedHours = horas,
                RemainingHours = concluidoHaDias.HasValue ? 0 : horas,
                Position = posicao, BacklogRank = Convert.ToDecimal(posicao),
                IsArchived = arquivada,
                ResponsibleId = responsavel ?? user2.Id,
                CreatedBy = user1.Id, CreatedAt = now.AddDays(-criadoHaDias),
                UpdatedAt = now.AddDays(-1),
                CompletedAt = concluidoHaDias.HasValue ? now.AddDays(-concluidoHaDias.Value) : null,
            };

        // 10.2 Hierarquia completa: Epico -> Feature -> Historia -> Tarefa -> Subtarefa.
        var epico = Item("Portal de atendimento ao cidadao", WorkItemKind.Epic, stage2, 1000,
            descricao: "Permitir que o cidadao abra e acompanhe solicitacoes sem ligar para o suporte.",
            prioridade: Priority.High, criadoHaDias: 45);
        var feature = Item("Acompanhamento publico por protocolo", WorkItemKind.Feature, stage2, 1100,
            pai: epico.Id, sprint: sprintAtual.Id,
            descricao: "Consulta publica do andamento a partir do numero de protocolo.",
            prioridade: Priority.High, pontos: 13, horas: 40, criadoHaDias: 40);
        var historia = Item("Como cidada, quero acompanhar minha solicitacao pelo protocolo",
            WorkItemKind.UserStory, stage2, 1200, pai: feature.Id, sprint: sprintAtual.Id,
            descricao: "Ver situacao, historico e respostas sem precisar de conta.",
            prioridade: Priority.High, pontos: 5, horas: 16, criadoHaDias: 30);
        var tarefa = Item("Criar pagina publica de acompanhamento", WorkItemKind.Task, stage2, 1300,
            pai: historia.Id, sprint: sprintAtual.Id, horas: 8, criadoHaDias: 25, responsavel: user3.Id);
        var subtarefa = Item("Montar o layout responsivo da consulta", WorkItemKind.Subtask, stage3, 1400,
            pai: tarefa.Id, sprint: sprintAtual.Id, horas: 3, criadoHaDias: 20, responsavel: user3.Id);

        // 10.3 Os demais tipos, para que cada um apareca com sua cor e finalidade.
        var bug = Item("Protocolo duplicado ao reenviar o formulario", WorkItemKind.Bug, stage2, 1500,
            sprint: sprintAtual.Id, descricao: "Dois cliques rapidos geram dois protocolos.",
            prioridade: Priority.Critical, horas: 4, criadoHaDias: 6, responsavel: user3.Id);
        var incidente = Item("Portal fora do ar por 12 minutos", WorkItemKind.Incident, stage4, 1600,
            descricao: "Indisponibilidade durante o pico da manha. Causa ja corrigida.",
            prioridade: Priority.Critical, horas: 2, criadoHaDias: 12, concluidoHaDias: 11);
        var melhoria = Item("Reduzir passos do formulario de abertura", WorkItemKind.Improvement, stage1, 1700,
            sprint: sprintFutura.Id, prioridade: Priority.Medium, pontos: 3, horas: 6, criadoHaDias: 9);
        var debito = Item("Substituir consulta N+1 na fila de solicitacoes",
            WorkItemKind.TechnicalDebt, stage1, 1800, sprint: sprintFutura.Id,
            descricao: "A listagem faz uma consulta por linha; nao muda comportamento, sustenta o ritmo.",
            prioridade: Priority.Medium, horas: 5, criadoHaDias: 8);
        var solicitacao = Item("Segunda via do comprovante de licenciamento",
            WorkItemKind.Request, stage1, 1900,
            descricao: "Pedido recebido pelo portal externo.", criadoHaDias: 3);

        // 10.4 Trabalho da sprint encerrada, que sustenta o historico e as metricas.
        var entregue1 = Item("Cadastro de solicitantes", WorkItemKind.UserStory, stage4, 900,
            sprint: sprintEncerrada.Id, prioridade: Priority.High, pontos: 8, horas: 20,
            criadoHaDias: 44, concluidoHaDias: 29);
        var entregue2 = Item("Fluxo de triagem inicial", WorkItemKind.UserStory, stage4, 910,
            sprint: sprintEncerrada.Id, pontos: 5, horas: 14, criadoHaDias: 43, concluidoHaDias: 30);

        // 10.5 Uma tarefa arquivada, para o filtro "Arquivadas" ter o que mostrar.
        var arquivada = Item("Estudo de viabilidade do app nativo", WorkItemKind.Task, stage1, 2000,
            descricao: "Descartado apos a decisao de priorizar o portal web.",
            criadoHaDias: 35, arquivada: true);

        await context.WorkItems.AddRangeAsync(
            epico, feature, historia, tarefa, subtarefa,
            bug, incidente, melhoria, debito, solicitacao,
            entregue1, entregue2, arquivada);

        await context.WorkItemAssignees.AddRangeAsync(
            new WorkItemAssignee { WorkItemId = feature.Id, UserId = user2.Id, AssignedAt = now.AddDays(-30) },
            new WorkItemAssignee { WorkItemId = historia.Id, UserId = user3.Id, AssignedAt = now.AddDays(-25) },
            new WorkItemAssignee { WorkItemId = tarefa.Id, UserId = user3.Id, AssignedAt = now.AddDays(-20) },
            new WorkItemAssignee { WorkItemId = bug.Id, UserId = user3.Id, AssignedAt = now.AddDays(-6) },
            new WorkItemAssignee { WorkItemId = entregue1.Id, UserId = user2.Id, AssignedAt = now.AddDays(-44) });

        // 10.6 Apontamentos da semana corrente, para "Minhas horas" ter conteudo.
        await context.TimeEntries.AddRangeAsync(
            TimeEntry.Manual(tarefa.Id, user3.Id, now.AddDays(-2).AddHours(9), now.AddDays(-2).AddHours(13), "Estrutura da pagina."),
            TimeEntry.Manual(tarefa.Id, user3.Id, now.AddDays(-1).AddHours(14), now.AddDays(-1).AddHours(17), "Consulta por protocolo."),
            TimeEntry.Manual(bug.Id, user3.Id, now.AddHours(-3), now.AddHours(-1), "Reproducao do protocolo duplicado."),
            TimeEntry.Manual(historia.Id, user2.Id, now.AddDays(-3).AddHours(10), now.AddDays(-3).AddHours(12), "Refinamento com a equipe."));

        project.DefaultBoardId = board.Id;

        // 10. Salvar alterações no banco e marcar a instalação como inicializada
        installationState.MarkInitialized(now);
        await context.SaveChangesAsync();
        await EnsureProjectWorkflowsAsync(context);
        await transaction.CommitAsync();
        return true;
    }

    private static async Task EnsureProjectWorkflowsAsync(AppDbContext context)
    {
        var stages = await context.Stages.IgnoreQueryFilters()
            .Include(x => x.Project)
            .OrderBy(x => x.Position)
            .ToListAsync();
        if (stages.Count == 0) return;

        var projectIds = stages.Select(x => x.ProjectId).Distinct().ToList();
        var statuses = await context.WorkflowStatuses.IgnoreQueryFilters()
            .Where(x => projectIds.Contains(x.ProjectId))
            .OrderBy(x => x.Position)
            .ToListAsync();

        foreach (var projectId in projectIds)
        {
            var projectStatuses = statuses.Where(x => x.ProjectId == projectId).ToList();
            foreach (var stage in stages.Where(x => x.ProjectId == projectId))
            {
                var status = projectStatuses.FirstOrDefault(x =>
                    x.Name == stage.Name && x.Category == stage.Category);
                if (status is null)
                {
                    status = WorkflowStatus.Create(projectId, stage.Name,
                        stage.Category switch
                        {
                            StageCategory.Backlog => "#94A3B8",
                            StageCategory.Ready => "#3B82F6",
                            StageCategory.InProgress => "#F59E0B",
                            StageCategory.Review => "#8B5CF6",
                            StageCategory.Done => "#10B981",
                            _ => "#64748B"
                        },
                        projectStatuses.Count, stage.Category,
                        projectStatuses.Count == 0, stage.Category == StageCategory.Done);
                    context.WorkflowStatuses.Add(status);
                    statuses.Add(status);
                    projectStatuses.Add(status);
                }
                // Colunas do seed precisam apontar para status ATIVO: o WorkflowMoveGuard
                // recusa destino com IsActive != true (caso a do board-stage-sync).
                status.IsActive = true;
                stage.WorkflowStatusId = status.Id;
            }
        }

        var stageIds = stages.Select(x => x.Id).ToList();
        var workItems = await context.WorkItems.IgnoreQueryFilters()
            .Where(x => x.StageId != null && stageIds.Contains(x.StageId.Value))
            .ToListAsync();
        var statusByStage = stages.ToDictionary(x => x.Id, x => x.WorkflowStatusId);
        foreach (var item in workItems)
            item.WorkflowStatusId = item.StageId.HasValue ? statusByStage[item.StageId.Value] : null;

        var existingTransitions = await context.WorkflowTransitions.IgnoreQueryFilters().ToListAsync();
        var transitionKeys = existingTransitions
            .Select(x => (x.SourceStatusId, x.TargetStatusId)).ToHashSet();
        foreach (var projectStatuses in statuses.GroupBy(x => x.ProjectId))
            foreach (var source in projectStatuses)
                foreach (var target in projectStatuses.Where(x => x.Id != source.Id))
                    if (transitionKeys.Add((source.Id, target.Id)))
                        context.WorkflowTransitions.Add(
                            WorkflowTransition.Create(source.Id, target.Id));

        await context.SaveChangesAsync();
    }
}
