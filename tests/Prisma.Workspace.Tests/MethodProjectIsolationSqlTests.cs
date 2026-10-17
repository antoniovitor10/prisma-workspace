using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Prisma.Workspace.Application.Features.MethodSecurity;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Identity.MethodTokens;
using Prisma.Workspace.Infrastructure.Identity;
using Prisma.Workspace.Infrastructure.Repositories;
using Prisma.Workspace.Infrastructure.Services;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Tenancy;

namespace Prisma.Workspace.Tests;

[Collection("MethodGit SQL")]
public sealed class MethodProjectIsolationSqlTests(GitDeliverySqlFixture fixture) : IClassFixture<GitDeliverySqlFixture>
{
    [MethodGitSqlFact]
    public async Task GlobalFlagPreservesIsolationAndLegacyStageForUnconfiguredProjectsButProtectsMethodProject()
    {
        await using var c = await fixture.CaseAsync();
        var owner = (await c.Context.Projects.SingleAsync(x => x.Id == c.ProjectId)).OwnerId;
        var project = Project.Criar("I" + Guid.NewGuid().ToString("N")[..8], "Fluxo humano sem método", owner,
            WorkNature.Project, WorkType.Development);
        project.OrganizationId = c.Repository.OrganizationId;
        var board = new Board { Id = Guid.NewGuid(), ProjectId = project.Id, OrganizationId = project.OrganizationId,
            OwnerId = owner, Name = "Quadro normal" };
        var legacyStage = new Stage { Id = Guid.NewGuid(), ProjectId = project.Id, BoardId = null, Name = "Coluna legada" };
        var destination = new Stage { Id = Guid.NewGuid(), ProjectId = project.Id, BoardId = board.Id, Name = "Em execução" };
        var item = new WorkItem { Id = Guid.NewGuid(), BoardId = board.Id, StageId = legacyStage.Id, Title = "Editável" };
        var workflow = WorkflowStatus.Create(project.Id, "Em execução", "#64748B", 100, StageCategory.InProgress, false, false);
        var final = new Stage { Id = Guid.NewGuid(), ProjectId = project.Id, BoardId = board.Id, Name = "Final", Category = StageCategory.Done };
        var rule = new AutomationRule { Id = Guid.NewGuid(), BoardId = board.Id, TriggerStageId = destination.Id,
            IsActive = true, ActionType = AutomationActionType.MoveToStage, ActionValue = final.Id.ToString("D") };
        c.Context.AddRange(project, board, legacyStage, destination, final, workflow, item, rule); await c.Context.SaveChangesAsync();
        var probe = new Transactions(); var saves = new Saves();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(c.Context.Database.GetConnectionString()).AddInterceptors(probe, saves).Options;
        var tenant = new CurrentOrganizationContext(); tenant.Set(project.OrganizationId);
        await using var db = new AppDbContext(options, tenant, methodContext: new MethodAccessContext(), methodFeature: new EnabledFeature());
        var card = await db.WorkItems.SingleAsync(x => x.Id == item.Id);
        card.Title = "Editado sem alterar a coluna legada";
        await db.SaveChangesAsync();
        Assert.DoesNotContain(IsolationLevel.Serializable, probe.Levels);
        probe.Levels.Clear();
        // Mesmo Save misto não aplica regras de coluna do método ao card legado.
        card.Title = "Mesmo lote de um projeto com método";
        var methodCard = await db.WorkItems.SingleAsync(x => x.Id == c.ItemId);
        methodCard.Title = "Método mantém proteção transacional";
        await db.SaveChangesAsync();
        Assert.Contains(IsolationLevel.Serializable, probe.Levels);
        probe.Levels.Clear();
        card.StageId = destination.Id;
        await db.SaveChangesAsync();
        Assert.DoesNotContain(IsolationLevel.Serializable, probe.Levels);
        Assert.Empty(await db.Set<MethodTaskState>().Where(x => x.ProjectId == project.Id).ToArrayAsync());
        probe.Levels.Clear();
        methodCard.Title = "Método continua isolado no Save seguinte";
        await db.SaveChangesAsync();
        Assert.Contains(IsolationLevel.Serializable, probe.Levels);
        var repository = new WorkflowRepository(db, new EnabledFeature());
        await repository.SynchronizeStageWorkItemsAsync(destination.Id, workflow.Id);
        await using var check = c.NewContext();
        Assert.True(await check.WorkItems.AsNoTracking().AnyAsync(x => x.Id == item.Id && x.WorkflowStatusId == workflow.Id));
        db.ChangeTracker.Clear(); saves.Count = 0; probe.Levels.Clear();
        var permissions = new PermissionService(db, tenant);
        var executor = new AutomationExecutor(db, repository, new ProjectAccessService(db, permissions), new EnabledFeature());
        await executor.ExecuteStageEnteredAsync(item.Id, destination.Id, owner);
        Assert.Equal(1, saves.Count); Assert.DoesNotContain(IsolationLevel.Serializable, probe.Levels);
        Assert.True(await check.WorkItems.AsNoTracking().AnyAsync(x => x.Id == item.Id && x.StageId == final.Id && x.CompletedAt != null));
        Assert.Single(await check.TaskEvents.Where(x => x.WorkItemId == item.Id && x.Kind == "automation_executed").ToArrayAsync());
        Assert.Empty(await check.TaskEvents.Where(x => x.WorkItemId == item.Id && x.Kind == "stage_changed").ToArrayAsync());
    }

    [Fact]
    public async Task PausedProjectStillRetiresAuthorizationAfterHumanMovementBeforeReactivation()
    {
        await using var f = await MethodGenericWriteGuardTests.GuardFixture.CreateAsync();
        await f.MoveAsync(f.Ui, MethodStatusCodes.InDevelopment);
        var grant = (await f.Seed.Db.Set<MethodTaskState>().AsNoTracking().SingleAsync()).ActiveAuthorizationEventId;
        (await f.Seed.Db.Set<MethodProjectSettings>().SingleAsync()).Enabled = false;
        await f.Seed.Db.SaveChangesAsync();
        await f.MoveAsync(f.Ui, MethodStatusCodes.Backlog);
        (await f.Seed.Db.Set<MethodProjectSettings>().SingleAsync()).Enabled = true;
        await f.Seed.Db.SaveChangesAsync();
        Assert.Null((await f.Seed.Db.Set<MethodTaskState>().AsNoTracking().SingleAsync()).ActiveAuthorizationEventId);
        Assert.True(await f.Seed.Db.Set<MethodAuthorizationEvent>().AnyAsync(x => x.Id == grant));
        Assert.Single(await f.Seed.Db.Set<MethodAuthorizationEvent>().Where(x => x.EventKind == "Revoked").ToArrayAsync());
    }

    private sealed class Saves : SaveChangesInterceptor
    {
        internal int Count { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { Count++; return ValueTask.FromResult(result); }
    }

    private sealed class EnabledFeature : IMethodFeatureConfiguration { public bool Enabled => true; }
    private sealed class Transactions : DbTransactionInterceptor
    {
        internal List<IsolationLevel> Levels { get; } = [];
        public override ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection connection,
            TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
        {
            Levels.Add(eventData.IsolationLevel);
            return ValueTask.FromResult(result);
        }
    }
}
