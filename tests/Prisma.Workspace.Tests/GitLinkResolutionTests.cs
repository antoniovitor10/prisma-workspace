using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodGit.Webhooks;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;
using Xunit;

namespace Prisma.Workspace.Tests;

public class GitLinkResolutionTests
{
    [Fact]
    public async Task Explicit_link_wins_over_branch_and_title()
    {
        var store = new GitTestStore();
        var one = store.AddTask("T-0001"); store.AddTask("T-0002"); store.AddTask("T-0003");
        store.Explicit = new(Guid.NewGuid(), store.Repository.Id, one.WorkItemId, Guid.NewGuid(), null, "explicit", "https://github.com/qa/metodo/pull/1", null, "human", store.Now);
        var result = await new GitTaskResolver(store).ResolveAsync(store.Repository, 1, "task/T-0002-slug", "[T-0003] título");
        Assert.Equal(one.WorkItemId, result.Task?.WorkItemId); Assert.Equal("explicit", result.Source);
    }
    [Fact]
    public async Task Branch_resolves_free_Detran_title_and_wins_over_title_marker()
    {
        var store = new GitTestStore(); var task = store.AddTask("T-0001"); store.AddTask("T-0002");
        var resolver = new GitTaskResolver(store);
        Assert.Equal(task.WorkItemId, (await resolver.ResolveAsync(store.Repository, 1, "task/T-0001-corrigir-consulta", "Título livre")).Task?.WorkItemId);
        Assert.Equal(task.WorkItemId, (await resolver.ResolveAsync(store.Repository, 1, "task/T-0001-corrigir-consulta", "[T-0002]")).Task?.WorkItemId);
    }
    [Fact]
    public async Task Invalid_priority_never_falls_back_to_title()
    {
        var store = new GitTestStore(); store.AddTask("T-0001");
        var result = await new GitTaskResolver(store).ResolveAsync(store.Repository, 1, "task/T-9999-ausente", "[T-0001]");
        Assert.Null(result.Task); Assert.Equal("branch", result.Source); Assert.Equal("task_not_found", result.Reason);
    }
    [Theory]
    [InlineData("feature/sem-id", "Título livre", "task_not_identified")]
    [InlineData("feature/sem-id", "[T-0001] e [T-0002]", "ambiguous_title")]
    [InlineData("task/T-0001-", "[T-0001]", "invalid_task_branch")]
    public async Task Missing_or_ambiguous_candidates_are_not_linked(string branch, string title, string reason)
    {
        var store = new GitTestStore(); store.AddTask("T-0001");
        var result = await new GitTaskResolver(store).ResolveAsync(store.Repository, 1, branch, title);
        Assert.Null(result.Task); Assert.Equal(reason, result.Reason);
    }
    [Fact]
    public async Task Duplicated_or_cross_project_identity_does_not_move()
    {
        var store = new GitTestStore(); store.AddTask("T-0001"); store.AddTask("T-0001");
        var resolver = new GitTaskResolver(store);
        Assert.True((await resolver.ResolveAsync(store.Repository, 1, "task/T-0001-x", null)).Conflict);
        store.Tasks.Clear(); var task = store.AddTask("T-0001");
        store.Tasks[0] = task with { ProjectId = Guid.NewGuid() };
        Assert.Null((await resolver.ResolveAsync(store.Repository, 1, "task/T-0001-x", null)).Task);
    }
    [Fact]
    public async Task Title_is_fallback_only_and_task_must_be_top_level()
    {
        var store = new GitTestStore(); var task = store.AddTask("T-0001"); var resolver = new GitTaskResolver(store);
        Assert.Equal(task.WorkItemId, (await resolver.ResolveAsync(store.Repository, 1, "feature/x", "[T-0001] título")).Task?.WorkItemId);
        store.Tasks[0] = task with { IsTopLevel = false };
        Assert.Null((await resolver.ResolveAsync(store.Repository, 1, "feature/x", "[T-0001] título")).Task);
    }
}

internal class GitTestStore : IMethodGitStore
{
    public DateTimeOffset Now { get; } = DateTimeOffset.Parse("2026-10-16T07:00:00Z");
    public GitRepositoryDescriptor Repository { get; set; }
    public MethodProjectSnapshot Project { get; set; }
    public List<MethodTaskSnapshot> Tasks { get; } = [];
    public TaskGitLinkDto? Explicit { get; set; }
    public int Accepted { get; set; }
    public NormalizedGitEvent? AcceptedEvent { get; set; }
    public GitTestStore()
    {
        var org = Guid.NewGuid(); var project = Guid.NewGuid();
        Repository = new(Guid.NewGuid(), org, project, "github", "github.com", 900001, "qa/metodo", "main", 424242, 1,
            "app-key", "current-secret", null, null, Guid.NewGuid(), true);
        Project = new(org, project, "Teste", Guid.NewGuid(), "Método", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), true, MethodGitPhase.Webhooks,
            0, 1, MethodStatusCodes.All.ToDictionary(code => code, _ => Guid.NewGuid()));
    }
    public MethodTaskSnapshot AddTask(string id, string status = MethodStatusCodes.InDevelopment)
    {
        var task = new MethodTaskSnapshot(Project.OrganizationId, Project.ProjectId, Project.BoardId, Guid.NewGuid(), id,
            Project.StageMappings[status], status, Guid.NewGuid(), Guid.NewGuid(), Now.AddMinutes(-5), Now.AddMinutes(-5),
            null, null, null, null, true, false, [1]);
        Tasks.Add(task); return task;
    }
    public virtual Task<GitRepositoryDescriptor?> GetRepositoryForIngressAsync(Guid id, CancellationToken ct = default) => Task.FromResult<GitRepositoryDescriptor?>(id == Repository.Id ? Repository : null);
    public virtual Task<GitRepositoryDescriptor?> GetRepositoryAsync(Guid id, CancellationToken ct = default) => GetRepositoryForIngressAsync(id, ct);
    public virtual Task<IReadOnlyList<GitRepositoryDescriptor>> ListRepositoriesAsync(Guid project, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<GitRepositoryDescriptor>>(project == Project.ProjectId ? [Repository] : []);
    public virtual Task SaveRepositoryAsync(GitRepositoryDescriptor repository, CancellationToken ct = default) { Repository = repository; return Task.CompletedTask; }
    public virtual Task<MethodProjectSnapshot?> GetProjectAsync(Guid project, CancellationToken ct = default) => Task.FromResult<MethodProjectSnapshot?>(project == Project.ProjectId ? Project : null);
    public virtual Task<MethodTaskSnapshot?> GetTaskAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Tasks.SingleOrDefault(task => task.WorkItemId == id));
    public virtual Task<IReadOnlyList<MethodTaskSnapshot>> FindTasksAsync(Guid project, string id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MethodTaskSnapshot>>(Tasks.Where(task => task.MethodTaskId == id).ToArray());
    public virtual Task<TaskGitLinkDto?> GetExplicitPullRequestLinkAsync(Guid integration, int number, CancellationToken ct = default) => Task.FromResult(Explicit);
    public virtual Task<IReadOnlyList<TaskGitLinkDto>> GetTaskLinksAsync(Guid task, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TaskGitLinkDto>>([]);
    public virtual Task<GitPullRequestObservationDto> UpsertObservationAsync(Guid integration, GitPullRequestSnapshot pr, string? reason, CancellationToken ct = default) => Task.FromResult(new GitPullRequestObservationDto(Guid.NewGuid(), integration, Project.ProjectId, pr.Number, pr.Url, pr.Title, pr.HeadBranch, pr.BaseBranch, pr.State, pr.IsDraft, null, null, reason, pr.UpdatedAtUtc));
    public virtual Task<TaskGitLinkDto> LinkInferredAsync(Guid integration, Guid task, Guid? observation, string? branch, string source, CancellationToken ct = default) => Task.FromResult(new TaskGitLinkDto(Guid.NewGuid(), integration, task, observation, branch, source, null, null, null, Now));
    public virtual Task<TaskGitLinkDto> LinkExplicitAsync(Guid integration, Guid task, Guid observation, string reason, CancellationToken ct = default) => LinkInferredAsync(integration, task, observation, null, "explicit", ct);
    public virtual Task<GitPullRequestPage> ListPullRequestsAsync(Guid project, bool? linked, int page, int size, CancellationToken ct = default) => Task.FromResult(new GitPullRequestPage([], page, size, 0));
    public virtual Task<GitDeliveryReceipt> AcceptDeliveryAsync(Guid integration, string delivery, byte[] hash, NormalizedGitEvent normalized, string correlation, CancellationToken ct = default)
    { Accepted++; AcceptedEvent = normalized; return Task.FromResult(new GitDeliveryReceipt(Guid.NewGuid(), false, false, "received")); }
    public virtual Task<GitDeliveryLease?> ClaimNextDeliveryAsync(DateTimeOffset now, TimeSpan lease, CancellationToken ct = default) => Task.FromResult<GitDeliveryLease?>(null);
    public virtual Task<GitDeliverySnapshot?> GetDeliveryAsync(Guid id, CancellationToken ct = default) => Task.FromResult<GitDeliverySnapshot?>(null);
    public virtual Task<bool> CompleteDeliveryAsync(Guid id, byte[] version, string status, string? code, DateTimeOffset? retry, CancellationToken ct = default) => Task.FromResult(true);
    public virtual Task<bool> HasAppliedFactAsync(Guid project, Guid task, byte[] key, CancellationToken ct = default) => Task.FromResult(false);
}
