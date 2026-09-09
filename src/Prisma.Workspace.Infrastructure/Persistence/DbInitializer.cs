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

    public static async Task SeedDataAsync(
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
        if (hasExistingData)
            throw new InvalidOperationException(
                "O seed demo exige uma instalação vazia e nunca altera dados existentes.");

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

        // 4. Criar Colunas (Stages) no Board
        var stage1 = new Stage { Id = Guid.NewGuid(), BoardId = board.Id,             Name = "Ideias", Category = StageCategory.Backlog, Position = 100, CreatedAt = now.AddDays(-15) };
        var stage2 = new Stage { Id = Guid.NewGuid(), BoardId = board.Id, Name = "Em andamento", Category = StageCategory.InProgress, Position = 200, CreatedAt = now.AddDays(-15) };
        var stage3 = new Stage { Id = Guid.NewGuid(), BoardId = board.Id, Name = "Revisão", Category = StageCategory.Review, Position = 300, CreatedAt = now.AddDays(-15) };
        var stage4 = new Stage { Id = Guid.NewGuid(), BoardId = board.Id, Name = "Concluído", Category = StageCategory.Done, Position = 400, CreatedAt = now.AddDays(-15) };

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

        project.DefaultBoardId = board.Id;

        // 10. Salvar alterações no banco e marcar a instalação como inicializada
        installationState.MarkInitialized(now);
        await context.SaveChangesAsync();
        await EnsureProjectWorkflowsAsync(context);
        await transaction.CommitAsync();
    }

    private static async Task EnsureProjectWorkflowsAsync(AppDbContext context)
    {
        var stages = await context.Stages.IgnoreQueryFilters()
            .Include(x => x.Board)
            .Where(x => x.Board.ProjectId != null)
            .OrderBy(x => x.Position)
            .ToListAsync();
        if (stages.Count == 0) return;

        var projectIds = stages.Select(x => x.Board.ProjectId!.Value).Distinct().ToList();
        var statuses = await context.WorkflowStatuses.IgnoreQueryFilters()
            .Where(x => projectIds.Contains(x.ProjectId))
            .OrderBy(x => x.Position)
            .ToListAsync();

        foreach (var projectId in projectIds)
        {
            var projectStatuses = statuses.Where(x => x.ProjectId == projectId).ToList();
            foreach (var stage in stages.Where(x => x.Board.ProjectId == projectId))
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
