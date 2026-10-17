using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodSecurity;
using Prisma.Workspace.Application.Features.Productivity;
using Prisma.Workspace.Application.Features.ExternalPortal;
using Prisma.Workspace.Application.Features.WorkItemDetails;
using Prisma.Workspace.Application.Features.WorkItems;
using Prisma.Workspace.Application.Features.WorkItems.Commands;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Identity;
using Prisma.Workspace.Infrastructure.Identity.MethodTokens;
using Prisma.Workspace.Infrastructure.Repositories;
using Prisma.Workspace.Infrastructure.Services;
using Prisma.Workspace.Infrastructure.Tenancy;
using Xunit;
using GuardFixture = Prisma.Workspace.Tests.MethodGenericWriteGuardTests.GuardFixture;

namespace Prisma.Workspace.Tests;

public sealed class MethodNominalEntryTests
{
    public static IEnumerable<object[]> Entries => new[] { "create-default-board", "update", "bulk" }
        .SelectMany(path => new[] { "ui", "agente", "humano", "sistema" }.Select(role => new object[] { path, role }));

    [Theory]
    [MemberData(nameof(Entries))]
    public async Task GenericHandlersCannotStartExecutionWithAnyMethodTokenButPreserveUi(string path, string role)
    {
        await using var f = await GuardFixture.CreateAsync();
        var source = await f.StoredAsync();
        (await f.Seed.Db.Projects.SingleAsync(x => x.Id == f.Seed.ProjectId)).DefaultBoardId = source.BoardId;
        await f.Seed.Db.SaveChangesAsync();
        var actor = role == "ui" ? f.Ui : await f.TokenAsync(role);
        await using var scope = f.Scope(actor);
        var (access, users) = Services(f, scope);
        async Task Execute()
        {
            switch (path)
            {
                case "create-default-board":
                    var create = new CreateWorkItemCommandHandler(new WorkItemRepository(scope.Db), new BoardRepository(scope.Db),
                        new StageRepository(scope.Db), new ProjectRepository(scope.Db), new TeamRepository(scope.Db), users, access,
                        scope.Permissions, new WorkflowRepository(scope.Db));
                    await create.Handle(new(Guid.Empty, f.Stages[MethodStatusCodes.InDevelopment], null, "Criação genérica", null, null,
                        Priority.Low, null, null, 100, actor.ActorId, ProjectId: f.Seed.ProjectId), default);
                    break;
                case "update":
                    var update = new UpdateWorkItemCommandHandler(new WorkItemManagementRepository(scope.Db), access, scope.Permissions,
                        new StageRepository(scope.Db), new TeamRepository(scope.Db), users, new TaskFeedRepository(scope.Db));
                    await update.Handle(new(f.ItemId, source.Title, null, source.Kind, f.Stages[MethodStatusCodes.InDevelopment], source.Priority,
                        null, null, WorkItemOrigin.Internal, null, null, null, null, null, null, null, null, null,
                        actor.ActorId, "Nome forjado não autoriza"), default);
                    break;
                case "bulk":
                    var bulk = new BulkWorkItemsCommandHandler(new ProductivityRepository(scope.Db), access, scope.Permissions, users,
                        new WorkflowRepository(scope.Db));
                    await bulk.Handle(new(source.BoardId, [f.ItemId], BulkActionType.Move,
                        f.Stages[MethodStatusCodes.InDevelopment].ToString("D"), null, actor.ActorId), default);
                    break;
            }
        }
        if (role == "ui")
        {
            await Execute();
            if (path == "create-default-board")
                Assert.True(await f.Seed.Db.WorkItems.AsNoTracking().AnyAsync(x => x.Title == "Criação genérica" && x.StageId == f.Stages[MethodStatusCodes.InDevelopment]));
            else
            {
                Assert.Equal(f.Stages[MethodStatusCodes.InDevelopment], (await f.StoredAsync()).StageId);
                Assert.Single(await f.Seed.Db.Set<MethodAuthorizationEvent>().AsNoTracking().Where(x => x.EventKind == "Granted").ToArrayAsync());
            }
        }
        else
        {
            var error = await Record.ExceptionAsync(Execute);
            Assert.True(error is AcessoNegadoException or MethodOperationException { StatusCode: 403 }, error?.ToString() ?? "O token autorizou indevidamente.");
            Assert.Equal(f.Stages[MethodStatusCodes.Backlog], (await f.StoredAsync()).StageId);
            Assert.Single(await f.Seed.Db.WorkItems.AsNoTracking().ToArrayAsync());
            Assert.Empty(await f.Seed.Db.Set<MethodAuthorizationEvent>().AsNoTracking().ToArrayAsync());
        }
    }

    [Theory]
    [InlineData("ui")]
    [InlineData("agente")]
    [InlineData("humano")]
    [InlineData("sistema")]
    public async Task ExternalTriageCannotStartExecutionWithMethodToken(string role)
    {
        await using var f = await GuardFixture.CreateAsync();
        var item = await f.StoredAsync();
        var portal = ExternalPortal.Create(f.Seed.OrgId, f.Seed.ProjectId, item.BoardId, "metodo-triage", true, false, ExternalPortalAccessMode.PublicLink);
        var external = new ExternalRequest { Id = Guid.NewGuid(), ExternalPortalId = portal.Id, WorkItemId = item.Id,
            Protocol = "METHOD-" + Guid.NewGuid().ToString("N")[..12], RequesterEmail = "test@example.invalid", AccessKeyHash = "test-hash" };
        f.Seed.Db.AddRange(portal, external); await f.Seed.Db.SaveChangesAsync();
        var actor = role == "ui" ? f.Ui : await f.TokenAsync(role);
        await using var scope = f.Scope(actor);
        var (access, users) = Services(f, scope);
        var handler = new ApplyExternalRequestTriageCommandHandler(new ExternalPortalRepository(scope.Db), new ProjectRepository(scope.Db), access, users, null!);
        var request = new ApplyExternalRequestTriageCommand(external.Protocol, ExternalRequestTriageAction.SentToKanban,
            "Encaminhar para execução.", null, null, null, null, null, null, null, f.Stages[MethodStatusCodes.InDevelopment], null, null,
            actor.ActorId, "Nome informado não autoriza");
        if (role == "ui")
        {
            await handler.Handle(request, default);
            Assert.Equal(f.Stages[MethodStatusCodes.InDevelopment], (await f.StoredAsync()).StageId);
            Assert.Single(await f.Seed.Db.Set<MethodAuthorizationEvent>().AsNoTracking().Where(x => x.EventKind == "Granted").ToArrayAsync());
        }
        else
        {
            var error = await Record.ExceptionAsync(() => handler.Handle(request, default));
            Assert.True(error is AcessoNegadoException or MethodOperationException { StatusCode: 403 }, error?.ToString() ?? "Triagem autorizou indevidamente.");
            Assert.Equal(f.Stages[MethodStatusCodes.Backlog], (await f.StoredAsync()).StageId);
            Assert.Empty(await f.Seed.Db.Set<MethodAuthorizationEvent>().AsNoTracking().ToArrayAsync());
        }
    }

    [Theory]
    [InlineData("ui")]
    [InlineData("agente")]
    [InlineData("humano")]
    [InlineData("sistema")]
    public async Task AutomationKeepsInitiatingPrincipalForEveryStep(string role)
    {
        await using var f = await GuardFixture.CreateAsync();
        var item = await f.StoredAsync();
        f.Seed.Db.AutomationRules.Add(new AutomationRule { Id = Guid.NewGuid(), BoardId = item.BoardId,
            TriggerStageId = f.Stages[MethodStatusCodes.Backlog], IsActive = true, ActionType = AutomationActionType.MoveToStage,
            ActionValue = f.Stages[MethodStatusCodes.InDevelopment].ToString("D") });
        f.Seed.Db.AutomationRules.Add(new AutomationRule { Id = Guid.NewGuid(), BoardId = item.BoardId,
            TriggerStageId = f.Stages[MethodStatusCodes.InDevelopment], IsActive = true, ActionType = AutomationActionType.MoveToStage,
            ActionValue = f.Stages[MethodStatusCodes.InReview].ToString("D") });
        await f.Seed.Db.SaveChangesAsync();
        var actor = role == "ui" ? f.Ui : await f.TokenAsync(role);
        await using var scope = f.Scope(actor);
        var (access, _) = Services(f, scope);
        var executor = new AutomationExecutor(scope.Db, new WorkflowRepository(scope.Db), access, new EnabledFeature(),
            new MethodTransitionPolicy(scope.Access, scope.Store, scope.Permissions));
        await executor.ExecuteStageEnteredAsync(item.Id, item.StageId!.Value, actor.ActorId);
        var events = await f.Seed.Db.TaskEvents.AsNoTracking().ToArrayAsync();
        if (role == "ui")
        {
            Assert.Equal(f.Stages[MethodStatusCodes.InReview], (await f.StoredAsync()).StageId);
            Assert.Equal(2, events.Count(x => x.Kind == "stage_changed"));
            Assert.Single(await f.Seed.Db.Set<MethodAuthorizationEvent>().AsNoTracking().Where(x => x.EventKind == "Granted").ToArrayAsync());
        }
        else
        {
            Assert.Equal(f.Stages[MethodStatusCodes.Backlog], (await f.StoredAsync()).StageId);
            Assert.Single(events, x => x.Kind == "automation_skipped");
            Assert.DoesNotContain(events, x => x.Kind == "stage_changed");
            Assert.Empty(await f.Seed.Db.Set<MethodAuthorizationEvent>().AsNoTracking().ToArrayAsync());
        }
        Assert.All(events, row => Assert.Equal(actor.ActorId, row.ActorId));
    }

    [Theory]
    [InlineData("ui")]
    [InlineData("agente")]
    public async Task DuplicateDoesNotCopyStableIdentityAndDoesNotGiveAgentExecution(string role)
    {
        await using var f = await GuardFixture.CreateAsync(MethodStatusCodes.InDevelopment);
        var actor = role == "ui" ? f.Ui : await f.TokenAsync(role);
        await using var scope = f.Scope(actor);
        var (access, _) = Services(f, scope);
        var handler = new DuplicateWorkItemCommandHandler(new WorkItemManagementRepository(scope.Db), new WorkItemRepository(scope.Db),
            access, scope.Permissions, new TaskFeedRepository(scope.Db), scope.Store, new EnabledFeature());
        if (role == "ui")
        {
            var id = await handler.Handle(new(f.ItemId, actor.ActorId), default);
            var item = await f.Seed.Db.WorkItems.AsNoTracking().Include(x => x.CustomFieldValues).SingleAsync(x => x.Id == id);
            Assert.Equal(f.Stages[MethodStatusCodes.InDevelopment], item.StageId); Assert.Empty(item.CustomFieldValues);
            Assert.Single(await f.Seed.Db.Set<MethodTaskState>().AsNoTracking().ToArrayAsync());
        }
        else
        {
            Assert.Equal(403, (await Assert.ThrowsAsync<MethodOperationException>(() => handler.Handle(new(f.ItemId, actor.ActorId), default))).StatusCode);
            Assert.Single(await f.Seed.Db.WorkItems.AsNoTracking().ToArrayAsync());
        }
        Assert.Empty(await f.Seed.Db.Set<MethodAuthorizationEvent>().AsNoTracking().ToArrayAsync());
    }

    [Theory]
    [InlineData("ui")]
    [InlineData("agente")]
    public async Task DetailWritesKeepStatusAndDoNotBorrowIdentityAdministration(string role)
    {
        await using var f = await GuardFixture.CreateAsync();
        var actor = role == "ui" ? f.Ui : await f.TokenAsync(role);
        await using var scope = f.Scope(actor);
        var (access, _) = Services(f, scope);
        var itemAccess = new WorkItemAccessService(scope.Db, access, scope.Permissions);
        var details = new WorkItemDetailsRepository(scope.Db);
        await new SetDescriptionCommandHandler(details, itemAccess).Handle(new(f.ItemId, "Descrição revisada", actor.ActorId), default);
        await new SetTaxonomyCommandHandler(details, itemAccess).Handle(new(f.ItemId, null, 3, [], actor.ActorId), default);
        var item = await f.StoredAsync();
        Assert.Equal("Descrição revisada", item.Description); Assert.Equal(3, item.Points);
        Assert.Equal(f.Stages[MethodStatusCodes.Backlog], item.StageId);
        Assert.Empty(await f.Seed.Db.Set<MethodAuthorizationEvent>().AsNoTracking().ToArrayAsync());
    }

    private static (ProjectAccessService Access, UserDirectory Users) Services(GuardFixture f, GuardFixture.ScopeLease scope)
    {
        var tenant = new CurrentOrganizationContext(); tenant.Set(f.Seed.OrgId);
        return (new(scope.Db, scope.Permissions, scope.Access, new MethodPermissionEvaluator(scope.Db, f.Seed.Clock)), new(scope.Db, tenant));
    }
    private sealed class EnabledFeature : IMethodFeatureConfiguration { public bool Enabled => true; }
}
