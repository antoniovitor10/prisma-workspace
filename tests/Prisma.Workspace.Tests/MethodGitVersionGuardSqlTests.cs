using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Identity.MethodTokens;
using Xunit;
using MovementScope = Prisma.Workspace.Tests.MethodMovementSqlTests.MovementScope;

namespace Prisma.Workspace.Tests;

[Collection("MethodGit SQL")]
public sealed class MethodGitVersionGuardSqlTests(GitDeliverySqlFixture fixture) : IClassFixture<GitDeliverySqlFixture>
{
    [MethodGitSqlFact]
    public async Task RealGuardAllowsWorkerInferredLinkCasWithoutChangingAuthorizationOrStage()
    {
        await using var c = await fixture.CaseAsync();
        await MethodMovementSqlTests.PrepareDelegation(c);
        await using var scope = new MovementScope(c);
        var before = (await scope.Store.GetTaskAsync(c.ItemId))!;
        var pr = new GitPullRequestSnapshot(7, "https://github.com/qa/metodo/pull/7", "Task T-0001", "task/T-0001-x", "main",
            new string('b', 40), null, "open", false, false, c.Now, c.Now, HeadRepositoryId: c.Repository.RepositoryId);
        var observation = await scope.Store.UpsertObservationAsync(c.Repository.Id, pr, null);
        await scope.Store.LinkInferredAsync(c.Repository.Id, c.ItemId, observation.Id, pr.HeadBranch, "branch");
        var after = (await scope.Store.GetTaskAsync(c.ItemId))!;
        Assert.False(before.RowVersion.SequenceEqual(after.RowVersion));
        Assert.Equal(before.ActiveAuthorizationEventId, after.ActiveAuthorizationEventId);
        Assert.Equal(before.CycleId, after.CycleId); Assert.Equal(before.StageId, after.StageId);
        Assert.Single(await scope.Db.Set<TaskGitLink>().Where(x => x.WorkItemId == c.ItemId).ToArrayAsync());
    }

    [MethodGitSqlFact]
    public async Task RealGuardAllowsAgentExplicitLinkCasAndCommitsProjectionCommentAndInbox()
    {
        await using var c = await fixture.CaseAsync();
        await MethodMovementSqlTests.PrepareDelegation(c);
        var grant = await c.Context.Set<MethodAuthorizationEvent>().AsNoTracking().SingleAsync(x => x.WorkItemId == c.ItemId);
        var ui = new MethodActorIdentity(MethodAuthenticationKind.UiSession, MethodRole.Human, null, grant.ActorUserId!, grant.ActorName,
            c.Repository.OrganizationId, null, null, null, Enum.GetValues<PlatformPermission>().ToHashSet(), "ui", "agent-link-ui");
        MethodActorIdentity agent;
        Guid observationId;
        await using (var scope = new MovementScope(c, actor: ui))
        {
            var tokens = new MethodTokenService(scope.Db, scope.Access, scope.Permissions,
                new MethodPermissionEvaluator(scope.Db, c.Clock), c.Clock);
            var issued = await tokens.IssueAsync(c.ProjectId, new("Agent SQL", "agente"));
            agent = (await tokens.AuthenticateAsync(issued.Token, "agent-link-sql"))!;
            var pr = new GitPullRequestSnapshot(8, "https://github.com/qa/metodo/pull/8", "Título livre", "feature/livre", "main",
                new string('b', 40), null, "open", false, false, c.Now, c.Now, HeadRepositoryId: c.Repository.RepositoryId);
            observationId = (await scope.Store.UpsertObservationAsync(c.Repository.Id, pr, "legacy_confirmation_required")).Id;
        }
        await using var agentScope = new MovementScope(c, actor: agent);
        var before = (await agentScope.Store.GetTaskAsync(c.ItemId))!;
        await agentScope.Store.LinkExplicitAsync(c.Repository.Id, c.ItemId, observationId, "Vínculo autorizado do agente.");
        var after = (await agentScope.Store.GetTaskAsync(c.ItemId))!;
        Assert.False(before.RowVersion.SequenceEqual(after.RowVersion));
        Assert.Equal(before.ActiveAuthorizationEventId, after.ActiveAuthorizationEventId);
        Assert.Equal(before.CycleId, after.CycleId); Assert.Equal(before.StageId, after.StageId);
        Assert.Single(await agentScope.Db.Set<TaskGitLink>().Where(x => x.WorkItemId == c.ItemId && x.Source == "explicit").ToArrayAsync());
        Assert.Single(await agentScope.Db.Comments.Where(x => x.WorkItemId == c.ItemId && x.UserId == agent.ActorId).ToArrayAsync());
        Assert.Contains("/pull/8", await agentScope.Db.WorkItemCustomFieldValues.Where(x => x.WorkItemId == c.ItemId && x.FieldDefinitionId == c.PrFieldId).Select(x => x.Value).SingleAsync());
        Assert.Single(await agentScope.Db.Set<GitWebhookDelivery>().Where(x => x.ProjectId == c.ProjectId).ToArrayAsync());
    }

    [MethodGitSqlFact]
    public async Task ConcurrentLinkInvalidatesStaleVersionPulseWithoutChangingAuthority()
    {
        await using var c = await fixture.CaseAsync();
        await MethodMovementSqlTests.PrepareDelegation(c);
        await using var first = new MovementScope(c);
        await using var stale = new MovementScope(c);
        var previous = await stale.Db.Set<MethodTaskState>().SingleAsync(x => x.WorkItemId == c.ItemId);
        stale.Db.Entry(previous).Property(x => x.MethodTaskId).IsModified = true;
        var pr = new GitPullRequestSnapshot(9, "https://github.com/qa/metodo/pull/9", "Task T-0001", "task/T-0001-y", "main",
            new string('c', 40), null, "open", false, false, c.Now, c.Now, HeadRepositoryId: c.Repository.RepositoryId);
        var observation = await first.Store.UpsertObservationAsync(c.Repository.Id, pr, null);
        await first.Store.LinkInferredAsync(c.Repository.Id, c.ItemId, observation.Id, pr.HeadBranch, "branch");
        var conflict = await Assert.ThrowsAsync<MethodOperationException>(() => stale.Db.SaveChangesAsync());
        Assert.Equal(409, conflict.StatusCode); Assert.Equal("stale_task", conflict.Code);
        var current = await first.Db.Set<MethodTaskState>().AsNoTracking().SingleAsync(x => x.WorkItemId == c.ItemId);
        Assert.Equal(previous.CycleId, current.CycleId); Assert.Equal(previous.ActiveAuthorizationEventId, current.ActiveAuthorizationEventId);
        Assert.False(previous.RowVersion.SequenceEqual(current.RowVersion));
    }
}
