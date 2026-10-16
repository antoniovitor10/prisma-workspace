using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.Reports;
using Prisma.Workspace.Application.Features.TaskFeed.Queries;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Identity;
using Prisma.Workspace.Infrastructure.Identity.MethodTokens;
using Prisma.Workspace.Infrastructure.Repositories;
using Prisma.Workspace.Infrastructure.Tenancy;
using Xunit;
using GuardFixture = Prisma.Workspace.Tests.MethodGenericWriteGuardTests.GuardFixture;

namespace Prisma.Workspace.Tests;

public sealed class MethodPrincipalPresentationTests
{
    [Fact]
    public async Task InactiveMethodPrincipalKeepsDisplayNameWithoutBecomingAssignableHuman()
    {
        await using var f = await GuardFixture.CreateAsync();
        var agent = await f.TokenAsync("agente");
        (await f.Seed.Db.Set<MethodPrincipal>().SingleAsync(x => x.Id == agent.PrincipalId)).IsActive = false;
        await f.Seed.Db.SaveChangesAsync();
        await using var scope = f.Scope(f.Ui);
        var tenant = new CurrentOrganizationContext(); tenant.Set(f.Seed.OrgId);
        var users = new UserDirectory(scope.Db, tenant);
        var names = await users.GetDisplayNamesAsync([agent.ActorId, f.Ui.ActorId]);
        Assert.Equal(agent.DisplayName, names[agent.ActorId]); Assert.False(string.IsNullOrWhiteSpace(names[f.Ui.ActorId]));
        Assert.DoesNotContain(await users.GetAllAsync(), x => x.Id == agent.ActorId);
        Assert.Empty(await users.GetByIdsAsync([agent.ActorId], includeInactive: true));
        Assert.False(await scope.Db.Users.AnyAsync(x => x.Id == agent.ActorId));
    }

    [Fact]
    public async Task CommentFeedGraphAndProductivityPreserveRealMethodAuthorship()
    {
        await using var f = await GuardFixture.CreateAsync();
        await f.MoveAsync(f.Ui, MethodStatusCodes.InDevelopment);
        var agent = await f.TokenAsync("agente");
        await f.MoveAsync(agent, MethodStatusCodes.Blocked, "Dependência indisponível.");
        await using (var author = f.Scope(agent))
        {
            author.Db.Comments.Add(new Comment { Id = Guid.NewGuid(), WorkItemId = f.ItemId, UserId = agent.ActorId,
                UserName = "Emissor humano não deve aparecer", Content = "Bloqueio documentado.", CreatedAt = f.Seed.Clock.GetUtcNow() });
            await author.Db.SaveChangesAsync();
        }
        await using var scope = f.Scope(f.Ui);
        var access = new ProjectAccessService(scope.Db, scope.Permissions, scope.Access, new MethodPermissionEvaluator(scope.Db, f.Seed.Clock));
        var workAccess = new WorkItemAccessService(scope.Db, access, scope.Permissions);
        var feed = new TaskFeedRepository(scope.Db);
        var comments = await new GetCommentsQueryHandler(feed, workAccess).Handle(new(f.ItemId, f.Ui.ActorId), default);
        var comment = Assert.Single(comments); Assert.Equal(agent.ActorId, comment.UserId); Assert.Equal(agent.DisplayName, comment.DisplayName);
        var graph = await new GetTaskStateGraphQueryHandler(feed, workAccess).Handle(new(f.ItemId, f.Ui.ActorId), default);
        var block = Assert.Single(graph.Edges, x => x.ActorId == agent.ActorId);
        Assert.Equal(agent.DisplayName, block.ActorName); Assert.Equal("Dependência indisponível.", block.Reason);
        var item = Assert.Single(await new ProductivityRepository(scope.Db).GetItemsAsync((await f.StoredAsync()).BoardId, [f.ItemId]));
        Assert.Contains(item.StageHistories, x => x.ActorId == agent.ActorId && x.ActorName == agent.DisplayName);
        var events = await new GetTaskEventsQueryHandler(feed, workAccess).Handle(new(f.ItemId, f.Ui.ActorId), default);
        Assert.Contains(events, x => x.ActorId == agent.ActorId && x.Payload!.Contains(agent.DisplayName, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TimeReportResolvesMethodAuthorAndKeepsLegacyHumanRows()
    {
        await using var f = await GuardFixture.CreateAsync();
        var agent = await f.TokenAsync("agente");
        var now = DateTimeOffset.UtcNow.AddHours(-1);
        f.Seed.Db.TimeEntries.AddRange(TimeEntry.Manual(f.ItemId, agent.ActorId, now.AddHours(-1), now, "Registro de autoria anterior"),
            TimeEntry.Manual(f.ItemId, f.Ui.ActorId, now.AddMinutes(-30), now, "Pessoa legada"));
        await f.Seed.Db.SaveChangesAsync();
        await using var scope = f.Scope(f.Ui);
        var tenant = new CurrentOrganizationContext(); tenant.Set(f.Seed.OrgId);
        var users = new UserDirectory(scope.Db, tenant);
        var report = await new ReportExecutor(new AnalyticsRepository(scope.Db), users, new ProjectRepository(scope.Db)).RunAsync(
            f.Seed.ProjectId, ReportDataSource.TimeEntries, ReportVisualization.Table,
            new(["userId", "user", "hours"], [], [], null, null, false, null, null), default);
        Assert.Equal(2, report.Rows.Count);
        var method = Assert.Single(report.Rows, x => Equals(x["userId"], agent.ActorId));
        Assert.Equal(agent.DisplayName, method["user"]); Assert.Equal(1m, method["hours"]);
        Assert.Contains(report.Rows, x => Equals(x["userId"], f.Ui.ActorId) && x["user"] is string name && !string.IsNullOrWhiteSpace(name));
        Assert.DoesNotContain(await users.GetAllAsync(), x => x.Id == agent.ActorId);
    }
}
