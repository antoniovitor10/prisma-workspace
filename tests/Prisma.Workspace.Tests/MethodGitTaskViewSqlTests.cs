using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodGit.Repositories;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Services.MethodGit;
using Xunit;

namespace Prisma.Workspace.Tests;

/// <summary>T-0005 e T-0006 (HU-0002): resumo do cartão e aba Git da tarefa, no SQL descartável do método.</summary>
[Collection("MethodGit SQL")]
public sealed class MethodGitTaskViewSqlTests : IClassFixture<GitDeliverySqlFixture>
{
    private readonly GitDeliverySqlFixture _fixture;
    public MethodGitTaskViewSqlTests(GitDeliverySqlFixture fixture) => _fixture = fixture;

    private static GitPullRequestSnapshot Pr(GitDeliverySqlFixture.SqlCase c, int number, string branch, string state = "open", bool merged = false,
        string head = "b", DateTimeOffset? at = null)
        => new(number, $"https://github.com/qa/metodo/pull/{number}", $"PR {number}", branch, "main", new string(head[0], 40),
            new string('c', 40), state, false, merged, at ?? c.Now, PullRequestAuthorName: "autor-git");

    [MethodGitSqlFact]
    public async Task CA4_card_summary_comes_from_one_tenant_scoped_query_and_prefers_the_open_pull_request()
    {
        await using var c = await _fixture.CaseAsync(); await using var other = await _fixture.CaseAsync();
        var store = c.Store(c.Context);
        var closed = await store.UpsertObservationAsync(c.Repository.Id, Pr(c, 1, "feat/a", "closed", at: c.Now.AddMinutes(1)), null);
        var open = await store.UpsertObservationAsync(c.Repository.Id, Pr(c, 2, "feat/b"), null);
        await store.LinkExplicitAsync(c.Repository.Id, c.ItemId, open.Id, "PR ativo.");
        c.Clock.Now = c.Now.AddMinutes(5);
        await store.LinkExplicitAsync(c.Repository.Id, c.ItemId, closed.Id, "PR fechado depois.");
        await store.UpdateObservationReviewsAsync(c.Repository.Id, 2, new string('b', 40), 1, false);
        // Revisão de outra head não grava: o resumo vale para a head atual (D4).
        await store.UpdateObservationReviewsAsync(c.Repository.Id, 1, new string('z', 40), 3, true);
        c.Context.ChangeTracker.Clear();

        var reader = new EfWorkItemGitSummaryReader(c.Context, new Tenant(c.Repository.OrganizationId));
        var summaries = await reader.GetAsync([c.ItemId, other.ItemId]);
        var summary = Assert.Single(summaries).Value;
        Assert.Equal(2, summary.PrNumber); Assert.Equal("approved", summary.PrState); Assert.Equal("feat/b", summary.Branch);
        Assert.Equal("metodo", summary.RepoName); Assert.Equal("qa/metodo", summary.RepoFullName);
        // Outra organização não enxerga os vínculos deste quadro.
        Assert.Empty(await new EfWorkItemGitSummaryReader(c.Context, new Tenant(other.Repository.OrganizationId)).GetAsync([c.ItemId]));
        var stored = await c.Context.Set<GitPullRequestObservation>().AsNoTracking().SingleAsync(x => x.Id == closed.Id);
        // merge_commit_sha de PR não mergeado (merge de teste do GitHub) não é guardado: fechado continua "fechado".
        Assert.Null(stored.MergeSha); Assert.Equal(0, stored.ApprovalsCount);
    }

    [MethodGitSqlFact]
    public async Task CA5_CA6_task_view_lists_branches_prs_and_timeline_in_order_with_actor_and_channel()
    {
        await using var c = await _fixture.CaseAsync();
        var human = c.Actor!;
        await c.UseAgentAsync();
        await Operations(c).LinkBranchAsync(c.ProjectId, "T-0001", new("qa/metodo", "feat/login"), c.Actor!.ActorId, default);
        c.Clock.Now = c.Now.AddMinutes(1);
        c.UseActor(Worker(c));
        Assert.Equal(GitBranchPushResult.Recorded, await c.Store(c.Context).MarkBranchPushedAsync(c.Repository.Id, "feat/login", "4242", "dev-git"));
        c.Clock.Now = c.Now.AddMinutes(2);
        c.UseActor(human);
        var store = c.Store(c.Context);
        var observation = await store.UpsertObservationAsync(c.Repository.Id, Pr(c, 9, "feat/login", "closed", merged: true, at: c.Now.AddMinutes(4)), null);
        await store.LinkExplicitAsync(c.Repository.Id, c.ItemId, observation.Id, "Vínculo pela aba Git.");
        // Movimento por fato Git pela consulta (T-0008): o histórico guarda o canal "consulta".
        c.Context.Add(new TaskEvent { Id = Guid.NewGuid(), WorkItemId = c.ItemId, ActorId = "method:sistema", Kind = "stage_changed", CreatedAt = c.Now.AddMinutes(3),
            Payload = JsonSerializer.Serialize(new { factType = MethodGitFactCodes.PullRequestOpened, pullRequestNumber = 9, channel = MethodChannels.Poll,
                actorName = "Integração SQL", gitActorName = "dev-git", integrationId = c.Repository.Id, fromStatus = "em-desenvolvimento", toStatus = "em-revisao" }) });
        await c.Context.SaveChangesAsync(); c.Context.ChangeTracker.Clear();

        var view = await Operations(c).GetTaskGitAsync(c.ProjectId, c.ItemId, human.ActorId, default);
        Assert.Equal("T-0001", view.MethodTaskId); Assert.True(view.CanLink);
        var branch = Assert.Single(view.Branches);
        Assert.Equal("feat/login", branch.BranchName); Assert.Equal(GitBranchStates.Remote, branch.BranchState);
        Assert.Equal("https://github.com/qa/metodo/tree/feat/login", branch.Url); Assert.Equal("qa/metodo", branch.RepositoryFullName);
        var pr = Assert.Single(view.PullRequests);
        Assert.Equal(9, pr.Number); Assert.True(pr.Merged); Assert.Equal("PR 9", pr.Title); Assert.Equal("https://github.com/qa/metodo/pull/9", pr.Url);
        Assert.Equal(["git_branch_linked", "git_branch_pushed", "git_pr_linked", "git_fact", "git_pr_merged"], view.Timeline.Select(x => x.Kind).ToArray());
        Assert.Equal(new string?[] { MethodChannels.Mcp, MethodChannels.Webhook, MethodChannels.Ui, MethodChannels.Poll, null }, view.Timeline.Select(x => x.Channel));
        Assert.Equal("Agente SQL", view.Timeline[0].ActorName); Assert.Equal("dev-git", view.Timeline[1].ActorName);
        Assert.Equal("Humano SQL", view.Timeline[2].ActorName); Assert.Equal(9, view.Timeline[2].PullRequestNumber);
        Assert.Equal("em-revisao", view.Timeline[3].ToStatus); Assert.Equal("qa/metodo", view.Timeline[3].RepositoryFullName);
        Assert.True(view.Timeline.Zip(view.Timeline.Skip(1)).All(pair => pair.First.AtUtc <= pair.Second.AtUtc));
        Assert.Contains(view.Repositories, r => r.RepositoryFullName == "qa/metodo" && r.Mode == GitIntegrationModes.App);
    }

    [MethodGitSqlFact]
    public async Task CA7_url_of_other_repository_missing_pull_request_and_actor_without_edit_are_refused_without_writing()
    {
        await using var c = await _fixture.CaseAsync();
        var human = c.Actor!.ActorId;
        var outside = await Assert.ThrowsAsync<MethodOperationException>(() =>
            Operations(c).LinkUrlAsync(c.ProjectId, "T-0001", new("https://github.com/outro/repo/pull/1"), human, default));
        Assert.Equal(400, outside.StatusCode); Assert.Equal("unregistered_git_repository", outside.Code);
        var missing = await Assert.ThrowsAsync<GitHubUnavailableException>(() =>
            Operations(c).LinkUrlAsync(c.ProjectId, "T-0001", new("https://github.com/qa/metodo/pull/404"), human, default));
        Assert.Equal("git_not_found", missing.Code);
        var denied = await Assert.ThrowsAsync<MethodOperationException>(() =>
            Operations(c, new DenyEdit()).LinkUrlAsync(c.ProjectId, "T-0001", new("https://github.com/qa/metodo/pull/5"), human, default));
        Assert.Equal(403, denied.StatusCode);
        await Assert.ThrowsAsync<MethodOperationException>(() =>
            Operations(c, new DenyEdit()).LinkBranchAsync(c.ProjectId, "T-0001", new("qa/metodo", "feat/x"), human, default));
        c.Context.ChangeTracker.Clear();
        Assert.Empty(await c.Context.Set<TaskGitLink>().AsNoTracking().Where(x => x.ProjectId == c.ProjectId).ToListAsync());
        Assert.False((await Operations(c, new DenyEdit()).GetTaskGitAsync(c.ProjectId, c.ItemId, human, default)).CanLink);
        // Task de outro projeto não vaza pela rota do projeto.
        var notHere = await Assert.ThrowsAsync<MethodOperationException>(() => Operations(c).GetTaskGitAsync(c.ProjectId, Guid.NewGuid(), human, default));
        Assert.Equal(404, notHere.StatusCode);
    }

    private static GitPullRequestOperations Operations(GitDeliverySqlFixture.SqlCase c, IPermissionService? permissions = null)
    {
        var store = c.Store(c.Context); var provider = new OnlyQaMetodoProvider(c); permissions ??= new AllowAll();
        return new(store, provider, new GitRepositoryOperations(store, provider, new TestGitSecrets(), c.Access, permissions, c.Clock), c.Access, permissions);
    }
    private static MethodActorIdentity Worker(GitDeliverySqlFixture.SqlCase c) => new(MethodAuthenticationKind.IntegrationWorker, MethodRole.System,
        c.Repository.SystemPrincipalId, $"method:{c.Repository.SystemPrincipalId:D}", "Integração SQL", c.Repository.OrganizationId, c.ProjectId, null, null,
        Enum.GetValues<PlatformPermission>().ToHashSet(), MethodChannels.Webhook, "sql-correlation");

    /// <summary>GitHub falso: só qa/metodo existe; PR 404 não existe.</summary>
    private sealed class OnlyQaMetodoProvider(GitDeliverySqlFixture.SqlCase c) : TestGitProvider
    {
        public override Task<GitInstallationValidation> ValidateInstallationAsync(GitRepositoryDescriptor repository, CancellationToken ct = default)
            => Task.FromResult(repository.RepositoryFullName == "qa/metodo" ? new GitInstallationValidation(true, null, null)
                : new GitInstallationValidation(false, "git_repository_name_mismatch", "outro repositório"));
        public override Task<GitPullRequestSnapshot> GetPullRequestAsync(GitRepositoryDescriptor repository, int number, CancellationToken ct = default)
            => number == 404 ? throw new GitHubUnavailableException("git_not_found") : Task.FromResult(Pr(c, number, $"feat/{number}"));
    }
    private sealed class AllowAll : IPermissionService
    {
        public Task<bool> HasAsync(string actor, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization, Guid? id = null, CancellationToken ct = default) => Task.FromResult(true);
        public Task EnsureAsync(string actor, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization, Guid? id = null, CancellationToken ct = default) => Task.CompletedTask;
    }
    /// <summary>Lê o projeto e a tarefa, mas não edita a tarefa.</summary>
    private sealed class DenyEdit : IPermissionService
    {
        public Task<bool> HasAsync(string actor, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization, Guid? id = null, CancellationToken ct = default)
            => Task.FromResult(!(scope == PermissionScope.WorkItem && permission == PlatformPermission.Edit));
        public async Task EnsureAsync(string actor, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization, Guid? id = null, CancellationToken ct = default)
        {
            if (!await HasAsync(actor, permission, scope, id, ct)) throw new MethodOperationException(403, "forbidden", "Sem permissão de edição.");
        }
    }
    private sealed class Tenant(Guid id) : IOrganizationContext { public Guid? OrganizationId => id; }
}
