using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodSecurity;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Identity;
using Prisma.Workspace.Infrastructure.Identity.MethodTokens;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Repositories;
using Prisma.Workspace.Infrastructure.Services.MethodGit;
using Prisma.Workspace.Infrastructure.Services.MethodSecurity;
using Prisma.Workspace.Infrastructure.Tenancy;
using Xunit;

namespace Prisma.Workspace.Tests;

[Collection("MethodGit SQL")]
public sealed class MethodMovementSqlTests(GitDeliverySqlFixture fixture) : IClassFixture<GitDeliverySqlFixture>
{
    [MethodGitSqlFact]
    public async Task WorkerCommitsMovementHistoryAuditFactAndReceiptAsOneUnitAndSemanticReplayAddsNothing()
    {
        await using var c = await fixture.CaseAsync();
        await PrepareDelegation(c);
        await using var scope = new MovementScope(c);
        var request = await Request(c, scope);
        var moved = await scope.Service.MoveAsync(request);
        Assert.True(moved.Applied); Assert.NotNull(moved.TaskEventId); Assert.NotNull(moved.StageHistoryId);
        await using var check = c.NewContext();
        Assert.Equal(request.DestinationStageId, (await check.WorkItems.SingleAsync(x => x.Id == c.ItemId)).StageId);
        var fact = await check.Set<GitAppliedFact>().SingleAsync(x => x.WorkItemId == c.ItemId);
        Assert.Equal(request.DeliveryId, fact.DeliveryId); Assert.Equal(moved.TaskEventId, fact.TaskEventId);
        Assert.Equal(GitDeliveryStates.Processed, (await check.Set<GitWebhookDelivery>().SingleAsync(x => x.Id == request.DeliveryId)).Status);
        var history = await check.StageHistories.SingleAsync(x => x.Id == moved.StageHistoryId);
        Assert.Equal(scope.Actor.ActorId, history.ActorId); Assert.Equal(scope.Actor.DisplayName, history.ActorName);
        var taskEvent = await check.TaskEvents.SingleAsync(x => x.Id == moved.TaskEventId);
        using var payload = JsonDocument.Parse(taskEvent.Payload!);
        Assert.Equal("integrationWorker", payload.RootElement.GetProperty("authenticationKind").GetString());
        Assert.Equal("webhook", payload.RootElement.GetProperty("channel").GetString());
        Assert.Equal("forja-sql-worker", payload.RootElement.GetProperty("correlationId").GetString());
        Assert.Equal(history.Id, payload.RootElement.GetProperty("stageHistoryId").GetGuid());
        var audit = await check.AuditLogs.SingleAsync(x => x.EntityType == "WorkItem" && x.EntityId == c.ItemId.ToString()
            && x.CorrelationId == "forja-sql-worker");
        Assert.Equal(scope.Actor.ActorId, audit.UserId); Assert.Equal("webhook", audit.Origin);

        var second = await Request(c, scope, request.GitFact);
        var repeated = await scope.Service.MoveAsync(second);
        Assert.True(repeated.AlreadyApplied); Assert.False(repeated.Applied);
        Assert.Single(await check.Set<GitAppliedFact>().AsNoTracking().Where(x => x.WorkItemId == c.ItemId).ToArrayAsync());
        Assert.Single(await check.StageHistories.AsNoTracking().Where(x => x.WorkItemId == c.ItemId).ToArrayAsync());
    }

    [MethodGitSqlFact]
    public async Task FailureAfterSqlSaveRollsBackEveryEffectAndWorkerFailureReceiptCannotFlushDirtyMovement()
    {
        await using var c = await fixture.CaseAsync();
        await PrepareDelegation(c);
        var failure = new FailAfterSave();
        await using var scope = new MovementScope(c, failure);
        var request = await Request(c, scope);
        await using var before = c.NewContext();
        var original = (await before.WorkItems.AsNoTracking().SingleAsync(x => x.Id == c.ItemId)).StageId;
        failure.Enabled = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Service.MoveAsync(request));
        Assert.Empty(scope.Db.ChangeTracker.Entries());
        failure.Enabled = false;
        Assert.True(await scope.Store.CompleteDeliveryAsync(request.DeliveryId!.Value, request.ExpectedDeliveryRowVersion!,
            GitDeliveryStates.RetryableFailure, "test_failure", c.Clock.Now.AddMinutes(1)));
        await using var check = c.NewContext();
        Assert.Equal(original, (await check.WorkItems.SingleAsync(x => x.Id == c.ItemId)).StageId);
        Assert.Empty(await check.Set<GitAppliedFact>().Where(x => x.WorkItemId == c.ItemId).ToArrayAsync());
        Assert.Empty(await check.StageHistories.Where(x => x.WorkItemId == c.ItemId).ToArrayAsync());
        Assert.Empty(await check.TaskEvents.Where(x => x.WorkItemId == c.ItemId && x.ActorId == scope.Actor.ActorId).ToArrayAsync());
        Assert.Empty(await check.AuditLogs.Where(x => x.CorrelationId == "forja-sql-worker").ToArrayAsync());
        Assert.Equal(GitDeliveryStates.RetryableFailure, (await check.Set<GitWebhookDelivery>().SingleAsync(x => x.Id == request.DeliveryId)).Status);
    }

    [MethodGitSqlFact]
    public async Task HumanUiDeletesMethodBoardAndTransfersCardsWhilePreservingPermanentProofs()
    {
        await using var c = await fixture.CaseAsync(); await PrepareDelegation(c);
        Guid oldBoard;
        await using (var worker = new MovementScope(c))
        {
            oldBoard = (await worker.Store.GetProjectAsync(c.ProjectId))!.BoardId;
            await worker.Service.MoveAsync(await Request(c, worker));
        }
        var grant = await c.Context.Set<MethodAuthorizationEvent>().AsNoTracking().SingleAsync(x => x.WorkItemId == c.ItemId && x.EventKind == "Granted");
        var board = new Board { Id = Guid.NewGuid(), ProjectId = c.ProjectId, OrganizationId = c.Repository.OrganizationId,
            OwnerId = grant.ActorUserId, Name = "Destino humano preservado" };
        var stage = new Stage { Id = Guid.NewGuid(), BoardId = board.Id, ProjectId = c.ProjectId, Name = "Destino", Category = StageCategory.Ready };
        c.Context.AddRange(board, stage); await c.Context.SaveChangesAsync();
        var ui = new MethodActorIdentity(MethodAuthenticationKind.UiSession, MethodRole.Human, null, grant.ActorUserId!, grant.ActorName,
            c.Repository.OrganizationId, null, null, null, Enum.GetValues<PlatformPermission>().ToHashSet(), "ui", "forja-sql-delete");
        await using (var scope = new MovementScope(c, actor: ui))
        {
            var tenant = new CurrentOrganizationContext(); tenant.Set(c.Repository.OrganizationId);
            var repository = new BoardStructureRepository(scope.Db, new UserDirectory(scope.Db, tenant));
            await repository.DeleteBoardAsync(oldBoard, board.Id, stage.Id, ui.ActorId, default);
        }
        await using var check = c.NewContext();
        Assert.False(await check.Boards.AnyAsync(x => x.Id == oldBoard));
        var settings = await check.Set<MethodProjectSettings>().SingleAsync(x => x.ProjectId == c.ProjectId);
        Assert.Null(settings.BoardId); Assert.False(settings.Enabled);
        var state = await check.Set<MethodTaskState>().SingleAsync(x => x.WorkItemId == c.ItemId && x.ProjectId == c.ProjectId);
        Assert.Null(state.MethodTaskId); Assert.Null(state.ActiveAuthorizationEventId);
        Assert.True(await check.Set<MethodAuthorizationEvent>().AnyAsync(x => x.Id == grant.Id));
        Assert.Single(await check.Set<GitAppliedFact>().Where(x => x.WorkItemId == c.ItemId).ToArrayAsync());
        Assert.Equal(board.Id, (await check.WorkItems.SingleAsync(x => x.Id == c.ItemId)).BoardId);
        Assert.Equal(stage.Id, (await check.WorkItems.SingleAsync(x => x.Id == c.ItemId)).StageId);
    }

    [MethodGitSqlFact]
    public async Task StaleTaskVersionRejectsWithoutConsumingReceipt()
    {
        await using var c = await fixture.CaseAsync(); await PrepareDelegation(c);
        await using var scope = new MovementScope(c);
        var request = await Request(c, scope);
        var bad = request with { ExpectedRowVersion = new byte[8] };
        Assert.Equal(409, (await Assert.ThrowsAsync<MethodOperationException>(() => scope.Service.MoveAsync(bad))).StatusCode);
        Assert.Empty(scope.Db.ChangeTracker.Entries());
        await using var check = c.NewContext();
        Assert.Empty(await check.Set<GitAppliedFact>().Where(x => x.WorkItemId == c.ItemId).ToArrayAsync());
        Assert.Equal(GitDeliveryStates.Processing, (await check.Set<GitWebhookDelivery>().SingleAsync(x => x.Id == request.DeliveryId)).Status);
    }

    private static async Task PrepareDelegation(GitDeliverySqlFixture.SqlCase c)
    {
        var human = (await c.Context.Set<MethodAuthorizationEvent>().SingleAsync(x => x.WorkItemId == c.ItemId)).ActorUserId!;
        (await c.Context.Users.SingleAsync(x => x.Id == human)).EmailConfirmed = true;
        c.Context.OrganizationMembers.Add(new OrganizationMember { UserId = human, OrganizationId = c.Repository.OrganizationId,
            Role = OrganizationRole.Administrator, IsActive = true });
        await c.Context.SaveChangesAsync();
        c.Clock.Now = c.Clock.Now.AddSeconds(1);
    }
    private static async Task<MethodMoveRequest> Request(GitDeliverySqlFixture.SqlCase c, MovementScope scope, VerifiedGitFact? fact = null)
    {
        var receipt = await c.Store(c.Context).AcceptDeliveryAsync(c.Repository.Id, Guid.NewGuid().ToString("D"), SHA256.HashData(Guid.NewGuid().ToByteArray()), c.Event, "forja-sql-worker");
        var delivery = await c.Context.Set<GitWebhookDelivery>().SingleAsync(x => x.Id == receipt.DeliveryId);
        delivery.Status = GitDeliveryStates.Processing; delivery.LeaseUntilUtc = c.Clock.Now.AddMinutes(10);
        await c.Context.SaveChangesAsync();
        var task = (await scope.Store.GetTaskAsync(c.ItemId))!;
        var project = (await scope.Store.GetProjectAsync(c.ProjectId))!;
        fact ??= new VerifiedGitFact(c.Repository.Id, c.ProjectId, c.ItemId, task.CycleId, MethodGitFactCodes.PullRequestOpened,
            "pull:7", "revision", c.Clock.Now, new string('a', 40), null, SHA256.HashData(Guid.NewGuid().ToByteArray()));
        return new(c.ItemId, project.StageMappings[MethodStatusCodes.InReview], "PR aberto comprovado.", GitFact: fact,
            DeliveryId: delivery.Id, ExpectedRowVersion: task.RowVersion, ExpectedDeliveryRowVersion: delivery.RowVersion);
    }
    private sealed class MovementScope : IAsyncDisposable
    {
        public AppDbContext Db { get; }
        public EfMethodGitStore Store { get; }
        public MethodMoveService Service { get; }
        public MethodActorIdentity Actor { get; }
        public MovementScope(GitDeliverySqlFixture.SqlCase c, FailAfterSave? failure = null, MethodActorIdentity? actor = null)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(c.Context.Database.GetConnectionString());
            if (failure is not null) options.AddInterceptors(failure);
            var tenant = new CurrentOrganizationContext(); tenant.Set(c.Repository.OrganizationId);
            var access = new MethodAccessContext();
            Actor = actor ?? new(MethodAuthenticationKind.IntegrationWorker, MethodRole.System, c.Repository.SystemPrincipalId,
                $"method:{c.Repository.SystemPrincipalId:D}", "Integração SQL", c.Repository.OrganizationId, c.ProjectId, null, null,
                new HashSet<PlatformPermission> { PlatformPermission.View, PlatformPermission.Edit, PlatformPermission.ChangeStatus }, "webhook", "forja-sql-worker");
            access.Initialize(Actor);
            var audit = new CurrentAuditContext(); audit.Set(Actor.ActorId, Actor.Channel, null, Actor.CorrelationId);
            Db = new(options.Options, tenant, audit, access, new EnabledFeature(), c.Clock);
            var permissions = new PermissionService(Db, tenant, access, new MethodPermissionEvaluator(Db, c.Clock));
            Store = new(Db, tenant, access, permissions, c.Clock);
            Service = new(Db, access, Store, new WorkflowRepository(Db), permissions, new SilentRealtime(), NullLogger<MethodMoveService>.Instance, c.Clock);
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
            => Enabled ? throw new InvalidOperationException("Falha após SQL, antes do commit.") : ValueTask.FromResult(result);
    }
    private sealed class EnabledFeature : IMethodFeatureConfiguration { public bool Enabled => true; }
    private sealed class SilentRealtime : IBoardRealtimeNotifier
    { public Task BoardChangedAsync(Guid boardId, string changeType, Guid? workItemId = null, CancellationToken ct = default) => Task.CompletedTask; }
}
