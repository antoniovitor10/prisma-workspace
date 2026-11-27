using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Workspace.Application.Features.MethodDocuments;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodSecurity;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Identity;
using Prisma.Workspace.Infrastructure.Identity.MethodTokens;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Repositories;
using Prisma.Workspace.Infrastructure.Services.MethodDocuments;
using Prisma.Workspace.Infrastructure.Services.MethodGit;
using Prisma.Workspace.Infrastructure.Services.MethodSecurity;
using Prisma.Workspace.Infrastructure.Tenancy;
using GuardFixture = Prisma.Workspace.Tests.MethodGenericWriteGuardTests.GuardFixture;

namespace Prisma.Workspace.Tests;

/// <summary>HU-0008, T-0020: documentos do método (dados, versões, permissões e API).</summary>
public sealed class MethodDocumentsTests
{
    internal const string Hu1 = "---\nid: HU-0001\ntitulo: \"Primeira HU\"\nstatus: aberta\n---\n\n# HU-0001 · Primeira HU\n\n## História\n\nComo pessoa, quero algo.\n\n| A | B |\n|---|---|\n| 1 | 2 |\n";

    [Fact] // CA-1
    public async Task AgentCreatesStoryWithVersionedDocumentAndImmutableRevisions()
    {
        await using var f = await GuardFixture.CreateAsync();
        await using var scope = f.Scope(await f.TokenAsync("agente"));
        var docs = Documents(f, scope);

        var v1 = await docs.SaveAsync(f.Seed.ProjectId, "HU-0001", new(Hu1, 0, "docs/trabalho/HU-0001-x/hu.md"), default);

        var story = await scope.Db.WorkItems.AsNoTracking().SingleAsync(x => x.Id == v1.WorkItemId);
        Assert.Equal(WorkItemKind.UserStory, story.Kind);
        Assert.Null(story.StageId); Assert.Null(story.ParentId);
        Assert.Equal("HU-0001 · Primeira HU", story.Title);
        Assert.Equal(1, v1.Version); Assert.Equal(Hu1, v1.Content); Assert.Equal("aberta", v1.Status);
        Assert.Equal(MethodDocumentText.Hash(Hu1), v1.ContentHash); Assert.Equal("mcp", v1.UpdatedVia);
        Assert.Equal("Primeira HU", v1.Title); Assert.Equal("hu", v1.Kind);

        var edited = Hu1.Replace("quero algo", "quero outra coisa", StringComparison.Ordinal);
        var v2 = await docs.SaveAsync(f.Seed.ProjectId, "HU-0001", new(edited, 1), default);
        Assert.Equal(2, v2.Version); Assert.Equal(edited, v2.Content);
        Assert.Equal("docs/trabalho/HU-0001-x/hu.md", v2.FilePath);
        var revisions = await docs.RevisionsAsync(f.Seed.ProjectId, "HU-0001", default);
        Assert.Equal([2, 1], revisions.Select(x => x.Version));
        Assert.Equal(Hu1, (await docs.RevisionAsync(f.Seed.ProjectId, "HU-0001", 1, default)).Content);

        var events = await scope.Db.TaskEvents.AsNoTracking().Where(x => x.WorkItemId == story.Id && x.Kind == "method_document_updated").ToListAsync();
        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.DoesNotContain("quero", e.Payload));
    }

    [Fact] // CA-2
    public async Task EpicIsCreatedAndStoryWithEpicoHeaderBecomesItsChild()
    {
        await using var f = await GuardFixture.CreateAsync();
        await using var scope = f.Scope(await f.TokenAsync("agente"));
        var docs = Documents(f, scope);
        var epic = await docs.SaveAsync(f.Seed.ProjectId, "EPIC-0001", new("---\nid: EPIC-0001\ntitulo: Épico\nstatus: planejada\n---\n\n# Épico\n", 0), default);
        var hu = await docs.SaveAsync(f.Seed.ProjectId, "HU-0002",
            new("---\nid: HU-0002\ntitulo: Filha\nstatus: aberta\nepico: EPIC-0001\n---\n\n# Filha\n", 0), default);

        var epicItem = await scope.Db.WorkItems.AsNoTracking().SingleAsync(x => x.Id == epic.WorkItemId);
        var story = await scope.Db.WorkItems.AsNoTracking().SingleAsync(x => x.Id == hu.WorkItemId);
        Assert.Equal(WorkItemKind.Epic, epicItem.Kind); Assert.Null(epicItem.StageId);
        Assert.Equal(epicItem.Id, story.ParentId); Assert.Equal("EPIC-0001", hu.ParentKey);

        var invalid = await Assert.ThrowsAsync<MethodOperationException>(() => docs.SaveAsync(f.Seed.ProjectId, "EPIC-0001",
            new("---\nid: EPIC-0001\ntitulo: Épico\nstatus: em-andamento\n---\n", 1), default));
        Assert.Equal(400, invalid.StatusCode);
        Assert.Equal(1, (await docs.GetAsync(f.Seed.ProjectId, "EPIC-0001", default)).Version);
    }

    [Fact] // CA-3
    public async Task LargePrdRoundTripsWithoutLimitAndNormalizesOnlyLineEndings()
    {
        await using var f = await GuardFixture.CreateAsync();
        await using var scope = f.Scope(await f.TokenAsync("agente"));
        var docs = Documents(f, scope);
        await docs.SaveAsync(f.Seed.ProjectId, "HU-0001", new(Hu1, 0), default);
        var body = "﻿---\r\nhu: HU-0001\r\n---\r\n\r\n# PRD com acentuação, tabela e código 🚀\r\n\r\n| a | b |\r\n|---|---|\r\n"
            + string.Concat(Enumerable.Repeat("Linha de texto do PRD com ç e ã.\r\n", 32_000));
        Assert.True(body.Length > 1_000_000);

        var saved = await docs.SaveAsync(f.Seed.ProjectId, "HU-0001/prd", new(body, 0), default);
        var expected = body.Replace("\r\n", "\n", StringComparison.Ordinal);
        var read = await docs.GetAsync(f.Seed.ProjectId, "HU-0001/prd", default);
        Assert.Equal(expected, read.Content);
        Assert.StartsWith("﻿", read.Content);
        Assert.Equal(MethodDocumentText.Hash(expected), saved.ContentHash);
        Assert.Equal("prd", read.Kind); Assert.Equal("HU-0001/prd", read.Key);

        var list = await docs.ListAsync(f.Seed.ProjectId, null, null, default);
        Assert.Contains(list, x => x.Key == "HU-0001/prd" && x.Version == 1);
        Assert.DoesNotContain("Linha de texto", JsonSerializer.Serialize(list));
    }

    [Fact] // CA-8
    public async Task StaleBaseVersionIsRejectedAndNothingChanges()
    {
        await using var f = await GuardFixture.CreateAsync();
        await using var scope = f.Scope(await f.TokenAsync("agente"));
        var docs = Documents(f, scope);
        await docs.SaveAsync(f.Seed.ProjectId, "HU-0001", new(Hu1, 0), default);
        await docs.SaveAsync(f.Seed.ProjectId, "HU-0001", new(Hu1 + "\nA", 1), default);
        await docs.SaveAsync(f.Seed.ProjectId, "HU-0001", new(Hu1 + "\nB", 2), default);

        var conflict = await Assert.ThrowsAsync<MethodOperationException>(() => docs.SaveAsync(f.Seed.ProjectId, "HU-0001", new(Hu1 + "\nC", 2), default));
        Assert.Equal((409, "method_document_version_conflict"), (conflict.StatusCode, conflict.Code));
        var current = await docs.GetAsync(f.Seed.ProjectId, "HU-0001", default);
        Assert.Equal(3, current.Version); Assert.EndsWith("\nB", current.Content);
        Assert.Equal(3, await scope.Db.MethodDocumentRevisions.CountAsync());
    }

    [Fact] // CA-8
    public async Task TaskBodyIsLockedForHumansWhileInDevelopmentButNotForAgent()
    {
        await using var f = await GuardFixture.CreateAsync();
        var body = "---\nid: T-0001\ntitulo: \"Caso do guard\"\nhu: HU-0001\n---\n\n# T-0001\n";
        await using (var agentScope = f.Scope(await f.TokenAsync("agente")))
            await Documents(f, agentScope).SaveAsync(f.Seed.ProjectId, "T-0001", new(body, 0), default);
        await f.MoveAsync(f.Ui, MethodStatusCodes.InDevelopment);

        await using (var human = f.Scope(f.Ui))
        {
            var docs = Documents(f, human);
            var read = await docs.GetAsync(f.Seed.ProjectId, "T-0001", default);
            Assert.True(read.Locked); Assert.False(read.CanEdit);
            var locked = await Assert.ThrowsAsync<MethodOperationException>(() => docs.SaveAsync(f.Seed.ProjectId, "T-0001", new(body + "x", 1), default));
            Assert.Equal((409, "method_document_locked"), (locked.StatusCode, locked.Code));
        }
        await using (var agentScope = f.Scope(await f.TokenAsync("agente")))
        {
            var saved = await Documents(f, agentScope).SaveAsync(f.Seed.ProjectId, "T-0001", new(body + "Relatório.\n", 1), default);
            Assert.Equal(2, saved.Version);
        }
    }

    [Fact] // CA-8
    public async Task HeaderRewriteChangesOnlyTitleAndStatusLinesAndItemTitle()
    {
        await using var f = await GuardFixture.CreateAsync();
        var original = "---\nid: HU-0001\ntitulo: \"Primeira HU\"\ntrilha: epico\nstatus: aberta      # aberta | planejada\nprd: sim\n---\n\n# Corpo\n";
        Guid storyId;
        await using (var agentScope = f.Scope(await f.TokenAsync("agente")))
            storyId = (await Documents(f, agentScope).SaveAsync(f.Seed.ProjectId, "HU-0001", new(original, 0), default)).WorkItemId;

        await using var human = f.Scope(f.Ui);
        var docs = Documents(f, human);
        var updated = await docs.UpdateHeaderAsync(f.Seed.ProjectId, "HU-0001", new(1, "Título \"novo\"", "planejada"), default);

        Assert.Equal(2, updated.Version); Assert.Equal("ui", updated.UpdatedVia);
        Assert.Equal("planejada", updated.Status); Assert.Equal("Título \"novo\"", updated.Title);
        var before = original.Split('\n'); var after = updated.Content.Split('\n');
        Assert.Equal(before.Length, after.Length);
        for (var i = 0; i < before.Length; i++)
        {
            if (before[i].StartsWith("titulo:", StringComparison.Ordinal)) Assert.Equal("titulo: \"Título \\\"novo\\\"\"", after[i]);
            else if (before[i].StartsWith("status:", StringComparison.Ordinal)) Assert.Equal("status: planejada      # aberta | planejada", after[i]);
            else Assert.Equal(before[i], after[i]);
        }
        Assert.Equal("HU-0001 · Título \"novo\"", (await human.Db.WorkItems.AsNoTracking().SingleAsync(x => x.Id == storyId)).Title);
        Assert.Equal(400, (await Assert.ThrowsAsync<MethodOperationException>(() =>
            docs.UpdateHeaderAsync(f.Seed.ProjectId, "HU-0001", new(2, null, "em-andamento"), default))).StatusCode);
    }

    [Fact] // CA-11
    public async Task OtherProjectTokenOtherOrganizationLimitedUserAndSystemAreRejected()
    {
        await using var f = await GuardFixture.CreateAsync();
        await using (var agentScope = f.Scope(await f.TokenAsync("agente")))
            await Documents(f, agentScope).SaveAsync(f.Seed.ProjectId, "HU-0001", new(Hu1, 0), default);

        // Token de outro projeto: o projeto nem aparece.
        var otherProject = await f.Seed.AddProjectAsync();
        var otherToken = await f.Seed.Service().IssueAsync(otherProject, new("Outro", "agente"));
        var otherActor = (await f.Seed.Service().AuthenticateAsync(otherToken.Token, "outro"))!;
        await using (var other = f.Scope(otherActor))
        {
            var docs = Documents(f, other);
            Assert.Equal(404, (await Assert.ThrowsAsync<MethodOperationException>(() => docs.GetAsync(f.Seed.ProjectId, "HU-0001", default))).StatusCode);
            Assert.Equal(404, (await Assert.ThrowsAsync<MethodOperationException>(() => docs.SaveAsync(f.Seed.ProjectId, "HU-0001", new("x", 1), default))).StatusCode);
        }

        // Papel sistema.
        await using (var system = f.Scope(await f.TokenAsync("sistema")))
            Assert.Equal(403, (await Assert.ThrowsAsync<MethodOperationException>(() =>
                Documents(f, system).GetAsync(f.Seed.ProjectId, "HU-0001", default))).StatusCode);

        // Pessoa sem Edit (e sem Create) lê, mas não grava nem cria.
        await f.Seed.AddLimitedIssuerAsync();
        f.Seed.Db.PermissionGrants.Add(new PermissionGrant { Id = Guid.NewGuid(), OrganizationId = f.Seed.OrgId,
            UserId = MethodTokenTests.Fixture.LimitedHuman, Scope = PermissionScope.Project, ScopeId = f.Seed.ProjectId,
            Permission = PlatformPermission.Create, IsAllowed = false });
        await f.Seed.Db.SaveChangesAsync();
        var limited = f.Ui with { ActorId = MethodTokenTests.Fixture.LimitedHuman, DisplayName = "Limitada" };
        await using (var scope = f.Scope(limited))
        {
            var docs = Documents(f, scope);
            var read = await docs.GetAsync(f.Seed.ProjectId, "HU-0001", default);
            Assert.False(read.CanEdit);
            Assert.Equal(403, (await Assert.ThrowsAsync<MethodOperationException>(() =>
                docs.SaveAsync(f.Seed.ProjectId, "HU-0001", new(Hu1 + "x", 1), default))).StatusCode);
            Assert.Equal(403, (await Assert.ThrowsAsync<MethodOperationException>(() =>
                docs.SaveAsync(f.Seed.ProjectId, "HU-0009", new(Hu1, 0), default))).StatusCode);
        }
        Assert.Equal(1, (await f.Seed.Db.MethodDocuments.AsNoTracking().SingleAsync()).Version);
        Assert.Empty(await f.Seed.Db.WorkItems.AsNoTracking().Where(x => x.Title.StartsWith("HU-0009")).ToListAsync());

        // Pessoa de outra organização: nem o projeto aparece (404 também por ID).
        var foreignOrg = Guid.NewGuid();
        var tenant = new CurrentOrganizationContext(); tenant.Set(foreignOrg);
        var access = new MethodAccessContext();
        access.Initialize(f.Ui with { OrganizationId = foreignOrg, ActorId = "foreign" });
        await using var foreignDb = new AppDbContext(f.Seed.Options, tenant, null, access, null, f.Seed.Clock);
        var foreignPermissions = new PermissionService(foreignDb, tenant, access, new MethodPermissionEvaluator(foreignDb, f.Seed.Clock));
        var foreignStore = new EfMethodGitStore(foreignDb, tenant, access, foreignPermissions, f.Seed.Clock);
        var foreignDocs = new MethodDocumentOperations(foreignDb, access, foreignStore,
            new MethodProjectOperations(foreignDb, access, tenant, foreignPermissions, foreignStore,
                new MethodProjectProvisioner(foreignDb, tenant, access, foreignPermissions, f.Seed.Clock)),
            foreignPermissions, new ProjectAccessService(foreignDb, foreignPermissions, access, new MethodPermissionEvaluator(foreignDb, f.Seed.Clock)),
            new SilentRealtime(), f.Seed.Clock);
        Assert.Equal(404, (await Assert.ThrowsAsync<MethodOperationException>(() => foreignDocs.GetAsync(f.Seed.ProjectId, "HU-0001", default))).StatusCode);
        Assert.Empty(await foreignDocs.ListAsync(f.Seed.ProjectId, null, null, default));
        Assert.Empty(await foreignDb.MethodDocuments.ToListAsync());
    }

    [Fact] // CA-11 (G-HISTORY)
    public async Task RevisionsCannotBeChangedOrDeleted()
    {
        await using var f = await GuardFixture.CreateAsync();
        await using (var agentScope = f.Scope(await f.TokenAsync("agente")))
            await Documents(f, agentScope).SaveAsync(f.Seed.ProjectId, "HU-0001", new(Hu1, 0), default);
        await using (var human = f.Scope(f.Ui))
        {
            var revision = await human.Db.MethodDocumentRevisions.SingleAsync();
            revision.Content = "adulterado";
            Assert.Equal(403, (await Assert.ThrowsAsync<MethodOperationException>(() => human.Db.SaveChangesAsync())).StatusCode);
        }
        await using (var human = f.Scope(f.Ui))
        {
            human.Db.MethodDocumentRevisions.Remove(await human.Db.MethodDocumentRevisions.SingleAsync());
            Assert.Equal(403, (await Assert.ThrowsAsync<MethodOperationException>(() => human.Db.SaveChangesAsync())).StatusCode);
        }
        Assert.Equal(Hu1, (await f.Seed.Db.MethodDocumentRevisions.AsNoTracking().SingleAsync()).Content);
    }

    [Fact]
    public void HeaderParserReadsSimpleFieldsAndIgnoresComments()
    {
        var header = MethodDocumentText.ParseHeader("---\nid: HU-0008\ntitulo: \"Com: dois pontos\"\nstatus: planejada   # comentário\nlista:\n  - item\n---\n# x\n");
        Assert.Equal("HU-0008", header["id"]); Assert.Equal("Com: dois pontos", header["titulo"]); Assert.Equal("planejada", header["status"]);
        Assert.Empty(MethodDocumentText.ParseHeader("# sem cabeçalho\n"));
    }

    internal static MethodDocumentOperations Documents(GuardFixture f, GuardFixture.ScopeLease scope)
    {
        var tenant = new CurrentOrganizationContext(); tenant.Set(f.Seed.OrgId);
        var provisioner = new MethodProjectProvisioner(scope.Db, tenant, scope.Access, scope.Permissions, f.Seed.Clock);
        var projects = new MethodProjectOperations(scope.Db, scope.Access, tenant, scope.Permissions, scope.Store, provisioner);
        var access = new ProjectAccessService(scope.Db, scope.Permissions, scope.Access, new MethodPermissionEvaluator(scope.Db, f.Seed.Clock));
        return new MethodDocumentOperations(scope.Db, scope.Access, scope.Store, projects, scope.Permissions, access, new SilentRealtime(), f.Seed.Clock);
    }

    internal static MethodTaskOperations Tasks(GuardFixture f, GuardFixture.ScopeLease scope)
    {
        var tenant = new CurrentOrganizationContext(); tenant.Set(f.Seed.OrgId);
        var provisioner = new MethodProjectProvisioner(scope.Db, tenant, scope.Access, scope.Permissions, f.Seed.Clock);
        var projects = new MethodProjectOperations(scope.Db, scope.Access, tenant, scope.Permissions, scope.Store, provisioner);
        var movement = new MethodMoveService(scope.Db, scope.Access, scope.Store, new WorkflowRepository(scope.Db), scope.Permissions,
            new SilentRealtime(), NullLogger<MethodMoveService>.Instance, f.Seed.Clock);
        return new MethodTaskOperations(scope.Db, scope.Access, scope.Store, projects, movement, scope.Permissions, new SilentRealtime(), f.Seed.Clock);
    }

    private sealed class SilentRealtime : IBoardRealtimeNotifier
    { public Task BoardChangedAsync(Guid boardId, string changeType, Guid? workItemId = null, CancellationToken ct = default) => Task.CompletedTask; }
}
