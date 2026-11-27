using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using GuardFixture = Prisma.Workspace.Tests.MethodGenericWriteGuardTests.GuardFixture;

namespace Prisma.Workspace.Tests;

/// <summary>HU-0008, T-0021 (ADR-0106): task do método como Tarefa filha de História do método.</summary>
public sealed class MethodTaskHierarchyTests
{
    private const string Hu8 = "---\nid: HU-0008\ntitulo: \"Documentos\"\nstatus: planejada\n---\n\n# HU-0008\n";

    private static async Task<Guid> StoryAsync(GuardFixture f, string key = "HU-0008")
    {
        await using var scope = f.Scope(await f.TokenAsync("agente"));
        return (await MethodDocumentsTests.Documents(f, scope).SaveAsync(f.Seed.ProjectId, key,
            new(Hu8.Replace("HU-0008", key, StringComparison.Ordinal), 0), default)).WorkItemId;
    }

    [Fact] // CA-4
    public async Task TaskCreatedUnderMethodStoryStaysAFullMethodTask()
    {
        await using var f = await GuardFixture.CreateAsync();
        var storyId = await StoryAsync(f);
        await using var scope = f.Scope(await f.TokenAsync("agente"));
        var tasks = MethodDocumentsTests.Tasks(f, scope);

        var created = await tasks.CreateAsync(f.Seed.ProjectId, new("T-0099", "Filha", "HU-0008", "x.md", ["CA-4"], "baixo"), default);
        var orphan = await tasks.CreateAsync(f.Seed.ProjectId, new("T-0098", "Sem História", "HU-0007", "y.md", [], "baixo"), default);

        Assert.Equal(storyId, (await scope.Db.WorkItems.AsNoTracking().SingleAsync(x => x.Id == created.WorkItemId)).ParentId);
        Assert.Null((await scope.Db.WorkItems.AsNoTracking().SingleAsync(x => x.Id == orphan.WorkItemId)).ParentId);
        Assert.Equal("HU-0008", (await tasks.GetAsync(f.Seed.ProjectId, "T-0099", default)).Hu);
        Assert.Contains(await tasks.ListAsync(f.Seed.ProjectId, null, false, default), x => x.Id == "T-0099");
        Assert.True((await scope.Store.GetTaskAsync(created.WorkItemId))!.IsMethodEligible);

        // Matriz de transições e bloqueio funcionam como numa task de topo.
        var blocked = await tasks.MoveAsync(f.Seed.ProjectId, "T-0099", new(MethodStatusCodes.Blocked, "Aguardando dependência."), default);
        Assert.Equal(MethodStatusCodes.Blocked, blocked.Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<MethodOperationException>(() => tasks.MoveAsync(f.Seed.ProjectId, "T-0098",
            new(MethodStatusCodes.InDevelopment, "Agente não autoriza."), default))).StatusCode);
    }

    [Fact] // CA-4: próximas tasks também acham a filha da História
    public async Task NextTasksIncludeAuthorizedChildOfMethodStory()
    {
        await using var f = await GuardFixture.CreateAsync();
        var storyId = await StoryAsync(f);
        await using (var ui = f.Scope(f.Ui))
        {
            var item = await ui.Db.WorkItems.SingleAsync(x => x.Id == f.ItemId);
            item.ParentId = storyId; await ui.Db.SaveChangesAsync();
        }
        await f.MoveAsync(f.Ui, MethodStatusCodes.InDevelopment);
        await using var scope = f.Scope(await f.TokenAsync("agente"));
        var next = await MethodDocumentsTests.Tasks(f, scope).ListAsync(f.Seed.ProjectId, null, true, default);
        var task = Assert.Single(next);
        Assert.Equal("T-0001", task.Id); Assert.True(task.Authorized);
    }

    [Fact] // CA-5
    public async Task LooseTasksShareOneAvulsasStory()
    {
        await using var f = await GuardFixture.CreateAsync();
        await using var scope = f.Scope(await f.TokenAsync("agente"));
        var tasks = MethodDocumentsTests.Tasks(f, scope);
        var a = await tasks.CreateAsync(f.Seed.ProjectId, new("T-0015", "Avulsa A", "avulsa", "avulsas/tasks/T-0015.md", ["CA-1"], "baixo"), default);
        var b = await tasks.CreateAsync(f.Seed.ProjectId, new("T-0016", "Avulsa B", "avulsa", "avulsas/tasks/T-0016.md", ["CA-1"], "baixo"), default);

        var story = await scope.Db.MethodDocuments.AsNoTracking().Include(x => x.WorkItem).SingleAsync(x => x.MethodKey == "AVULSAS");
        Assert.Equal(WorkItemKind.UserStory, story.WorkItem.Kind); Assert.Null(story.WorkItem.StageId);
        Assert.Equal("Avulsas", story.WorkItem.Title); Assert.Null(story.FilePath);
        var parents = await scope.Db.WorkItems.AsNoTracking().Where(x => x.Id == a.WorkItemId || x.Id == b.WorkItemId).Select(x => x.ParentId).ToListAsync();
        Assert.All(parents, p => Assert.Equal(story.WorkItemId, p));
        Assert.Equal("avulsa", a.Hu); Assert.Equal("avulsa", b.Hu);
        Assert.Equal(400, (await Assert.ThrowsAsync<MethodOperationException>(() => tasks.CreateAsync(f.Seed.ProjectId,
            new("T-0017", "Inválida", "solta", "z.md", [], "baixo"), default))).StatusCode);
    }

    [Theory] // CA-10
    [InlineData(false)]
    [InlineData(true)]
    public async Task RelinkKeepsAuthorizationCycleBlockCommentsAndLinks(bool blocked)
    {
        await using var f = await GuardFixture.CreateAsync();
        var story = await StoryAsync(f);
        var other = await StoryAsync(f, "HU-0009");
        await f.MoveAsync(f.Ui, MethodStatusCodes.InDevelopment);
        if (blocked) await f.MoveAsync(await f.TokenAsync("agente"), MethodStatusCodes.Blocked, "Dependência indisponível.");
        f.Seed.Db.Comments.Add(new Comment { Id = Guid.NewGuid(), WorkItemId = f.ItemId, Content = "Comentário", UserId = f.Ui.ActorId, UserName = "Pessoa UI" });
        await f.Seed.Db.SaveChangesAsync();
        var before = await f.Seed.Db.Set<MethodTaskState>().AsNoTracking().SingleAsync(x => x.WorkItemId == f.ItemId);
        var authorizationEvents = await f.Seed.Db.Set<MethodAuthorizationEvent>().AsNoTracking().CountAsync();
        Assert.NotNull(before.ActiveAuthorizationEventId);

        foreach (var target in new[] { "HU-0008", "HU-0009" })
        {
            await using var scope = f.Scope(await f.TokenAsync("agente"));
            var relinked = await MethodDocumentsTests.Documents(f, scope).RelinkTaskAsync(f.Seed.ProjectId, "T-0001", new(target), default);
            Assert.Equal(target == "HU-0008" ? story : other, relinked.ParentWorkItemId);
        }

        var after = await f.Seed.Db.Set<MethodTaskState>().AsNoTracking().SingleAsync(x => x.WorkItemId == f.ItemId);
        Assert.Equal(before.ActiveAuthorizationEventId, after.ActiveAuthorizationEventId);
        Assert.Equal(before.CycleId, after.CycleId);
        Assert.Equal(before.BlockingEventId, after.BlockingEventId);
        Assert.Equal("T-0001", after.MethodTaskId);
        Assert.Equal(authorizationEvents, await f.Seed.Db.Set<MethodAuthorizationEvent>().AsNoTracking().CountAsync());
        Assert.Single(await f.Seed.Db.Comments.AsNoTracking().Where(x => x.WorkItemId == f.ItemId).ToListAsync());
        var item = await f.StoredAsync();
        Assert.Equal(other, item.ParentId);
        Assert.Equal(f.Stages[blocked ? MethodStatusCodes.Blocked : MethodStatusCodes.InDevelopment], item.StageId);
    }

    [Fact] // CA-10: outra mudança de pai continua sendo troca de identidade
    public async Task MovingUnderAnEpicStillRevokesAuthorization()
    {
        await using var f = await GuardFixture.CreateAsync();
        await f.MoveAsync(f.Ui, MethodStatusCodes.InDevelopment);
        var before = await f.Seed.Db.Set<MethodTaskState>().AsNoTracking().SingleAsync(x => x.WorkItemId == f.ItemId);
        await using (var ui = f.Scope(f.Ui))
        {
            var board = (await f.StoredAsync()).BoardId;
            var epic = new WorkItem { Id = Guid.NewGuid(), BoardId = board, Kind = WorkItemKind.Epic, Title = "Épico qualquer" };
            ui.Db.Add(epic);
            var item = await ui.Db.WorkItems.SingleAsync(x => x.Id == f.ItemId);
            item.ParentId = epic.Id;
            await ui.Db.SaveChangesAsync();
        }
        var after = await f.Seed.Db.Set<MethodTaskState>().AsNoTracking().SingleAsync(x => x.WorkItemId == f.ItemId);
        Assert.Null(after.ActiveAuthorizationEventId);
        Assert.NotEqual(before.CycleId, after.CycleId);
        Assert.Null(after.MethodTaskId);
    }

    [Fact] // CA-11
    public async Task TaskUnderNonMethodStoryEpicOrTaskIsNotEligible()
    {
        await using var f = await GuardFixture.CreateAsync();
        var board = (await f.StoredAsync()).BoardId;
        var plainStory = new WorkItem { Id = Guid.NewGuid(), BoardId = board, Kind = WorkItemKind.UserStory, Title = "História comum" };
        var epic = new WorkItem { Id = Guid.NewGuid(), BoardId = board, Kind = WorkItemKind.Epic, Title = "Épico" };
        await using (var ui = f.Scope(f.Ui))
        {
            ui.Db.AddRange(plainStory, epic);
            await ui.Db.SaveChangesAsync();
        }
        foreach (var parent in new[] { plainStory.Id, epic.Id })
        {
            await using (var ui = f.Scope(f.Ui))
            {
                var item = await ui.Db.WorkItems.SingleAsync(x => x.Id == f.ItemId);
                item.ParentId = parent; await ui.Db.SaveChangesAsync();
            }
            await using var scope = f.Scope(await f.TokenAsync("agente"));
            Assert.Null(await scope.Store.GetTaskAsync(f.ItemId));
            Assert.DoesNotContain(await MethodDocumentsTests.Tasks(f, scope).ListAsync(f.Seed.ProjectId, null, false, default), x => x.Id == "T-0001");
        }
        // O agente não cria task sob item que não é História do método.
        await using (var scope = f.Scope(await f.TokenAsync("agente")))
        {
            var created = await MethodDocumentsTests.Tasks(f, scope).CreateAsync(f.Seed.ProjectId,
                new("T-0050", "Sem História do método", "HU-0050", "x.md", [], "baixo"), default);
            Assert.Null((await scope.Db.WorkItems.AsNoTracking().SingleAsync(x => x.Id == created.WorkItemId)).ParentId);
        }
    }

    [Fact] // CA-4: a regra de elegibilidade é traduzida para SQL (sem avaliação no cliente)
    public async Task Sql_EligibilityExpressionTranslatesAndRuns()
    {
        var value = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(value)) return; // opt-in
        Assert.StartsWith("Prisma_BoardColumns_", new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(value).InitialCatalog);
        var tenant = new Prisma.Workspace.Infrastructure.Tenancy.CurrentOrganizationContext(); tenant.Set(Guid.NewGuid());
        await using var db = new Prisma.Workspace.Infrastructure.Persistence.AppDbContext(
            new DbContextOptionsBuilder<Prisma.Workspace.Infrastructure.Persistence.AppDbContext>().UseSqlServer(value).Options, tenant);
        await db.Database.MigrateAsync();
        var query = db.WorkItems.Where(Prisma.Workspace.Infrastructure.Services.MethodSecurity.MethodEligibility.HasEligibleParent(db));
        Assert.Contains("MethodDocuments", query.ToQueryString());
        Assert.Empty(await query.Take(1).ToListAsync());
    }

    [Fact] // CA-11
    public async Task RelinkFromOtherProjectOrToUnknownStoryChangesNothing()
    {
        await using var f = await GuardFixture.CreateAsync();
        await StoryAsync(f);
        var otherProject = await f.Seed.AddProjectAsync();
        var otherToken = await f.Seed.Service().IssueAsync(otherProject, new("Outro", "agente"));
        var otherActor = (await f.Seed.Service().AuthenticateAsync(otherToken.Token, "outro"))!;
        await using (var other = f.Scope(otherActor))
            Assert.Equal(404, (await Assert.ThrowsAsync<MethodOperationException>(() => MethodDocumentsTests.Documents(f, other)
                .RelinkTaskAsync(f.Seed.ProjectId, "T-0001", new("HU-0008"), default))).StatusCode);
        await using (var scope = f.Scope(await f.TokenAsync("agente")))
        {
            var docs = MethodDocumentsTests.Documents(f, scope);
            Assert.Equal(404, (await Assert.ThrowsAsync<MethodOperationException>(() =>
                docs.RelinkTaskAsync(f.Seed.ProjectId, "T-0001", new("HU-0404"), default))).StatusCode);
            Assert.Equal(400, (await Assert.ThrowsAsync<MethodOperationException>(() =>
                docs.RelinkTaskAsync(f.Seed.ProjectId, "T-0001", new("EPIC-0001"), default))).StatusCode);
        }
        await using (var system = f.Scope(await f.TokenAsync("sistema")))
            Assert.Equal(403, (await Assert.ThrowsAsync<MethodOperationException>(() => MethodDocumentsTests.Documents(f, system)
                .RelinkTaskAsync(f.Seed.ProjectId, "T-0001", new("HU-0008"), default))).StatusCode);
        Assert.Null((await f.StoredAsync()).ParentId);
    }
}
