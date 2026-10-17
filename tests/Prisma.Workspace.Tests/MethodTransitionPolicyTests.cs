using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodSecurity;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Identity.MethodTokens;

namespace Prisma.Workspace.Tests;

public sealed class MethodTransitionPolicyTests
{
    [Fact]
    public async Task LegacyPortalAndAutomationWithoutMethodKeepTheirExistingRules()
    {
        var f = new Fixture("backlog");
        var configured = f.Store.Project!;
        f.Store.Project = null;
        f.Permissions.Allowed = false;
        var policy = new MethodTransitionPolicy(new MethodAccessContext(), f.Store, f.Permissions);
        Assert.True((await policy.EvaluateAsync(f.Move("em-desenvolvimento"))).Allowed);
        Assert.True((await f.Policy(MethodRole.Human, MethodAuthenticationKind.Unknown).EvaluateAsync(f.Move("em-desenvolvimento"))).Allowed);
        f.Store.Project = configured with { Enabled = false };
        Assert.True((await policy.EvaluateAsync(f.Move("em-desenvolvimento") with { IsCreation = true })).Allowed);
    }

    [Fact]
    public async Task AnonymousMethodProjectMayCreateBacklogButNeverAuthorizeExecution()
    {
        var f = new Fixture("backlog");
        var policy = new MethodTransitionPolicy(new MethodAccessContext(), f.Store, f.Permissions);
        Assert.True((await policy.EvaluateAsync(f.Move("backlog") with { IsCreation = true, FromStageId = null })).Allowed);
        Assert.False((await policy.EvaluateAsync(f.Move("em-desenvolvimento"))).Allowed);
        Assert.False((await policy.EvaluateAsync(f.Move("em-desenvolvimento") with { IsCreation = true, FromStageId = null })).Allowed);
    }

    [Fact]
    public async Task NewTaskIdentityUsesProjectCreatePermissionEvenWithPreallocatedWorkItemId()
    {
        var f = new Fixture("backlog");
        var mutation = f.Move("backlog") with { IsCreation = true, ChangesMethodIdentity = true, FromStageId = null };
        Assert.True((await f.Policy().EvaluateAsync(mutation)).Allowed);
        Assert.Equal(PermissionScope.Project, f.Permissions.LastScope);
        Assert.Equal(mutation.ProjectId, f.Permissions.LastScopeId);
    }

    [Fact]
    public async Task HumanTokenTransferIntoDevelopmentCannotUseOtherBoardShortcut()
    {
        var f = new Fixture("backlog");
        var methodBoard = f.Store.Task!.BoardId;
        var otherBoard = Guid.NewGuid();
        var otherStage = Guid.NewGuid();
        f.Store.Task = f.Store.Task with { BoardId = otherBoard, StageId = otherStage, ActiveAuthorizationEventId = null };
        var mutation = f.Move("em-desenvolvimento") with { BoardId = otherBoard, FromStageId = otherStage, DestinationBoardId = methodBoard };
        Assert.False((await f.Policy(MethodRole.Human).EvaluateAsync(mutation)).Allowed);
        Assert.False((await f.Policy(MethodRole.Human).EvaluateAsync(mutation with { IsCreation = true })).Allowed);
        // Outras transferências não ganham a matriz do agente.
        Assert.True((await f.Policy(MethodRole.Human).EvaluateAsync(mutation with { ToStageId = f.Store.Project!.StageMappings["pronta"] })).Allowed);
    }

    [Theory]
    [InlineData("backlog")]
    [InlineData("pronta")]
    public async Task AgentCannotAuthorizeEvenWithForgedGitFact(string from)
    {
        var f = new Fixture(from);
        var decision = await f.Policy().EvaluateAsync(f.Move("em-desenvolvimento") with { GitFact = f.Fact(MethodGitFactCodes.PullRequestOpened) });
        Assert.False(decision.Allowed);
        Assert.Equal("interactive_authorization_required", decision.Code);
    }

    [Fact]
    public async Task UiSessionKeepsEveryExistingTransitionRegardlessOfMethodSettings()
    {
        foreach (var from in MethodStatusCodes.All)
        foreach (var to in MethodStatusCodes.All)
        {
            var f = new Fixture(from);
            f.Store.Project = null;
            f.Permissions.Allowed = false;
            var decision = await f.Policy(MethodRole.Human, MethodAuthenticationKind.UiSession).EvaluateAsync(
                f.Move(to) with { ChangesMethodIdentity = true, DestinationProjectId = Guid.NewGuid(), DestinationBoardId = Guid.NewGuid() });
            Assert.True(decision.Allowed, $"UI {from} -> {to} recebeu matriz nova");
        }
    }

    [Theory]
    [InlineData("backlog")]
    [InlineData("pronta")]
    [InlineData("bloqueada")]
    [InlineData("em-teste")]
    public async Task HumanMethodTokenCannotStartOrRenewInteractiveExecution(string from)
    {
        var f = new Fixture(from);
        Assert.False((await f.Policy(MethodRole.Human).EvaluateAsync(f.Move("em-desenvolvimento"))).Allowed);
        Assert.False((await f.Policy(MethodRole.Human).EvaluateAsync(f.Move("em-desenvolvimento") with
            { IsCreation = true, WorkItemId = null, FromStageId = null })).Allowed);
    }

    [Fact]
    public async Task HumanMethodTokenDoesNotInheritAgentMatrixForOtherMoves()
    {
        var f = new Fixture("em-teste");
        Assert.True((await f.Policy(MethodRole.Human).EvaluateAsync(f.Move("concluida"))).Allowed);
        f = new Fixture("em-revisao");
        // Retorno no ciclo já autorizado não concede uma autorização nova.
        Assert.True((await f.Policy(MethodRole.Human).EvaluateAsync(f.Move("em-desenvolvimento"))).Allowed);
    }

    [Fact]
    public async Task HumanMethodTokenCreationAllowsAllMappedStatusesExceptDevelopment()
    {
        foreach (var status in MethodStatusCodes.All)
        {
            var f = new Fixture("backlog");
            var mutation = f.Move(status) with { IsCreation = true, WorkItemId = null, FromStageId = null };
            Assert.Equal(status != MethodStatusCodes.InDevelopment, (await f.Policy(MethodRole.Human).EvaluateAsync(mutation)).Allowed);
        }
    }

    [Fact]
    public async Task OldGitEventCannotUndoLaterManualMoveByHumanMethodTokenInSameCycle()
    {
        var f = new Fixture("em-desenvolvimento");
        var fact = f.Fact(MethodGitFactCodes.PullRequestReadyForReview);
        // A autorização e a última sessão UI são anteriores, mas o humano retornou pela credencial de método depois do fato.
        f.Store.Task = f.Store.Task! with { LastManualChangeAtUtc = f.Now.AddSeconds(1) };
        var decision = await f.Policy(MethodRole.System, MethodAuthenticationKind.IntegrationWorker)
            .EvaluateAsync(f.Move("em-revisao") with { GitFact = fact });
        Assert.False(decision.Allowed);
        Assert.Equal("stale_git_fact", decision.Code);
    }

    [Fact]
    public async Task AgentMatrix_BlocksAllUnlistedTransitions()
    {
        foreach (var from in MethodStatusCodes.All)
        foreach (var to in MethodStatusCodes.All)
        {
            var f = new Fixture(from);
            var allowed = (await f.Policy().EvaluateAsync(f.Move(to))).Allowed;
            var expected = from == to || to == MethodStatusCodes.Blocked && from != MethodStatusCodes.Completed
                || from == MethodStatusCodes.InDevelopment && to == MethodStatusCodes.InReview;
            Assert.Equal(expected, allowed);
        }
    }

    [Fact]
    public async Task ResumeRequiresSamePrincipalAuthorizedOriginCurrentCycleAndHumanApproval()
    {
        var f = new Fixture("bloqueada");
        var valid = f.Store.Task! with { BlockingEventId = Guid.NewGuid(), BlockingPrincipalId = f.PrincipalId,
            BlockingOriginStatusCode = "em-desenvolvimento", ResumeApprovalEventId = Guid.NewGuid() };
        f.Store.Task = valid;
        Assert.True((await f.Policy().EvaluateAsync(f.Move("em-desenvolvimento"))).Allowed);
        foreach (var invalid in new[]
        {
            valid with { BlockingPrincipalId = Guid.NewGuid() }, valid with { BlockingOriginStatusCode = "backlog" },
            valid with { ResumeApprovalEventId = null }, valid with { BlockingEventId = null },
            valid with { ActiveAuthorizationEventId = null }, valid with { CycleId = Guid.Empty }
        })
        {
            f.Store.Task = invalid;
            Assert.False((await f.Policy().EvaluateAsync(f.Move("em-desenvolvimento"))).Allowed);
        }
    }

    [Fact]
    public async Task AgentCannotEscapeIdentityBoardOrProjectAndNeedsEffectivePermission()
    {
        var f = new Fixture("em-desenvolvimento");
        Assert.False((await f.Policy().EvaluateAsync(f.Move("bloqueada") with { ChangesMethodIdentity = true })).Allowed);
        Assert.False((await f.Policy().EvaluateAsync(f.Move("bloqueada") with { DestinationBoardId = Guid.NewGuid() })).Allowed);
        Assert.False((await f.Policy().EvaluateAsync(f.Move("bloqueada") with { DestinationProjectId = Guid.NewGuid() })).Allowed);
        f.Permissions.Allowed = false;
        Assert.False((await f.Policy().EvaluateAsync(f.Move("bloqueada"))).Allowed);
    }

    [Fact]
    public async Task CreationOnlyAllowsAgentBacklogOrCompletedAndSystemCannotCreate()
    {
        foreach (var to in MethodStatusCodes.All)
        {
            var f = new Fixture("backlog");
            var mutation = f.Move(to) with { IsCreation = true, WorkItemId = null, FromStageId = null };
            Assert.Equal(to is "backlog" or "concluida", (await f.Policy().EvaluateAsync(mutation)).Allowed);
            Assert.False((await f.Policy(MethodRole.System, MethodAuthenticationKind.IntegrationWorker).EvaluateAsync(mutation)).Allowed);
        }
    }

    [Fact]
    public async Task AgentReviewNeedsLevelZeroAndActualAuthorization()
    {
        var f = new Fixture("em-desenvolvimento");
        f.Store.Project = f.Store.Project! with { AutonomyLevel = 1 };
        Assert.False((await f.Policy().EvaluateAsync(f.Move("em-revisao"))).Allowed);
        f.Store.Project = f.Store.Project with { AutonomyLevel = 0 };
        f.Store.Task = f.Store.Task! with { ActiveAuthorizationEventId = null };
        Assert.False((await f.Policy().EvaluateAsync(f.Move("em-revisao"))).Allowed);
    }

    [Fact]
    public async Task SystemHttpTokenCannotMoveEvenWhenInternalFactIsPassed()
    {
        var f = new Fixture("em-desenvolvimento");
        var decision = await f.Policy(MethodRole.System).EvaluateAsync(f.Move("em-revisao") with { GitFact = f.Fact(MethodGitFactCodes.PullRequestOpened) });
        Assert.False(decision.Allowed);
        Assert.Equal("verified_fact_required", decision.Code);
    }

    [Theory]
    [InlineData("pull_request_opened", "em-desenvolvimento", "em-revisao")]
    [InlineData("pull_request_ready_for_review", "em-desenvolvimento", "em-revisao")]
    [InlineData("pull_request_reopened", "em-desenvolvimento", "em-revisao")]
    [InlineData("pull_request_closed_without_merge", "em-revisao", "em-desenvolvimento")]
    [InlineData("review_changes_requested", "em-revisao", "em-desenvolvimento")]
    [InlineData("pull_request_merged", "em-revisao", "em-teste")]
    public async Task InternalWorkerOnlyAppliesCurrentVerifiedFacts(string factType, string from, string to)
    {
        var f = new Fixture(from);
        var fact = f.Fact(factType);
        var mutation = f.Move(to) with { GitFact = fact };
        Assert.True((await f.Policy(MethodRole.System, MethodAuthenticationKind.IntegrationWorker).EvaluateAsync(mutation)).Allowed);
        Assert.False((await f.Policy(MethodRole.System, MethodAuthenticationKind.IntegrationWorker).EvaluateAsync(mutation with
            { GitFact = fact with { CycleId = Guid.NewGuid() } })).Allowed);
        Assert.False((await f.Policy(MethodRole.System, MethodAuthenticationKind.IntegrationWorker).EvaluateAsync(mutation with
            { GitFact = fact with { OccurredAtUtc = f.Now.AddMinutes(-10) } })).Allowed);
        f.Store.Repository = f.Store.Repository! with { SystemPrincipalId = Guid.NewGuid() };
        Assert.False((await f.Policy(MethodRole.System, MethodAuthenticationKind.IntegrationWorker).EvaluateAsync(mutation)).Allowed);
    }

    [Fact]
    public async Task MergeNeedsCompleteReadNoOtherOpenPrAndNoChangesRequested()
    {
        var f = new Fixture("em-revisao");
        var fact = f.Fact(MethodGitFactCodes.PullRequestMerged);
        foreach (var invalid in new[]
        {
            fact with { OpenPullRequestsReadComplete = false }, fact with { HasOtherOpenPullRequests = true },
            fact with { HasEffectiveChangesRequested = true }, fact with { MergeSha = null }, fact with { SemanticKey = [] }
        })
            Assert.False((await f.Policy(MethodRole.System, MethodAuthenticationKind.IntegrationWorker)
                .EvaluateAsync(f.Move("em-teste") with { GitFact = invalid })).Allowed);
        f.Store.Task = f.Store.Task! with { LastInteractiveChangeAtUtc = f.Now.AddSeconds(1) };
        Assert.False((await f.Policy(MethodRole.System, MethodAuthenticationKind.IntegrationWorker)
            .EvaluateAsync(f.Move("em-teste") with { GitFact = fact })).Allowed);
    }

    [Fact]
    public async Task StaleTaskIsConflictAndUnknownActorCannotBorrowUiAuthority()
    {
        var f = new Fixture("em-desenvolvimento");
        f.Store.Task = f.Store.Task! with { StageId = Guid.NewGuid() };
        var error = await Assert.ThrowsAsync<MethodOperationException>(() => f.Policy().EnsureAllowedAsync(f.Move("bloqueada")));
        Assert.Equal(409, error.StatusCode);
        Assert.False((await f.Policy(MethodRole.Human, MethodAuthenticationKind.Unknown).EvaluateAsync(f.Move("bloqueada"))).Allowed);
    }

    private sealed class Fixture
    {
        internal readonly Guid PrincipalId = Guid.NewGuid();
        internal readonly DateTimeOffset Now = new(2026, 10, 16, 7, 30, 0, TimeSpan.Zero);
        internal readonly Store Store = new();
        internal readonly Permissions Permissions = new();
        private readonly Dictionary<string, Guid> _stages = MethodStatusCodes.All.ToDictionary(x => x, _ => Guid.NewGuid());
        private readonly string _from;
        internal Fixture(string from)
        {
            _from = from;
            var org = Guid.NewGuid(); var project = Guid.NewGuid(); var board = Guid.NewGuid();
            Store.Project = new(org, project, "Método", board, "Método", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                true, MethodGitPhase.Webhooks, 0, 1, _stages);
            Store.Task = new(org, project, board, Guid.NewGuid(), "T-0001", _stages[from], from, Guid.NewGuid(),
                Guid.NewGuid(), Now.AddMinutes(-5), Now.AddMinutes(-5), null, null, null, null, true, false, [1]);
            Store.Repository = new(Guid.NewGuid(), org, project, "github", "github.com", 900001, "qa/metodo", "main", 1, 1,
                "test-app-key", "test-secret", null, null, PrincipalId, true);
        }
        internal MethodTransitionPolicy Policy(MethodRole role = MethodRole.Agent,
            MethodAuthenticationKind kind = MethodAuthenticationKind.MethodToken)
        {
            var context = new MethodAccessContext();
            var project = Store.Project;
            var task = Store.Task!;
            context.Initialize(new(kind, role, PrincipalId, $"method:{PrincipalId:D}", "Ator", task.OrganizationId, task.ProjectId,
                Guid.NewGuid(), Now.AddDays(1), Enum.GetValues<PlatformPermission>().ToHashSet(),
                kind == MethodAuthenticationKind.UiSession ? MethodChannels.Ui : MethodChannels.Mcp, "policy-test"));
            return new(context, Store, Permissions);
        }
        internal MethodMutation Move(string to) => new(Store.Task!.ProjectId, Store.Task.BoardId, Store.Task.WorkItemId,
            _stages[_from], _stages[to], "Motivo da movimentação.");
        internal VerifiedGitFact Fact(string type) => new(Store.Repository!.Id, Store.Task!.ProjectId, Store.Task.WorkItemId,
            Store.Task.CycleId, type, "pr:1", "revision:1", Now, "head-sha", "merge-sha", new byte[32], 1, null, true);
    }

    private sealed class Permissions : IPermissionService
    {
        internal bool Allowed = true;
        internal PermissionScope? LastScope;
        internal Guid? LastScopeId;
        public Task<bool> HasAsync(string userId, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization,
            Guid? scopeId = null, CancellationToken cancellationToken = default)
        {
            LastScope = scope; LastScopeId = scopeId;
            return Task.FromResult(Allowed);
        }
        public Task EnsureAsync(string userId, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization,
            Guid? scopeId = null, CancellationToken cancellationToken = default) => Allowed ? Task.CompletedTask : throw new InvalidOperationException();
    }

    private sealed class Store : IMethodGitStore
    {
        internal MethodProjectSnapshot? Project;
        internal MethodTaskSnapshot? Task;
        internal GitRepositoryDescriptor? Repository;
        public Task<MethodProjectSnapshot?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.FromResult(Project);
        public Task<MethodTaskSnapshot?> GetTaskAsync(Guid workItemId, CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.FromResult(Task);
        public Task<GitRepositoryDescriptor?> GetRepositoryAsync(Guid integrationId, CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.FromResult(Repository);
        public Task<GitRepositoryDescriptor?> GetRepositoryForIngressAsync(Guid integrationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<GitRepositoryDescriptor>> ListRepositoriesAsync(Guid projectId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveRepositoryAsync(GitRepositoryDescriptor repository, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MethodTaskSnapshot>> FindTasksAsync(Guid projectId, string methodTaskId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TaskGitLinkDto?> GetExplicitPullRequestLinkAsync(Guid integrationId, int number, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TaskGitLinkDto>> GetTaskLinksAsync(Guid workItemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GitPullRequestObservationDto> UpsertObservationAsync(Guid integrationId, GitPullRequestSnapshot pullRequest, string? unlinkedReason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TaskGitLinkDto> LinkInferredAsync(Guid integrationId, Guid workItemId, Guid? observationId, string? branchName, string source, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TaskGitLinkDto> LinkExplicitAsync(Guid integrationId, Guid workItemId, Guid observationId, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GitPullRequestPage> ListPullRequestsAsync(Guid projectId, bool? linked, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GitDeliveryReceipt> AcceptDeliveryAsync(Guid integrationId, string deliveryId, byte[] bodyHash, NormalizedGitEvent normalizedEvent, string correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GitDeliveryLease?> ClaimNextDeliveryAsync(DateTimeOffset nowUtc, TimeSpan lease, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GitDeliverySnapshot?> GetDeliveryAsync(Guid deliveryId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> CompleteDeliveryAsync(Guid deliveryId, byte[] expectedRowVersion, string status, string? errorCode, DateTimeOffset? nextAttemptAtUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasAppliedFactAsync(Guid projectId, Guid workItemId, byte[] semanticKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
