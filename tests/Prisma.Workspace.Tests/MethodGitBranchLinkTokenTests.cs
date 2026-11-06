using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodGit.Repositories;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Identity;
using Prisma.Workspace.Infrastructure.Repositories;
using Prisma.Workspace.Infrastructure.Services.MethodGit;
using Prisma.Workspace.Infrastructure.Services.MethodSecurity;
using Prisma.Workspace.Infrastructure.Tenancy;
using Xunit;
using GuardFixture = Prisma.Workspace.Tests.MethodGenericWriteGuardTests.GuardFixture;

namespace Prisma.Workspace.Tests;

/// <summary>T-0004 CA-8 com token de agente emitido de verdade e PermissionService real (banco em memória):
/// o teto do agente basta para vincular branch, e obter_task (GET da task) devolve o vínculo.</summary>
public sealed class MethodGitBranchLinkTokenTests
{
    [Fact]
    public async Task CA8_issued_agent_token_links_branch_and_obter_task_returns_local_link()
    {
        await using var f = await GuardFixture.CreateAsync(MethodStatusCodes.InDevelopment);
        var integration = await AddIntegrationAsync(f);
        await using var scope = f.Scope(await f.TokenAsync("agente"));
        var link = await Operations(scope).LinkBranchAsync(f.Seed.ProjectId, "T-0001", new("qa/metodo", "task/T-0001-token"),
            scope.Access.Current!.ActorId, default);
        Assert.Equal(GitBranchStates.Local, link.BranchState);
        var task = await Tasks(f, scope).GetAsync(f.Seed.ProjectId, "T-0001", default);
        var git = Assert.Single(task.GitLinks!);
        Assert.Equal("task/T-0001-token", git.BranchName); Assert.Equal(GitBranchStates.Local, git.BranchState);
        Assert.Equal("qa/metodo", git.RepositoryFullName); Assert.Equal(integration, git.IntegrationId);
        Assert.Single(await scope.Db.TaskEvents.Where(x => x.WorkItemId == f.ItemId && x.Kind == "git_branch_linked").ToListAsync());
    }

    [Fact]
    public async Task CA8_issued_token_of_another_project_is_refused_without_writing()
    {
        await using var f = await GuardFixture.CreateAsync(MethodStatusCodes.InDevelopment);
        await AddIntegrationAsync(f);
        var otherProject = await f.Seed.AddProjectAsync();
        var otherToken = await f.Seed.Service().IssueAsync(otherProject, new("Outro projeto", "agente"));
        var actor = (await f.Seed.Service().AuthenticateAsync(otherToken.Token, "guard-token"))!;
        await using var scope = f.Scope(actor);
        var error = await Assert.ThrowsAsync<MethodOperationException>(() => Operations(scope).LinkBranchAsync(f.Seed.ProjectId, "T-0001",
            new("qa/metodo", "task/T-0001-token"), actor.ActorId, default));
        Assert.Equal(403, error.StatusCode);
        Assert.Empty(await scope.Db.Set<TaskGitLink>().IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await scope.Db.TaskEvents.IgnoreQueryFilters().Where(x => x.Kind == "git_branch_linked").ToListAsync());
    }

    private static async Task<Guid> AddIntegrationAsync(GuardFixture f)
    {
        var principal = new MethodPrincipal { Id = Guid.NewGuid(), OrganizationId = f.Seed.OrgId, ProjectId = f.Seed.ProjectId, Role = MethodRole.System,
            DisplayName = "Integração", DelegatedByUserId = GuardFixture.Human, PermissionCeilingJson = "[1,3,6,7]" };
        var integration = new GitRepositoryIntegration { Id = Guid.NewGuid(), ProjectId = f.Seed.ProjectId, OrganizationId = f.Seed.OrgId, Provider = "github",
            Host = "github.com", RepositoryId = 900001, RepositoryFullName = "qa/metodo", DefaultBranch = "main", AppId = 1, InstallationId = 1,
            AppPrivateKeySecretRef = "k", WebhookSecretRef = "w", SystemPrincipalId = principal.Id, Enabled = true };
        f.Seed.Db.AddRange(principal, integration); await f.Seed.Db.SaveChangesAsync();
        return integration.Id;
    }
    private static GitPullRequestOperations Operations(GuardFixture.ScopeLease scope)
    {
        var provider = new TestGitProvider();
        return new(scope.Store, provider, new GitRepositoryOperations(scope.Store, provider, new TestGitSecrets(), scope.Access, scope.Permissions),
            scope.Access, scope.Permissions);
    }
    private static MethodTaskOperations Tasks(GuardFixture f, GuardFixture.ScopeLease scope)
    {
        var tenant = new CurrentOrganizationContext(); tenant.Set(f.Seed.OrgId);
        var provisioner = new MethodProjectProvisioner(scope.Db, tenant, scope.Access, scope.Permissions, f.Seed.Clock);
        var project = new MethodProjectOperations(scope.Db, scope.Access, tenant, scope.Permissions, scope.Store, provisioner);
        var movement = new MethodMoveService(scope.Db, scope.Access, scope.Store, new WorkflowRepository(scope.Db), scope.Permissions,
            new SilentRealtime(), NullLogger<MethodMoveService>.Instance, f.Seed.Clock);
        return new(scope.Db, scope.Access, scope.Store, project, movement, scope.Permissions, new SilentRealtime(), f.Seed.Clock);
    }
    private sealed class SilentRealtime : IBoardRealtimeNotifier
    { public Task BoardChangedAsync(Guid boardId, string changeType, Guid? workItemId = null, CancellationToken ct = default) => Task.CompletedTask; }
}
