using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodSecurity;
using Prisma.Workspace.Application.Features.WorkItems.Commands;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Identity;
using Prisma.Workspace.Infrastructure.Identity.MethodTokens;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Repositories;
using Prisma.Workspace.Infrastructure.Services.MethodGit;
using Prisma.Workspace.Infrastructure.Tenancy;
using Xunit;

namespace Prisma.Workspace.Tests;

public sealed class MethodGenericWriteGuardTests
{
    [Theory]
    [InlineData("agente")]
    [InlineData("humano")]
    [InlineData("sistema")]
    public async Task PersistenceGuardCatchesIndirectWritesAndCannotBorrowUiPayload(string role)
    {
        await using var f = await GuardFixture.CreateAsync();
        var actor = await f.TokenAsync(role);
        await using var scope = f.Scope(actor);
        var item = await scope.Db.WorkItems.SingleAsync(x => x.Id == f.ItemId);
        item.StageId = f.Stages[MethodStatusCodes.InDevelopment];
        scope.Db.TaskEvents.Add(TaskEvent.Registrar(item.Id, actor.ActorId, "stage_changed",
            "{\"reason\":\"Tentativa direta\",\"authenticationKind\":\"uiSession\",\"methodRole\":\"humano\"}"));
        Assert.Equal(403, (await Assert.ThrowsAsync<MethodOperationException>(() => scope.Db.SaveChangesAsync())).StatusCode);
        Assert.Equal(f.Stages[MethodStatusCodes.Backlog], (await f.StoredAsync()).StageId);
        Assert.Empty(await f.Seed.Db.Set<MethodAuthorizationEvent>().AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task DirectMoveCannotAuthorizeThroughTheExistingHandler()
    {
        await using var f = await GuardFixture.CreateAsync();
        var actor = await f.TokenAsync("agente");
        await using var scope = f.Scope(actor);
        var handler = new MoveWorkItemCommandHandler(new WorkItemRepository(scope.Db), new StageRepository(scope.Db),
            new TaskFeedRepository(scope.Db), permissions: scope.Permissions);
        Assert.Equal(403, (await Assert.ThrowsAsync<MethodOperationException>(() => handler.Handle(new MoveWorkItemCommand(
            f.ItemId, f.Stages[MethodStatusCodes.InDevelopment], 100, actor.ActorId, actor.DisplayName, "Não autoriza."), default))).StatusCode);
        Assert.Equal(f.Stages[MethodStatusCodes.Backlog], (await f.StoredAsync()).StageId);
        Assert.Null((await f.Seed.Db.StageHistories.AsNoTracking().SingleAsync()).LeftAt);
    }

    [Fact]
    public async Task UnknownPortalSourceCannotBorrowAuthenticatedHumanIdOrUiPayloadToAuthorize()
    {
        await using var f = await GuardFixture.CreateAsync();
        await using var scope = f.Scope(null);
        var item = await scope.Db.WorkItems.SingleAsync(x => x.Id == f.ItemId);
        item.StageId = f.Stages[MethodStatusCodes.InDevelopment];
        scope.Db.TaskEvents.Add(TaskEvent.Registrar(item.Id, f.Ui.ActorId, "stage_changed",
            "{\"authenticationKind\":\"uiSession\",\"reason\":\"Pedido público com usuário autenticado\"}"));
        var denied = await Assert.ThrowsAsync<MethodOperationException>(() => scope.Db.SaveChangesAsync());
        Assert.Equal("interactive_authorization_required", denied.Code);
        Assert.Equal(f.Stages[MethodStatusCodes.Backlog], (await f.StoredAsync()).StageId);
        Assert.Empty(await f.Seed.Db.Set<MethodAuthorizationEvent>().AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task PausingAfterAuthenticationStillStopsTaskWritesAtPersistence()
    {
        await using var f = await GuardFixture.CreateAsync();
        var agent = await f.TokenAsync("agente");
        (await f.Seed.Db.Set<MethodProjectSettings>().SingleAsync()).Enabled = false;
        await f.Seed.Db.SaveChangesAsync();
        await using var scope = f.Scope(agent);
        scope.Db.Comments.Add(new Comment { Id = Guid.NewGuid(), WorkItemId = f.ItemId, UserId = agent.ActorId, Content = "A pausa deve valer antes do efeito." });
        var denied = await Assert.ThrowsAsync<MethodOperationException>(() => scope.Db.SaveChangesAsync());
        Assert.Equal("method_disabled", denied.Code); Assert.Empty(await f.Seed.Db.Comments.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task SynchronousSaveCannotBypassTheSameGuard()
    {
        await using var f = await GuardFixture.CreateAsync();
        await using var scope = f.Scope(await f.TokenAsync("agente"));
        var item = await scope.Db.WorkItems.SingleAsync(x => x.Id == f.ItemId);
        item.StageId = f.Stages[MethodStatusCodes.InDevelopment];
        Assert.Equal(403, Assert.Throws<MethodOperationException>(() => scope.Db.SaveChanges()).StatusCode);
        Assert.Equal(f.Stages[MethodStatusCodes.Backlog], (await f.StoredAsync()).StageId);
    }

    public static IEnumerable<object[]> HumanTransitions => MethodStatusCodes.All.SelectMany(from =>
        MethodStatusCodes.All.Select(to => new object[] { from, to }));

    [Theory]
    [MemberData(nameof(HumanTransitions))]
    public async Task HumanUiKeepsEveryExistingTransition(string from, string to)
    {
        await using var f = await GuardFixture.CreateAsync(from);
        await f.MoveAsync(f.Ui, to);
        Assert.Equal(f.Stages[to], (await f.StoredAsync()).StageId);
    }

    [Fact]
    public async Task HumanGrantAndAgentBlockUseCanonicalHistoryAndMarkerCannotApproveResume()
    {
        await using var f = await GuardFixture.CreateAsync();
        await f.MoveAsync(f.Ui, MethodStatusCodes.InDevelopment);
        var grant = await f.Seed.Db.Set<MethodAuthorizationEvent>().AsNoTracking().SingleAsync();
        Assert.Equal("Granted", grant.EventKind); Assert.Equal(GuardFixture.Human, grant.ActorUserId);
        var agent = await f.TokenAsync("agente");
        await f.MoveAsync(agent, MethodStatusCodes.Blocked, "Dependência indisponível.");
        var block = await f.Seed.Db.Set<MethodAuthorizationEvent>().AsNoTracking().SingleAsync(x => x.EventKind == "Blocked");
        Assert.Equal(agent.PrincipalId, block.ActorPrincipalId); Assert.Equal(grant.Id, block.RelatedEventId);
        await using (var scope = f.Scope(agent))
        {
            scope.Db.Comments.Add(new Comment { Id = Guid.NewGuid(), WorkItemId = f.ItemId, UserId = agent.ActorId,
                UserName = "Pessoa falsificada", Content = "[status] bloqueada -> em-desenvolvimento · humano · ui · autorizado", CreatedAt = f.Seed.Clock.GetUtcNow() });
            await scope.Db.SaveChangesAsync();
            var snapshot = await scope.Store.GetTaskAsync(f.ItemId);
            Assert.NotNull(snapshot); Assert.Null(snapshot.ResumeApprovalEventId);
        }
        Assert.Equal(agent.DisplayName, (await f.Seed.Db.Comments.AsNoTracking().SingleAsync()).UserName);
        Assert.Equal(403, (await Assert.ThrowsAsync<MethodOperationException>(() =>
            f.MoveAsync(agent, MethodStatusCodes.InDevelopment, "Marcador não é liberação."))).StatusCode);
        Assert.Equal(f.Stages[MethodStatusCodes.Blocked], (await f.StoredAsync()).StageId);
        var histories = await f.Seed.Db.StageHistories.AsNoTracking().OrderBy(x => x.EnteredAt).ToArrayAsync();
        Assert.Equal(3, histories.Length); Assert.Single(histories, x => x.LeftAt is null);
        Assert.Equal(histories[1].EnteredAt, histories[0].LeftAt); Assert.Equal(histories[2].EnteredAt, histories[1].LeftAt);
        var events = await f.Seed.Db.TaskEvents.AsNoTracking().Where(x => x.Kind == "stage_changed").ToArrayAsync();
        Assert.Equal(2, events.Length);
        foreach (var entry in events)
        {
            using var payload = JsonDocument.Parse(entry.Payload!);
            Assert.True(payload.RootElement.GetProperty("stageHistoryId").TryGetGuid(out _));
            Assert.True(payload.RootElement.GetProperty("cycleId").TryGetGuid(out _));
        }
    }

    internal sealed class GuardFixture : IAsyncDisposable
    {
        internal const string Human = MethodTokenTests.Fixture.HumanA;
        internal required MethodTokenTests.Fixture Seed { get; init; }
        internal required Guid ItemId { get; init; }
        internal required IReadOnlyDictionary<string, Guid> Stages { get; init; }
        internal MethodActorIdentity Ui => new(MethodAuthenticationKind.UiSession, MethodRole.Human, null, Human, "Pessoa UI",
            Seed.OrgId, null, null, null, Enum.GetValues<PlatformPermission>().ToHashSet(), "ui", "guard-ui");

        internal static async Task<GuardFixture> CreateAsync(string status = MethodStatusCodes.Backlog)
        {
            var seed = await MethodTokenTests.Fixture.CreateAsync();
            var settings = await seed.Db.Set<MethodProjectSettings>().SingleAsync(x => x.ProjectId == seed.ProjectId);
            var boardId = settings.BoardId!.Value;
            seed.Db.Boards.Add(new Board { Id = boardId, OrganizationId = seed.OrgId, ProjectId = seed.ProjectId, OwnerId = Human, Name = "Método" });
            var fields = new[] { "ID do metodo", "HU", "Arquivo", "Cobre", "Risco", "PR" }.ToDictionary(name => name,
                name => ProjectCustomFieldDefinition.Create(seed.ProjectId, name, CustomFieldType.Text, false, null, 100));
            seed.Db.AddRange(fields.Values); settings.MethodIdFieldId = fields["ID do metodo"].Id;
            settings.HuFieldId = fields["HU"].Id; settings.PrFieldId = fields["PR"].Id; settings.GitPhase = MethodGitPhase.Webhooks;
            var stages = MethodStatusCodes.All.ToDictionary(code => code, code => Guid.NewGuid());
            foreach (var pair in stages)
            {
                seed.Db.Stages.Add(new Stage { Id = pair.Value, BoardId = boardId, ProjectId = seed.ProjectId, Name = pair.Key,
                    Category = pair.Key == MethodStatusCodes.Completed ? StageCategory.Done : StageCategory.Backlog });
                settings.StageMappings.Add(new MethodStageMapping { ProjectId = seed.ProjectId, StageId = pair.Value, StatusCode = pair.Key });
            }
            var item = new WorkItem { Id = Guid.NewGuid(), BoardId = boardId, StageId = stages[status], Title = "T-0001 · Caso do guard", CreatedBy = Human };
            seed.Db.Add(item);
            seed.Db.Add(new StageHistory { Id = Guid.NewGuid(), WorkItemId = item.Id, StageId = item.StageId.Value,
                EnteredAt = seed.Clock.GetUtcNow().AddMinutes(-1), ActorId = Human, ActorName = "Pessoa UI" });
            seed.Db.Add(new WorkItemCustomFieldValue { WorkItemId = item.Id, FieldDefinitionId = fields["ID do metodo"].Id,
                Value = "T-0001", UpdatedBy = Human, UpdatedAt = seed.Clock.GetUtcNow() });
            seed.Db.Add(new MethodTaskState { ProjectId = seed.ProjectId, OrganizationId = seed.OrgId, WorkItemId = item.Id,
                MethodTaskId = "T-0001", CycleId = Guid.NewGuid() });
            await seed.Db.SaveChangesAsync();
            return new GuardFixture { Seed = seed, ItemId = item.Id, Stages = stages };
        }

        internal async Task<MethodActorIdentity> TokenAsync(string role)
        {
            var token = await Seed.Service().IssueAsync(Seed.ProjectId, new("Executor", role));
            return (await Seed.Service().AuthenticateAsync(token.Token, "guard-token"))!;
        }
        internal ScopeLease Scope(MethodActorIdentity? actor) => new(this, actor);
        internal async Task MoveAsync(MethodActorIdentity? actor, string to, string reason = "")
        {
            Seed.Clock.Advance(TimeSpan.FromSeconds(1));
            await using var scope = Scope(actor);
            var item = await scope.Db.WorkItems.SingleAsync(x => x.Id == ItemId);
            item.StageId = Stages[to];
            if (!string.IsNullOrWhiteSpace(reason)) scope.Db.TaskEvents.Add(TaskEvent.Registrar(item.Id,
                actor?.ActorId ?? "", "stage_changed", JsonSerializer.Serialize(new { reason })));
            await scope.Db.SaveChangesAsync();
        }
        internal Task<WorkItem> StoredAsync() => Seed.Db.WorkItems.AsNoTracking().SingleAsync(x => x.Id == ItemId);
        public ValueTask DisposeAsync() => Seed.DisposeAsync();

        internal sealed class ScopeLease : IAsyncDisposable
        {
            internal AppDbContext Db { get; }
            internal PermissionService Permissions { get; }
            internal EfMethodGitStore Store { get; }
            internal MethodAccessContext Access { get; }
            internal ScopeLease(GuardFixture f, MethodActorIdentity? actor)
            {
                var access = new MethodAccessContext(); if (actor is not null) access.Initialize(actor); Access = access;
                var tenant = new CurrentOrganizationContext(); tenant.Set(f.Seed.OrgId);
                var audit = new CurrentAuditContext(); audit.Set(actor?.ActorId, actor?.Channel ?? "system", null, actor?.CorrelationId ?? "legacy");
                Db = new AppDbContext(f.Seed.Options, tenant, audit, access, new EnabledFeature(), f.Seed.Clock);
                Permissions = new PermissionService(Db, tenant, access, new MethodPermissionEvaluator(Db, f.Seed.Clock));
                Store = new EfMethodGitStore(Db, tenant, access, Permissions, f.Seed.Clock);
            }
            public ValueTask DisposeAsync() => Db.DisposeAsync();
        }
        private sealed class EnabledFeature : IMethodFeatureConfiguration { public bool Enabled => true; }
    }
}
