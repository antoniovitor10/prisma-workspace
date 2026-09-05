using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
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
        await context.Database.MigrateAsync();

        // 2. Criar ou forçar senha de Usuários de demonstração com logs detalhados
        var user1 = await userManager.FindByEmailAsync("po@detran.local");
        if (user1 is null)
        {
            user1 = new IdentityUser { UserName = "po@detran.local", Email = "po@detran.local", EmailConfirmed = true };
            var res = await userManager.CreateAsync(user1, demoPassword);
            Serilog.Log.Information("Seed user1 creation Succeeded: {Succ}, Errors: {Errors}", res.Succeeded, string.Join(", ", res.Errors.Select(e => e.Description)));
        }
        else
        {
            if (!user1.EmailConfirmed) { user1.EmailConfirmed = true; await userManager.UpdateAsync(user1); }
        }

        var user2 = await userManager.FindByEmailAsync("joao.silva@detran.se.gov.br");
        if (user2 is null)
        {
            user2 = new IdentityUser { UserName = "joao.silva@detran.se.gov.br", Email = "joao.silva@detran.se.gov.br", EmailConfirmed = true };
            var res = await userManager.CreateAsync(user2, demoPassword);
            Serilog.Log.Information("Seed user2 creation Succeeded: {Succ}", res.Succeeded);
        }
        else
        {
            if (!user2.EmailConfirmed) { user2.EmailConfirmed = true; await userManager.UpdateAsync(user2); }
        }

        var user3 = await userManager.FindByEmailAsync("maria.santos@detran.se.gov.br");
        if (user3 is null)
        {
            user3 = new IdentityUser { UserName = "maria.santos@detran.se.gov.br", Email = "maria.santos@detran.se.gov.br", EmailConfirmed = true };
            var res = await userManager.CreateAsync(user3, demoPassword);
            Serilog.Log.Information("Seed user3 creation Succeeded: {Succ}", res.Succeeded);
        }
        else
        {
            if (!user3.EmailConfirmed) { user3.EmailConfirmed = true; await userManager.UpdateAsync(user3); }
        }

        // Organização padrão dos dados de demonstração e respectivas associações.
        var organization = await context.Organizations.IgnoreQueryFilters()
            .Include(x => x.Members)
            .FirstOrDefaultAsync(x => x.Id == DefaultOrganizationId);
        if (organization is null)
        {
            organization = new Organization
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
        }
        else if (organization.Slug == "detran-se"
            || organization.Name.Contains("Detran", StringComparison.OrdinalIgnoreCase))
        {
            organization.Name = "Prisma Demo";
            organization.Slug = "prisma-demo";
            organization.UpdatedAt = DateTimeOffset.UtcNow;
        }

        void EnsureMember(IdentityUser user, OrganizationRole role)
        {
            if (organization.Members.All(x => x.UserId != user.Id))
                organization.Members.Add(OrganizationMember.Create(organization.Id, user.Id, role));
        }

        EnsureMember(user1, OrganizationRole.Administrator);
        EnsureMember(user2, OrganizationRole.ProjectManager);
        EnsureMember(user3, OrganizationRole.TeamMember);
        await context.SaveChangesAsync();

        // Se já existe o board principal de teste, não precisa inserir os dados novamente.
        var boardExists = await context.Boards.IgnoreQueryFilters()
            .AnyAsync(b => b.Name == "Gestão de Infrações e Processos CNH");
        if (boardExists)
        {
            await EnsureProjectWorkflowsAsync(context);
            await EnsureDefaultBoardsAndPlacementsAsync(context);
            return;
        }

        var now = DateTimeOffset.UtcNow;

        // 3. Criar Projeto e Quadro (Board)
        var project = Project.Criar("INFRACOES", "Gestão de Infrações e Processos CNH", user1.Id,
            WorkNature.Project, WorkType.Development,
            "Fluxos de tecnologia e operação para processos de habilitação e infrações.");
        project.OrganizationId = organization.Id;
        await context.Projects.AddAsync(project);
        var board = new Board
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            ProjectId = project.Id,
            Name = "Gestão de Infrações e Processos CNH",
            OwnerId = user1.Id,
            CreatedAt = now.AddDays(-15)
        };
        await context.Boards.AddAsync(board);

        // 4. Criar Colunas (Stages) no Board
        var stage1 = new Stage { Id = Guid.NewGuid(), BoardId = board.Id, Name = "Triagem / Backlog", Category = StageCategory.Backlog, Position = 100, WipLimit = 10, CreatedAt = now.AddDays(-15) };
        var stage2 = new Stage { Id = Guid.NewGuid(), BoardId = board.Id, Name = "Análise Técnica", Category = StageCategory.InProgress, Position = 200, WipLimit = 5, CreatedAt = now.AddDays(-15) };
        var stage3 = new Stage { Id = Guid.NewGuid(), BoardId = board.Id, Name = "Homologação Jurídica", Category = StageCategory.Review, Position = 300, WipLimit = 3, CreatedAt = now.AddDays(-15) };
        var stage4 = new Stage { Id = Guid.NewGuid(), BoardId = board.Id, Name = "Processo Concluído", Category = StageCategory.Done, Position = 400, WipLimit = 15, CreatedAt = now.AddDays(-15) };

        await context.Stages.AddRangeAsync(stage1, stage2, stage3, stage4);

        // 5. Criar Tarefas Principais (WorkItems onde ParentId == null)
        var card1 = new WorkItem
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            StageId = stage2.Id,
            ParentId = null,
            Title = "Instruir Recurso de Multa Suspensão da CNH",
            Subtitle = "Proc. nº 2026-88349",
            Description = "Realizar a análise detalhada das alegações de defesa do condutor infrator e elaborar o relatório de conformidade do processo administrativo de trânsito.",
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
            Title = "Homologar Lote de Placas Mercosul - Ciretran Itabaiana",
            Subtitle = "Lote Mercosul 12-B",
            Description = "Validar a conformidade física e documental dos novos lacres de placas homologados pela credenciada regional.",
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
            Title = "Auditoria Anual de Banco de Dados Renavam",
            Subtitle = "TI / Segurança da Informação",
            Description = "Varredura geral e cruzamento de metadados para integridade da base de dados regional do Renavam Sergipe.",
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
            Title = "Digitalizar Processo Físico do Condutor",
            Subtitle = "Anexar ao dossiê",
            Description = "Digitalizar todas as laudas do recurso físico e subir como anexo no sistema.",
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
            Title = "Parecer da Procuradoria Jurídica",
            Subtitle = "Parecer final",
            Description = "Solicitar posicionamento jurídico sobre a prescrição punitiva.",
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

        // Card 2: Passou por Triagem, Análise Técnica e está em Homologação Jurídica
        var hist3 = new StageHistory { Id = Guid.NewGuid(), WorkItemId = card2.Id, StageId = stage1.Id, EnteredAt = now.AddDays(-8), LeftAt = now.AddDays(-7) };
        var hist4 = new StageHistory { Id = Guid.NewGuid(), WorkItemId = card2.Id, StageId = stage2.Id, EnteredAt = now.AddDays(-7), LeftAt = now.AddDays(-4) };
        var hist5 = new StageHistory { Id = Guid.NewGuid(), WorkItemId = card2.Id, StageId = stage3.Id, EnteredAt = now.AddDays(-4), LeftAt = null };

        // Card 3: Permanece na Triagem desde a criação
        var hist6 = new StageHistory { Id = Guid.NewGuid(), WorkItemId = card3.Id, StageId = stage1.Id, EnteredAt = now.AddDays(-10), LeftAt = null };

        await context.StageHistories.AddRangeAsync(hist1, hist2, hist3, hist4, hist5, hist6);

        // 9. Lançar horas de exemplo (TimeEntry)
        var time1 = TimeEntry.Manual(card1.Id, user1.Id, now.AddDays(-3).AddHours(9), now.AddDays(-3).AddHours(12), "Análise preliminar da defesa prévia.");
        var time2 = TimeEntry.Manual(card1.Id, user2.Id, now.AddDays(-2).AddHours(14), now.AddDays(-2).AddHours(17), "Cruzamento de dados de infração.");
        var time3 = TimeEntry.Manual(card2.Id, user3.Id, now.AddDays(-6).AddHours(8), now.AddDays(-6).AddHours(16), "Conferência geral de laudos regional.");

        await context.TimeEntries.AddRangeAsync(time1, time2, time3);

        project.DefaultBoardId = board.Id;
        var seedItems = new[] { card1, card2, card3, subCard1, subCard2 };
        foreach (var item in seedItems)
        {
            context.WorkItemBoardPlacements.Add(new WorkItemBoardPlacement
            {
                Id = Guid.NewGuid(),
                WorkItemId = item.Id,
                BoardId = board.Id,
                StageId = item.StageId,
                Position = item.Position,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            });
        }

        // 10. Salvar alterações no banco
        await context.SaveChangesAsync();
        await EnsureProjectWorkflowsAsync(context);
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

    /// <summary>
    /// Garante quadro padrão e placements para dados legados/seed anteriores à TASK-029.
    /// </summary>
    private static async Task EnsureDefaultBoardsAndPlacementsAsync(AppDbContext context)
    {
        var projects = await context.Projects.IgnoreQueryFilters()
            .Include(p => p.Boards)
            .Where(p => p.DefaultBoardId == null)
            .ToListAsync();
        foreach (var project in projects)
        {
            var defaultBoard = project.Boards.OrderBy(b => b.CreatedAt).FirstOrDefault();
            if (defaultBoard is not null)
                project.DefaultBoardId = defaultBoard.Id;
        }

        var existingKeys = await context.WorkItemBoardPlacements.IgnoreQueryFilters()
            .Select(p => new { p.WorkItemId, p.BoardId })
            .ToListAsync();
        var existing = existingKeys.Select(x => (x.WorkItemId, x.BoardId)).ToHashSet();

        var items = await context.WorkItems.IgnoreQueryFilters()
            .Where(w => !w.IsArchived)
            .Select(w => new { w.Id, w.BoardId, w.StageId, w.Position, w.CreatedAt, w.UpdatedAt })
            .ToListAsync();
        foreach (var item in items)
        {
            if (existing.Contains((item.Id, item.BoardId))) continue;
            context.WorkItemBoardPlacements.Add(new WorkItemBoardPlacement
            {
                Id = Guid.NewGuid(),
                WorkItemId = item.Id,
                BoardId = item.BoardId,
                StageId = item.StageId,
                Position = item.Position,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            });
        }

        await context.SaveChangesAsync();
    }
}
