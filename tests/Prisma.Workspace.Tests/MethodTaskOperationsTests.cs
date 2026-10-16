using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Application.Features.MethodSecurity;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Repositories;
using Prisma.Workspace.Infrastructure.Services.MethodGit;
using Prisma.Workspace.Infrastructure.Services.MethodSecurity;
using Prisma.Workspace.Infrastructure.Services;
using Prisma.Workspace.Infrastructure.Identity;
using Prisma.Workspace.Infrastructure.Identity.MethodTokens;
using Prisma.Workspace.Infrastructure.Tenancy;
using Xunit;
using GuardFixture = Prisma.Workspace.Tests.MethodGenericWriteGuardTests.GuardFixture;

namespace Prisma.Workspace.Tests;

public sealed class MethodTaskOperationsTests
{
    [Theory]
    [InlineData(MethodStatusCodes.Backlog)]
    [InlineData(MethodStatusCodes.Completed)]
    public async Task AgentCreatesTopLevelTaskWithFieldsAndHuTag(string status)
    {
        await using var f = await GuardFixture.CreateAsync();
        await using var scope = f.Scope(await f.TokenAsync("agente"));
        var tasks = Services(f, scope).Tasks;
        var created = await tasks.CreateAsync(f.Seed.ProjectId, new("T-0002", "Task sem pai", "HU-0001", "tasks/T-0002.md", ["CA-01"], "baixo", status), default);
        var stored = await scope.Db.WorkItems.Include(x => x.CustomFieldValues).Include(x => x.WorkItemTags).ThenInclude(x => x.Tag)
            .SingleAsync(x => x.Id == created.WorkItemId);
        Assert.Null(stored.ParentId); Assert.Equal("HU-0001", created.Hu); Assert.Equal(status, created.Status);
        Assert.Equal("Task sem pai", created.Title); Assert.False(created.Authorized);
        Assert.Equal(5, stored.CustomFieldValues.Count); Assert.Equal("HU-0001", Assert.Single(stored.WorkItemTags).Tag.Name);
        Assert.Contains(await tasks.ListAsync(f.Seed.ProjectId, null, false, default), x => x.WorkItemId == created.WorkItemId);
        Assert.Equal(409, (await Assert.ThrowsAsync<MethodOperationException>(() => tasks.CreateAsync(f.Seed.ProjectId,
            new("T-0002", "Duplicada", "HU-0001", "x.md", [], "baixo", status), default))).StatusCode);
    }

    [Theory]
    [InlineData("agente")]
    [InlineData("humano")]
    public async Task NeitherTokenCreatesAuthorizedDevelopment(string role)
    {
        await using var f = await GuardFixture.CreateAsync();
        await using var scope = f.Scope(await f.TokenAsync(role));
        var tasks = Services(f, scope).Tasks;
        Assert.Equal(403, (await Assert.ThrowsAsync<MethodOperationException>(() => tasks.CreateAsync(f.Seed.ProjectId,
            new("T-0002", "Não autoriza", "HU-0001", "x.md", [], "baixo", MethodStatusCodes.InDevelopment), default))).StatusCode);
        Assert.Single(await f.Seed.Db.WorkItems.AsNoTracking().ToListAsync());
        Assert.Empty(await f.Seed.Db.Set<MethodAuthorizationEvent>().AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task UpdateReplacesHuTagAndKeepsCanonicalStatus()
    {
        await using var f = await GuardFixture.CreateAsync();
        await using var scope = f.Scope(await f.TokenAsync("agente"));
        var tasks = Services(f, scope).Tasks;
        await tasks.CreateAsync(f.Seed.ProjectId, new("T-0002", "Inicial", "HU-0001", "x.md", [], "baixo"), default);
        var updated = await tasks.UpdateAsync(f.Seed.ProjectId, "T-0002", new("Revisada", "HU-0002", "y.md", ["CA-02"], "alto"), default);
        Assert.Equal("HU-0002", updated.Hu); Assert.Equal("Revisada", updated.Title); Assert.Equal(MethodStatusCodes.Backlog, updated.Status);
        var tags = await scope.Db.WorkItemTags.Where(x => x.WorkItemId == updated.WorkItemId).Select(x => x.Tag.Name).ToArrayAsync();
        Assert.Equal(["HU-0002"], tags); Assert.Equal(["CA-02"], updated.Covers);
    }

    [Fact]
    public async Task UnknownDependencyBlocksNextRatherThanDisappearing()
    {
        await using var f = await GuardFixture.CreateAsync();
        await f.MoveAsync(f.Ui, MethodStatusCodes.InDevelopment);
        var item = await f.StoredAsync();
        var other = new WorkItem { Id = Guid.NewGuid(), BoardId = item.BoardId, StageId = f.Stages[MethodStatusCodes.Completed], Title = "Dependência sem ID" };
        f.Seed.Db.Add(other); f.Seed.Db.Add(WorkItemLink.Create(item.Id, other.Id, WorkItemLinkType.DependsOn, f.Ui.ActorId));
        await f.Seed.Db.SaveChangesAsync();
        await using var scope = f.Scope(await f.TokenAsync("agente"));
        var tasks = Services(f, scope).Tasks;
        Assert.Empty(await tasks.ListAsync(f.Seed.ProjectId, null, true, default));
        Assert.Equal(["dependência indisponível"], (await tasks.GetAsync(f.Seed.ProjectId, "T-0001", default)).DependsOn);
    }

    [Fact]
    public async Task OnlyUiApprovesExactOwnBlockAndAgentResumesWithoutNewGrant()
    {
        await using var f = await GuardFixture.CreateAsync();
        await f.MoveAsync(f.Ui, MethodStatusCodes.InDevelopment);
        var agent = await f.TokenAsync("agente");
        await f.MoveAsync(agent, MethodStatusCodes.Blocked, "Dependência indisponível.");
        var state = await f.Seed.Db.Set<MethodTaskState>().AsNoTracking().SingleAsync();
        var request = new ResumeMethodTaskRequest(state.BlockingEventId!.Value, state.CycleId, "Dependência resolvida.");
        foreach (var role in new[] { "humano", "agente", "sistema" })
        {
            await using var denied = f.Scope(await f.TokenAsync(role));
            Assert.Equal(403, (await Assert.ThrowsAsync<MethodOperationException>(() => Services(f, denied).Tasks
                .ApproveResumeAsync(f.Seed.ProjectId, f.ItemId, request, default))).StatusCode);
        }
        await using (var human = f.Scope(f.Ui))
        {
            var tasks = Services(f, human).Tasks;
            Assert.Equal(409, (await Assert.ThrowsAsync<MethodOperationException>(() => tasks.ApproveResumeAsync(f.Seed.ProjectId, f.ItemId,
                request with { BlockingEventId = Guid.NewGuid() }, default))).StatusCode);
            var approved = await tasks.ApproveResumeAsync(f.Seed.ProjectId, f.ItemId, request, default);
            Assert.Equal(approved.EventId, (await tasks.ApproveResumeAsync(f.Seed.ProjectId, f.ItemId, request, default)).EventId);
            Assert.Equal(approved.EventId, (await tasks.GetAsync(f.Seed.ProjectId, "T-0001", default)).ResumeApprovalEventId);
        }
        f.Seed.Clock.Advance(TimeSpan.FromSeconds(1));
        await using (var scope = f.Scope(agent))
        {
            var tasks = Services(f, scope).Tasks;
            Assert.True((await tasks.GetAsync(f.Seed.ProjectId, "T-0001", default)).CanResume);
            var resumed = await tasks.MoveAsync(f.Seed.ProjectId, "T-0001", new(MethodStatusCodes.InDevelopment, "Liberação recebida."), default);
            Assert.Equal(MethodStatusCodes.InDevelopment, resumed.Status); Assert.False(resumed.CanResume);
        }
        Assert.Single(await f.Seed.Db.Set<MethodAuthorizationEvent>().AsNoTracking().Where(x => x.EventKind == "Granted").ToArrayAsync());
        Assert.Single(await f.Seed.Db.Set<MethodAuthorizationEvent>().AsNoTracking().Where(x => x.EventKind == "Resumed").ToArrayAsync());
    }

    [Fact]
    public async Task DedicatedMoveTriggersAutomationWithoutGrantingItHumanAuthority()
    {
        await using var f = await GuardFixture.CreateAsync();
        await f.MoveAsync(f.Ui, MethodStatusCodes.InDevelopment);
        var item = await f.StoredAsync();
        f.Seed.Db.AutomationRules.Add(new AutomationRule { Id = Guid.NewGuid(), BoardId = item.BoardId,
            TriggerStageId = f.Stages[MethodStatusCodes.Blocked], IsActive = true, ActionType = AutomationActionType.MoveToStage,
            ActionValue = f.Stages[MethodStatusCodes.InDevelopment].ToString("D") });
        await f.Seed.Db.SaveChangesAsync();
        var agent = await f.TokenAsync("agente");
        await using var scope = f.Scope(agent);
        var tasks = Services(f, scope, withAutomation: true).Tasks;
        var blocked = await tasks.MoveAsync(f.Seed.ProjectId, "T-0001", new(MethodStatusCodes.Blocked, "Dependência indisponível."), default);
        Assert.Equal(MethodStatusCodes.Blocked, blocked.Status);
        var skipped = Assert.Single(await scope.Db.TaskEvents.Where(x => x.WorkItemId == item.Id && x.Kind == "automation_skipped").ToArrayAsync());
        Assert.Equal(agent.ActorId, skipped.ActorId);
        Assert.Empty(await scope.Db.Set<MethodAuthorizationEvent>().Where(x => x.WorkItemId == item.Id && x.EventKind == "Resumed").ToArrayAsync());
    }

    [Fact]
    public async Task BackfillReportsDuplicatesIncludingArchivedAndNeverChoosesOne()
    {
        await using var f = await GuardFixture.CreateAsync();
        var item = await f.StoredAsync();
        var field = (await f.Seed.Db.Set<MethodProjectSettings>().SingleAsync()).MethodIdFieldId;
        var board = new Board { Id = Guid.NewGuid(), ProjectId = f.Seed.ProjectId, OrganizationId = f.Seed.OrgId, Name = "Outro quadro", OwnerId = f.Ui.ActorId };
        var duplicate = new WorkItem { Id = Guid.NewGuid(), BoardId = board.Id, Title = "Arquivada", IsArchived = true };
        f.Seed.Db.AddRange(board, duplicate, new WorkItemCustomFieldValue { WorkItemId = duplicate.Id, FieldDefinitionId = field, Value = "T-0001" });
        await f.Seed.Db.SaveChangesAsync();
        await using var scope = f.Scope(f.Ui);
        var settings = await scope.Db.Set<MethodProjectSettings>().SingleAsync();
        settings.Enabled = false; await scope.Db.SaveChangesAsync();
        Assert.Null((await scope.Db.Set<MethodTaskState>().SingleAsync()).MethodTaskId);
        var tenant = new CurrentOrganizationContext(); tenant.Set(f.Seed.OrgId);
        var projects = new MethodProjectOperations(scope.Db, scope.Access, tenant, scope.Permissions, scope.Store,
            new MethodProjectProvisioner(scope.Db, tenant, scope.Access, scope.Permissions, f.Seed.Clock));
        var snapshot = await projects.SettingsAsync(f.Seed.ProjectId);
        var issue = Assert.Single(snapshot!.IdentityIssues!);
        Assert.Equal("duplicate_method_id", issue.Code); Assert.Equal("T-0001", issue.Id); Assert.Equal(2, issue.WorkItemIds.Count);
        Assert.Empty(await scope.Db.Set<MethodAuthorizationEvent>().ToArrayAsync());
    }

    [Fact]
    public async Task ReAdoptionDoesNotRetroactivelyAuthorizeDevelopment()
    {
        await using var f = await GuardFixture.CreateAsync(MethodStatusCodes.InDevelopment);
        f.Seed.Db.Set<MethodTaskState>().RemoveRange(await f.Seed.Db.Set<MethodTaskState>().ToArrayAsync());
        await f.Seed.Db.SaveChangesAsync();
        await using var scope = f.Scope(f.Ui);
        var settings = await scope.Db.Set<MethodProjectSettings>().SingleAsync();
        settings.Enabled = false; await scope.Db.SaveChangesAsync();
        var state = await scope.Db.Set<MethodTaskState>().SingleAsync();
        Assert.Equal("T-0001", state.MethodTaskId); Assert.Null(state.ActiveAuthorizationEventId);
        Assert.Empty(await scope.Db.Set<MethodAuthorizationEvent>().ToArrayAsync());
        Assert.Equal(f.Stages[MethodStatusCodes.InDevelopment], (await f.StoredAsync()).StageId);
    }

    private static (MethodTaskOperations Tasks, MethodMoveService Movement) Services(GuardFixture f, GuardFixture.ScopeLease scope, bool withAutomation = false)
    {
        var tenant = new CurrentOrganizationContext(); tenant.Set(f.Seed.OrgId);
        var provisioner = new MethodProjectProvisioner(scope.Db, tenant, scope.Access, scope.Permissions, f.Seed.Clock);
        var project = new MethodProjectOperations(scope.Db, scope.Access, tenant, scope.Permissions, scope.Store, provisioner);
        var movement = new MethodMoveService(scope.Db, scope.Access, scope.Store, new WorkflowRepository(scope.Db), scope.Permissions,
            new SilentRealtime(), NullLogger<MethodMoveService>.Instance, f.Seed.Clock);
        var automation = withAutomation ? new AutomationExecutor(scope.Db, new WorkflowRepository(scope.Db),
            new ProjectAccessService(scope.Db, scope.Permissions, scope.Access, new MethodPermissionEvaluator(scope.Db, f.Seed.Clock)),
            new EnabledFeature(), new MethodTransitionPolicy(scope.Access, scope.Store, scope.Permissions)) : null;
        return (new MethodTaskOperations(scope.Db, scope.Access, scope.Store, project, movement, scope.Permissions, new SilentRealtime(),
            f.Seed.Clock, automation: automation), movement);
    }
    private sealed class SilentRealtime : IBoardRealtimeNotifier
    { public Task BoardChangedAsync(Guid boardId, string changeType, Guid? workItemId = null, CancellationToken ct = default) => Task.CompletedTask; }
    private sealed class EnabledFeature : IMethodFeatureConfiguration { public bool Enabled => true; }
}
