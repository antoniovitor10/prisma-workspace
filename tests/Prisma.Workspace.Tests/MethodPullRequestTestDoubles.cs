using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodGit.Repositories;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Tests;

/// <summary>Dublês em memória da HU-0007 (rascunho e abrir PR), sem SQL e sem GitHub real.</summary>
internal sealed class PullRequestTestStore : GitTestStore, IMethodGitStore
{
    public List<TaskGitLinkDto> Links { get; } = [];
    public List<(Guid WorkItemId, string Title, string Body)> SavedDrafts { get; } = [];
    public MethodPullRequestDraftDto? Draft { get; set; }
    public List<GitPullRequestSnapshot> Observed { get; } = [];
    public List<(Guid WorkItemId, Guid ObservationId, string Reason)> Explicit { get; } = [];
    public List<(Guid WorkItemId, GitPullRequestOpenedRecord Record)> Opened { get; } = [];
    public Exception? LinkFailure { get; set; }
    /// <summary>Status da task depois do vínculo (ex.: o webhook moveu antes); null mantém o status atual.</summary>
    public string? StatusAfterLink { get; set; }
    public int Writes => SavedDrafts.Count + Observed.Count + Explicit.Count + Opened.Count;

    public TaskGitLinkDto AddBranch(Guid workItemId, string branch, string state = GitBranchStates.Remote, Guid? integrationId = null)
    {
        var link = new TaskGitLinkDto(Guid.NewGuid(), integrationId ?? Repository.Id, workItemId, null, branch, "explicit", null, null, "user-1", Now,
            state, state == GitBranchStates.Remote ? Now : null, Repository.RepositoryFullName);
        Links.Add(link); return link;
    }
    public override Task<IReadOnlyList<TaskGitLinkDto>> GetTaskLinksAsync(Guid task, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskGitLinkDto>>(Links.Where(x => x.WorkItemId == task).ToArray());
    public override Task<GitPullRequestObservationDto> UpsertObservationAsync(Guid integration, GitPullRequestSnapshot pr, string? reason, CancellationToken ct = default)
    { Observed.Add(pr); return base.UpsertObservationAsync(integration, pr, reason, ct); }
    public override Task<TaskGitLinkDto> LinkExplicitAsync(Guid integration, Guid task, Guid observation, string reason, CancellationToken ct = default)
    {
        if (LinkFailure is not null) throw LinkFailure;
        Explicit.Add((task, observation, reason));
        if (StatusAfterLink is not null)
        {
            var index = Tasks.FindIndex(x => x.WorkItemId == task);
            Tasks[index] = Tasks[index] with { StatusCode = StatusAfterLink, StageId = Project.StageMappings[StatusAfterLink] };
        }
        return base.LinkExplicitAsync(integration, task, observation, reason, ct);
    }
    public Task<MethodPullRequestDraftDto?> GetPullRequestDraftAsync(Guid workItemId, CancellationToken ct = default) => Task.FromResult(Draft);
    public Task<MethodPullRequestDraftDto> SavePullRequestDraftAsync(Guid workItemId, string title, string body, CancellationToken ct = default)
    {
        SavedDrafts.Add((workItemId, title, body));
        Draft = new(title, body, Now, "Agente", true);
        return Task.FromResult(Draft);
    }
    public Task RecordPullRequestOpenedAsync(Guid workItemId, GitPullRequestOpenedRecord record, CancellationToken ct = default)
    { Opened.Add((workItemId, record)); return Task.CompletedTask; }
}

/// <summary>Permissões por nome; as negadas lançam AcessoNegadoException como o PermissionService real.</summary>
internal sealed class PullRequestTestPermissions : IPermissionService
{
    public HashSet<PlatformPermission> Denied { get; } = [];
    public Task<bool> HasAsync(string actor, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization, Guid? id = null, CancellationToken ct = default)
        => Task.FromResult(!Denied.Contains(permission));
    public Task EnsureAsync(string actor, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization, Guid? id = null, CancellationToken ct = default)
        => Denied.Contains(permission) ? throw new AcessoNegadoException() : Task.CompletedTask;
}

internal sealed class PullRequestTestAccess(MethodActorIdentity? actor) : IMethodAccessContext
{
    public MethodActorIdentity? Current { get; set; } = actor;
}

/// <summary>GitHub falso em memória: PRs abertos por head e criação com falha injetável.</summary>
internal sealed class PullRequestTestProvider : TestGitProvider, IGitProvider
{
    public List<GitPullRequestSnapshot> OpenPullRequests { get; } = [];
    public List<GitPullRequestCreation> Created { get; } = [];
    public int Finds { get; private set; }
    public string? CreateFailure { get; set; }
    public string? FindFailure { get; set; }
    public DateTimeOffset? RetryAt { get; set; }
    public bool CreateThenReportExisting { get; set; }
    public int NextNumber { get; set; } = 7;
    public long HeadRepositoryId { get; set; } = 900001;
    /// <summary>Data de abertura/atualização dos PRs criados (padrão fixo; os testes com SQL usam o relógio do caso).</summary>
    public DateTimeOffset? Time { get; set; }
    /// <summary>Chamado depois de criar o PR e antes de responder (ex.: o webhook chega antes do vínculo).</summary>
    public Func<GitPullRequestSnapshot, Task>? AfterCreate { get; set; }
    public override Task<GitPullRequestSnapshot> GetPullRequestAsync(GitRepositoryDescriptor repository, int number, CancellationToken ct = default)
        => Task.FromResult(OpenPullRequests.Single(x => x.Number == number));
    public Task<GitPullRequestSnapshot?> FindOpenPullRequestByHeadAsync(GitRepositoryDescriptor repository, string branch, CancellationToken ct = default)
    {
        Finds++;
        if (FindFailure is not null) throw new GitPullRequestRejectedException(FindFailure, RetryAt);
        return Task.FromResult(OpenPullRequests.FirstOrDefault(x => x.HeadBranch == branch));
    }
    public Task<GitPullRequestSnapshot> CreatePullRequestAsync(GitRepositoryDescriptor repository, GitPullRequestCreation pullRequest, CancellationToken ct = default)
    {
        if (CreateThenReportExisting)
        {
            // Outro clique criou o PR entre a lista e a criação: o GitHub responde 422 "já existe".
            OpenPullRequests.Add(Pr(NextNumber++, pullRequest.Head, false) with { HeadRepositoryId = HeadRepositoryId });
            throw new GitPullRequestRejectedException(GitPullRequestErrorCodes.AlreadyExists);
        }
        if (CreateFailure is not null) throw new GitPullRequestRejectedException(CreateFailure, RetryAt);
        Created.Add(pullRequest);
        var pr = Pr(NextNumber++, pullRequest.Head, pullRequest.Draft) with { Title = pullRequest.Title, BaseBranch = pullRequest.Base, HeadRepositoryId = HeadRepositoryId };
        if (Time is { } time) pr = pr with { UpdatedAtUtc = time, OpenedAtUtc = time };
        OpenPullRequests.Add(pr);
        return AfterCreate is null ? Task.FromResult(pr) : AfterCreateAsync(pr);
    }
    private async Task<GitPullRequestSnapshot> AfterCreateAsync(GitPullRequestSnapshot pr) { await AfterCreate!(pr); return pr; }
    public static GitPullRequestSnapshot Pr(int number, string head, bool draft) => new(number, $"https://github.com/qa/metodo/pull/{number}", "PR",
        head, "main", new string('a', 40), null, "open", draft, false, DateTimeOffset.Parse("2026-11-27T10:00:00Z"), HeadRepositoryId: 900001);
}

internal sealed class PullRequestTestMovement : IMethodMoveService
{
    public List<MethodMoveRequest> Requests { get; } = [];
    public MethodOperationException? Failure { get; set; }
    public Task<MethodMoveResult> MoveAsync(MethodMoveRequest request, CancellationToken ct = default)
    {
        Requests.Add(request);
        if (Failure is not null) throw Failure;
        return Task.FromResult(new MethodMoveResult(request.WorkItemId, null, request.DestinationStageId, true, false, Guid.NewGuid(), Guid.NewGuid(), "applied"));
    }
}

internal sealed class PullRequestTestAutomation : IAutomationExecutor
{
    public List<(Guid WorkItemId, Guid StageId)> Calls { get; } = [];
    public Task ExecuteStageEnteredAsync(Guid workItemId, Guid stageId, string actorId, CancellationToken ct = default)
    { Calls.Add((workItemId, stageId)); return Task.CompletedTask; }
}

internal sealed class PullRequestTestCardLinks : IMethodCardLinks
{
    public string CardUrl(Guid projectId, Guid workItemId) => $"https://prisma.example.invalid/projects/{projectId:D}/backlog?item={workItemId:D}";
}

/// <summary>Montagem das operações de rascunho e abertura sobre os dublês.</summary>
internal sealed class PullRequestTestHarness
{
    public const string HumanId = "user-1";
    public PullRequestTestStore Store { get; } = new();
    public PullRequestTestProvider Provider { get; } = new();
    public PullRequestTestPermissions Permissions { get; } = new();
    public PullRequestTestMovement Movement { get; } = new();
    public PullRequestTestAutomation Automation { get; } = new();
    public PullRequestTestAccess Access { get; }
    public MethodTaskSnapshot Task { get; }
    public PullRequestTestHarness(string status = MethodStatusCodes.InDevelopment)
    {
        Access = new(Ui());
        Task = Store.AddTask("T-0027", status);
    }
    public Guid ProjectId => Store.Project.ProjectId;
    public MethodActorIdentity Ui() => new(MethodAuthenticationKind.UiSession, MethodRole.Human, null, HumanId, "Pessoa UI",
        Store.Project.OrganizationId, null, null, null, Enum.GetValues<PlatformPermission>().ToHashSet(), MethodChannels.Ui, "corr-ui");
    public MethodActorIdentity Token(MethodRole role) => new(MethodAuthenticationKind.MethodToken, role, Guid.NewGuid(), "method:token",
        role == MethodRole.Agent ? "Agente" : "Pessoa token", Store.Project.OrganizationId, Store.Project.ProjectId, Guid.NewGuid(),
        Store.Now.AddHours(1), Enum.GetValues<PlatformPermission>().ToHashSet(), MethodChannels.Mcp, "corr-token");
    private GitRepositoryOperations Projects() => new(Store, Provider, new TestGitSecrets(), Access, Permissions);
    private GitPullRequestOperations Tasks() => new(Store, Provider, Projects(), Access, Permissions);
    public GitPullRequestDrafts Drafts() => new(Store, Projects(), Tasks(), Access, Permissions);
    public GitPullRequestOpening Opening() => new(Store, Provider, Projects(), Tasks(), Access, Permissions, Movement,
        new PullRequestTestCardLinks(), Automation, new GitTestClock(DateTimeOffset.Parse("2026-11-27T12:00:00Z")));
    public string ActorId => Access.Current?.ActorId ?? HumanId;
}
