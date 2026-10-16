using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Identity;
using Prisma.Workspace.Infrastructure.Identity.MethodTokens;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Tenancy;
using Xunit;

namespace Prisma.Workspace.Tests;

public sealed class MethodSecurityScopeTests
{
    [Theory]
    [InlineData("agente", ProjectRole.Member)]
    [InlineData("humano", ProjectRole.ProjectAdmin)]
    public async Task MethodCredentialCannotBorrowGlobalAdminOrReadOtherProjects(string role, ProjectRole expectedRole)
    {
        await using var f = await MethodTokenTests.Fixture.CreateAsync();
        var other = await f.AddProjectAsync();
        var globalRole = new IdentityRole { Id = Guid.NewGuid().ToString("D"), Name = "GlobalAdmin", NormalizedName = "GLOBALADMIN" };
        f.Db.Roles.Add(globalRole);
        f.Db.UserRoles.Add(new IdentityUserRole<string> { UserId = MethodTokenTests.Fixture.HumanA, RoleId = globalRole.Id });
        var ownItem = await SeedItemAsync(f.Db, f.OrgId, f.ProjectId);
        var otherItem = await SeedItemAsync(f.Db, f.OrgId, other);
        await f.Db.SaveChangesAsync();
        var issued = await f.Service().IssueAsync(f.ProjectId, new("Executor", role));
        var actor = (await f.Service().AuthenticateAsync(issued.Token, "scope-test"))!;
        var access = new MethodAccessContext(); access.Initialize(actor);
        var tenant = new CurrentOrganizationContext(); tenant.Set(f.OrgId);
        await using var db = new AppDbContext(f.Options, tenant, methodContext: access);
        var evaluator = new MethodPermissionEvaluator(db, f.Clock);
        var permissions = new PermissionService(db, tenant, access, evaluator);
        var projects = new ProjectAccessService(db, permissions, access, evaluator);

        Assert.Equal(expectedRole, await projects.GetRoleAsync(f.ProjectId, actor.ActorId));
        Assert.Null(await projects.GetRoleAsync(other, actor.ActorId));
        Assert.Equal([f.ProjectId], await db.Projects.Select(x => x.Id).ToArrayAsync());
        Assert.Equal([ownItem], await db.WorkItems.Select(x => x.Id).ToArrayAsync());
        Assert.Null(await db.WorkItems.SingleOrDefaultAsync(x => x.Id == otherItem));
        Assert.False(await permissions.HasAsync(actor.ActorId, PlatformPermission.View, PermissionScope.Project, other));
        Assert.False(await permissions.HasAsync(actor.ActorId, PlatformPermission.View, PermissionScope.WorkItem, otherItem));
        Assert.False(await permissions.HasAsync(actor.ActorId, PlatformPermission.AdministerOrganization));
        if (role == "agente")
        {
            Assert.False(await permissions.HasAsync(MethodTokenTests.Fixture.HumanA, PlatformPermission.ManagePermissions,
                PermissionScope.Project, f.ProjectId));
            // Consultar elegibilidade de participante é permitido; usar seu nome para autorizar não é.
            Assert.Equal(ProjectRole.ProjectAdmin, await projects.GetRoleAsync(f.ProjectId, MethodTokenTests.Fixture.HumanA));
            await Assert.ThrowsAsync<NaoEncontradoException>(() => projects.EnsureAtLeastAsync(f.ProjectId,
                MethodTokenTests.Fixture.HumanA, ProjectRole.ProjectAdmin));
        }
        await f.Service().RevokeAsync(f.ProjectId, issued.Metadata.Id);
        Assert.False(await permissions.HasAsync(actor.ActorId, PlatformPermission.View, PermissionScope.Project, f.ProjectId));
        Assert.Null(await projects.GetRoleAsync(f.ProjectId, actor.ActorId));
    }

    [Fact]
    public async Task HumanUiAndLegacyContextsKeepTheirExistingProjectReadsAndPermissions()
    {
        await using var f = await MethodTokenTests.Fixture.CreateAsync();
        var other = await f.AddProjectAsync();
        var tenant = new CurrentOrganizationContext(); tenant.Set(f.OrgId);
        var access = new MethodAccessContext();
        access.Initialize(new MethodActorIdentity(MethodAuthenticationKind.UiSession, MethodRole.Human, null,
            MethodTokenTests.Fixture.HumanA, "Humano", f.OrgId, null, null, null, new HashSet<PlatformPermission>(), "ui", "ui-scope"));
        await using var db = new AppDbContext(f.Options, tenant, methodContext: access);
        var permissions = new PermissionService(db, tenant, access, new MethodPermissionEvaluator(db, f.Clock));
        var projects = new ProjectAccessService(db, permissions, access, new MethodPermissionEvaluator(db, f.Clock));
        Assert.Equal(2, await db.Projects.CountAsync());
        Assert.True(await permissions.HasAsync(MethodTokenTests.Fixture.HumanA, PlatformPermission.ManagePermissions,
            PermissionScope.Project, other));
        Assert.Equal(ProjectRole.ProjectAdmin, await projects.GetRoleAsync(other, MethodTokenTests.Fixture.HumanA));
        await using var legacy = new AppDbContext(f.Options, tenant);
        Assert.Equal(2, await legacy.Projects.CountAsync());
    }

    [Fact]
    public async Task WorkerInitializesTenantPrincipalAndAuditEvenWhenMappingIsUnavailable()
    {
        await using var f = await MethodTokenTests.Fixture.CreateAsync();
        var issued = await f.Service().IssueAsync(f.ProjectId, new("Integração", "sistema"));
        var settings = await f.Db.Set<MethodProjectSettings>().SingleAsync(x => x.ProjectId == f.ProjectId);
        settings.Enabled = false; // Pausa dos efeitos não perde o ingresso assinado.
        var repository = new GitRepositoryIntegration { Id = Guid.NewGuid(), ProjectId = f.ProjectId,
            OrganizationId = f.OrgId, SystemPrincipalId = issued.Metadata.PrincipalId };
        f.Db.Add(repository); await f.Db.SaveChangesAsync();
        var tenant = new CurrentOrganizationContext(); var audit = new CurrentAuditContext(); var access = new MethodAccessContext();
        await using var db = new AppDbContext(f.Options, tenant, audit, access);
        var initializer = new MethodWorkerContextInitializer(db, tenant, audit, access, access,
            new MethodPermissionEvaluator(db, f.Clock));
        await initializer.InitializeAsync(repository.Id, null, "worker-scope");
        Assert.Equal(f.OrgId, tenant.OrganizationId);
        Assert.Equal(MethodAuthenticationKind.IntegrationWorker, access.Current!.AuthenticationKind);
        Assert.Equal($"method:{issued.Metadata.PrincipalId:D}", access.Current.ActorId);
        Assert.Equal(f.ProjectId, access.Current.ProjectId);
        Assert.Equal("webhook", audit.Origin); Assert.Equal("worker-scope", audit.CorrelationId);
        Assert.Equal(access.Current.ActorId, audit.UserId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.InitializeAsync(repository.Id, null, "reuse"));
    }

    [Fact]
    public async Task WorkerRejectsDeliveryFromAnotherIntegrationBeforeInitializingIdentity()
    {
        await using var f = await MethodTokenTests.Fixture.CreateAsync();
        var issued = await f.Service().IssueAsync(f.ProjectId, new("Integração", "sistema"));
        var repository = new GitRepositoryIntegration { Id = Guid.NewGuid(), ProjectId = f.ProjectId,
            OrganizationId = f.OrgId, SystemPrincipalId = issued.Metadata.PrincipalId };
        f.Db.Add(repository); await f.Db.SaveChangesAsync();
        var tenant = new CurrentOrganizationContext(); var audit = new CurrentAuditContext(); var access = new MethodAccessContext();
        await using var db = new AppDbContext(f.Options, tenant, audit, access);
        var initializer = new MethodWorkerContextInitializer(db, tenant, audit, access, access,
            new MethodPermissionEvaluator(db, f.Clock));
        Assert.Equal(403, (await Assert.ThrowsAsync<MethodOperationException>(() =>
            initializer.InitializeAsync(repository.Id, Guid.NewGuid(), "wrong-delivery"))).StatusCode);
        Assert.Null(access.Current); Assert.Null(tenant.OrganizationId); Assert.Null(audit.UserId);
    }

    private static async Task<Guid> SeedItemAsync(AppDbContext db, Guid orgId, Guid projectId)
    {
        var board = new Board { Id = Guid.NewGuid(), ProjectId = projectId, OrganizationId = orgId,
            Name = "Quadro", OwnerId = MethodTokenTests.Fixture.HumanA };
        var stage = new Stage { Id = Guid.NewGuid(), ProjectId = projectId, BoardId = board.Id, Name = "Backlog" };
        var item = new WorkItem { Id = Guid.NewGuid(), BoardId = board.Id, StageId = stage.Id, Title = "Item" };
        db.AddRange(board, stage, item); await db.SaveChangesAsync();
        return item.Id;
    }
}
