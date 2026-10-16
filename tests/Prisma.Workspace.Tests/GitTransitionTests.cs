using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodGit.Webhooks;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Services.MethodGit;
using Xunit;

namespace Prisma.Workspace.Tests;

public class GitTransitionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(777777L)]
    public async Task Fork_or_missing_head_repository_needs_explicit_link(long? headRepositoryId)
    {
        var f = new Fixture();
        f.Provider.PullRequests[(f.Store.Repository.Id, 1)] = f.Pr with { HeadRepositoryId = headRepositoryId, Title = "[T-0001] tentativa" };
        Assert.Equal("head_repository_requires_explicit_link", (await f.Process("opened")).Code);
        Assert.Empty(f.Move.Requests);
        var task = f.Store.Tasks[0];
        f.Store.Explicit = new(Guid.NewGuid(), f.Store.Repository.Id, task.WorkItemId, Guid.NewGuid(), null, "explicit", f.Pr.Url, null, "human", f.Store.Now);
        Assert.True((await f.Process("opened")).Moved);
    }
    [Fact]
    public async Task Business_pending_retries_back_off_and_stop_at_one_hour()
    {
        var f = new Fixture(MethodStatusCodes.InReview); f.SetMerged();
        f.Provider.PullRequests[(f.Store.Repository.Id, 2)] = f.Pr with { Number = 2, Url = "https://github.com/qa/metodo/pull/2", IsDraft = true };
        foreach (var (attempt, minutes) in new[] { (1, 1), (2, 2), (3, 4), (6, 32), (7, 60), (100, 60) })
        {
            var delivery = f.Delivery(f.Event("closed")) with { Attempts = attempt };
            Assert.Equal("merge_waiting_open_pr", (await f.Processor.ProcessAsync(delivery)).Code);
            Assert.Equal(f.Store.Now.AddMinutes(minutes), f.Store.LastNextAttemptAtUtc);
        }
        Assert.Empty(f.Move.Requests);
    }
    [Theory]
    [InlineData("opened")][InlineData("ready_for_review")][InlineData("reopened")]
    public async Task Ready_open_PR_moves_only_authorized_development(string action)
    {
        var f = new Fixture(); var result = await f.Process(action);
        Assert.True(result.Moved); Assert.Equal(MethodStatusCodes.InReview, f.Store.Tasks[0].StatusCode);
        Assert.Single(f.Move.Requests); Assert.NotNull(f.Move.Requests[0].GitFact);
        Assert.Equal(f.DeliveryId, f.Move.Requests[0].DeliveryId);
    }
    [Theory]
    [InlineData(MethodStatusCodes.Backlog)][InlineData(MethodStatusCodes.Ready)][InlineData(MethodStatusCodes.Blocked)][InlineData(MethodStatusCodes.Completed)]
    public async Task PR_never_authorizes_or_leaves_incompatible_stage(string status)
    {
        var f = new Fixture(status); await f.Process("opened"); Assert.Empty(f.Move.Requests);
    }
    [Fact]
    public async Task Disabled_mapping_or_Git_phase_preserves_pending_delivery_without_moving()
    {
        var f = new Fixture(); f.Store.Project = f.Store.Project with { Enabled = false };
        Assert.Equal("method_disabled", (await f.Process("opened")).Code); Assert.Empty(f.Move.Requests);
        f.Store.Project = f.Store.Project with { Enabled = true, GitPhase = MethodGitPhase.NoGit };
        Assert.Equal("method_git_disabled", (await f.Process("opened")).Code); Assert.Empty(f.Move.Requests);
    }
    [Fact]
    public async Task Draft_and_branch_only_record_without_moving()
    {
        var f = new Fixture(); f.Provider.PullRequests[(f.Store.Repository.Id, 1)] = f.Pr with { IsDraft = true };
        Assert.False((await f.Process("opened")).Moved); Assert.Empty(f.Move.Requests);
        var branchEvent = f.Event("branch") with { EventName = "create", BranchName = "task/T-0001-branch", PullRequestNumber = null };
        Assert.False((await f.Processor.ProcessAsync(f.Delivery(branchEvent))).Moved); Assert.Empty(f.Move.Requests);
    }
    [Fact]
    public async Task Stage_name_alone_does_not_fabricate_authorization()
    {
        var f = new Fixture(); f.Store.Tasks[0] = f.Store.Tasks[0] with { ActiveAuthorizationEventId = null, AuthorizedAtUtc = null };
        await f.Process("opened"); Assert.Empty(f.Move.Requests);
    }
    [Fact]
    public async Task Closed_without_merge_returns_review_to_development()
    {
        var f = new Fixture(MethodStatusCodes.InReview);
        f.Provider.PullRequests[(f.Store.Repository.Id, 1)] = f.Pr with { State = "closed", ClosedAtUtc = f.Store.Now };
        Assert.True((await f.Process("closed")).Moved); Assert.Equal(MethodStatusCodes.InDevelopment, f.Store.Tasks[0].StatusCode);
    }
    [Fact]
    public async Task Effective_changes_requested_returns_to_development_but_old_HEAD_does_not()
    {
        var f = new Fixture(MethodStatusCodes.InReview);
        f.Provider.Reviews[(f.Store.Repository.Id, 1)] = [new(10, "reviewer", "changes_requested", f.Pr.HeadSha, f.Store.Now)];
        var e = f.Event("submitted") with { EventName = "pull_request_review", ReviewId = 10, ReviewState = "changes_requested", ReviewCommitSha = f.Pr.HeadSha };
        Assert.True((await f.Processor.ProcessAsync(f.Delivery(e))).Moved);
        var stale = new Fixture(MethodStatusCodes.InReview);
        stale.Provider.Reviews[(stale.Store.Repository.Id, 1)] = [new(10, "reviewer", "changes_requested", new string('c', 40), stale.Store.Now)];
        await stale.Processor.ProcessAsync(stale.Delivery(e with { RepositoryId = stale.Store.Repository.RepositoryId }));
        Assert.Empty(stale.Move.Requests);
    }
    [Fact]
    public async Task Comment_only_review_does_not_approve_requested_changes()
    {
        var f = new Fixture(MethodStatusCodes.InReview); f.SetMerged();
        f.Provider.Reviews[(f.Store.Repository.Id, 1)] = [new(10, "reviewer", "changes_requested", f.Pr.HeadSha, f.Store.Now.AddSeconds(-1)),
            new(11, "reviewer", "commented", f.Pr.HeadSha, f.Store.Now)];
        var result = await f.Process("closed"); Assert.False(result.Moved); Assert.Equal("effective_changes_requested", result.Code);
    }
    [Fact]
    public async Task Merge_only_promotes_in_configured_main_branch_with_complete_view()
    {
        var f = new Fixture(MethodStatusCodes.InReview); f.SetMerged();
        Assert.True((await f.Process("closed")).Moved); Assert.Equal(MethodStatusCodes.InTest, f.Store.Tasks[0].StatusCode);
        var otherBase = new Fixture(MethodStatusCodes.InReview); otherBase.SetMerged();
        otherBase.Provider.PullRequests[(otherBase.Store.Repository.Id, 1)] = otherBase.Provider.PullRequests[(otherBase.Store.Repository.Id, 1)] with { BaseBranch = "release" };
        Assert.Equal("merge_non_default_branch", (await otherBase.Process("closed")).Code); Assert.Empty(otherBase.Move.Requests);
    }
    [Fact]
    public async Task Draft_in_another_repository_blocks_merge_promotion()
    {
        var f = new Fixture(MethodStatusCodes.InReview); f.SetMerged();
        var second = f.AddRepository();
        f.Provider.PullRequests[(second.Id, 2)] = f.Pr with { Number = 2, Url = "https://github.com/qa/second/pull/2", IsDraft = true, HeadRepositoryId = second.RepositoryId };
        Assert.Equal("merge_waiting_open_pr", (await f.Process("closed")).Code);
        Assert.Empty(f.Move.Requests); Assert.Contains(second.Id, f.Provider.ListedRepositories);
    }
    [Fact]
    public async Task Incomplete_pagination_or_access_never_means_no_other_PR()
    {
        var f = new Fixture(MethodStatusCodes.InReview); f.SetMerged(); var second = f.AddRepository();
        f.Provider.Incomplete.Add(second.Id);
        Assert.Equal(GitDeliveryStates.RetryableFailure, (await f.Process("closed")).Status); Assert.Empty(f.Move.Requests);
    }
    [Fact]
    public async Task Retained_merge_cannot_win_race_with_other_PR_closing_without_merge()
    {
        var f = new Fixture(MethodStatusCodes.InReview); f.SetMerged(f.Store.Now.AddSeconds(-30));
        f.Provider.PullRequests[(f.Store.Repository.Id, 2)] = f.Pr with { Number = 2, Url = "https://github.com/qa/metodo/pull/2", State = "closed", ClosedAtUtc = f.Store.Now };
        Assert.Equal("retained_merge_closed_without_merge", (await f.Process("closed")).Code); Assert.Empty(f.Move.Requests);
        await f.Processor.ProcessAsync(f.Delivery(f.Event("closed") with { PullRequestNumber = 2 }));
        Assert.Equal(MethodStatusCodes.InDevelopment, f.Store.Tasks[0].StatusCode);
        await f.Process("closed"); Assert.Single(f.Move.Requests); Assert.Equal(MethodStatusCodes.InDevelopment, f.Store.Tasks[0].StatusCode);
    }
    [Fact]
    public async Task Ignored_ready_replay_after_human_token_manual_return_stays_in_development()
    {
        var f = new Fixture(); f.Store.Tasks[0] = f.Store.Tasks[0] with { LastManualChangeAtUtc = f.Store.Now.AddSeconds(1) };
        Assert.Equal("stale_git_fact", (await f.Process("ready_for_review")).Code); Assert.Empty(f.Move.Requests);
    }
    [Fact]
    public async Task Old_event_is_not_rebound_to_new_authorization_cycle()
    {
        var f = new Fixture(); f.Store.Tasks[0] = f.Store.Tasks[0] with { CycleId = Guid.NewGuid(), AuthorizedAtUtc = f.Store.Now.AddMinutes(1) };
        Assert.Equal("stale_git_fact", (await f.Process("opened")).Code); Assert.Empty(f.Move.Requests);
    }
    [Fact]
    public async Task Permanent_fact_survives_operational_delivery_replay()
    {
        var f = new Fixture(); await f.Process("opened");
        f.Store.Tasks[0] = f.Store.Tasks[0] with { StatusCode = MethodStatusCodes.InDevelopment, StageId = f.Store.Project.StageMappings[MethodStatusCodes.InDevelopment] };
        var result = await f.Processor.ProcessAsync(f.Delivery(f.Event("opened")) with { Id = Guid.NewGuid(), DeliveryId = Guid.NewGuid().ToString("D") });
        Assert.Equal("already_applied", result.Code); Assert.Single(f.Move.Requests);
    }
    [Fact]
    public async Task New_review_round_is_not_deduplicated_only_by_destination()
    {
        var f = new Fixture(); await f.Process("opened");
        f.Provider.PullRequests[(f.Store.Repository.Id, 1)] = f.Pr with { State = "closed", ClosedAtUtc = f.Store.Now.AddSeconds(1) };
        await f.Processor.ProcessAsync(f.Delivery(f.Event("closed") with { OccurredAtUtc = f.Store.Now.AddSeconds(1) }));
        f.Provider.PullRequests[(f.Store.Repository.Id, 1)] = f.Pr with { UpdatedAtUtc = f.Store.Now.AddSeconds(2) };
        await f.Processor.ProcessAsync(f.Delivery(f.Event("reopened") with { OccurredAtUtc = f.Store.Now.AddSeconds(2) }));
        Assert.Equal(3, f.Move.Requests.Count); Assert.Equal(MethodStatusCodes.InReview, f.Store.Tasks[0].StatusCode);
    }

    private sealed class Fixture
    {
        public TransitionStore Store { get; } = new();
        public TransitionProvider Provider { get; } = new();
        public RecordingMovement Move { get; }
        public GitDeliveryProcessor Processor { get; }
        public GitPullRequestSnapshot Pr { get; }
        public Guid DeliveryId { get; } = Guid.NewGuid();
        public Fixture(string status = MethodStatusCodes.InDevelopment)
        {
            Store.AddTask("T-0001", status);
            Pr = new(1, "https://github.com/qa/metodo/pull/1", "Título livre", "task/T-0001-feature", "main", new string('b', 40), null, "open", false, false, Store.Now, Store.Now,
                HeadRepositoryId: Store.Repository.RepositoryId);
            Provider.PullRequests[(Store.Repository.Id, 1)] = Pr;
            Move = new(Store);
            Processor = new(Store, Provider, new GitTaskResolver(Store), Move, NullLogger<GitDeliveryProcessor>.Instance, new GitTestClock(Store.Now));
        }
        public void SetMerged(DateTimeOffset? at = null) => Provider.PullRequests[(Store.Repository.Id, 1)] = Pr with
        { State = "closed", IsMerged = true, MergeSha = new string('a', 40), MergedAtUtc = at ?? Store.Now, ClosedAtUtc = at ?? Store.Now };
        public GitRepositoryDescriptor AddRepository()
        {
            var repo = Store.Repository with { Id = Guid.NewGuid(), RepositoryId = 900002, RepositoryFullName = "qa/second", SystemPrincipalId = Guid.NewGuid() };
            Store.Repositories.Add(repo); return repo;
        }
        public NormalizedGitEvent Event(string action) => new("github", Store.Repository.RepositoryId, 1, "pull_request", action, Store.Now,
            "provider-revision", null, 1, Pr.Url, Pr.Title, Pr.HeadBranch, Pr.BaseBranch, Pr.HeadSha, Pr.MergeSha, Pr.State);
        public GitDeliverySnapshot Delivery(NormalizedGitEvent e) => new(DeliveryId, Store.Repository.Id, Store.Project.ProjectId, "github", Guid.NewGuid().ToString("D"), e,
            GitDeliveryStates.Processing, 1, "correlation", [1]);
        public Task<GitProcessingOutcome> Process(string action) => Processor.ProcessAsync(Delivery(Event(action)));
    }
    private sealed class TransitionStore : GitTestStore
    {
        public List<GitRepositoryDescriptor> Repositories { get; } = [];
        public HashSet<string> Applied { get; } = [];
        public DateTimeOffset? LastNextAttemptAtUtc { get; private set; }
        public override Task<bool> CompleteDeliveryAsync(Guid id, byte[] version, string status, string? code, DateTimeOffset? retry, CancellationToken ct = default)
        { LastNextAttemptAtUtc = retry; return Task.FromResult(true); }
        public override Task<IReadOnlyList<GitRepositoryDescriptor>> ListRepositoriesAsync(Guid project, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<GitRepositoryDescriptor>>([Repository, .. Repositories]);
        public override Task<GitRepositoryDescriptor?> GetRepositoryAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<GitRepositoryDescriptor?>(id == Repository.Id ? Repository : Repositories.SingleOrDefault(r => r.Id == id));
        public override Task<bool> HasAppliedFactAsync(Guid project, Guid task, byte[] key, CancellationToken ct = default) => Task.FromResult(Applied.Contains(Convert.ToHexString(key)));
    }
    private sealed class TransitionProvider : TestGitProvider
    {
        public Dictionary<(Guid, int), GitPullRequestSnapshot> PullRequests { get; } = [];
        public Dictionary<(Guid, int), IReadOnlyList<GitReviewSnapshot>> Reviews { get; } = [];
        public HashSet<Guid> Incomplete { get; } = [];
        public List<Guid> ListedRepositories { get; } = [];
        public override Task<GitPullRequestSnapshot> GetPullRequestAsync(GitRepositoryDescriptor repo, int number, CancellationToken ct = default) => Task.FromResult(PullRequests[(repo.Id, number)]);
        public override Task<IReadOnlyList<GitReviewSnapshot>> GetReviewsAsync(GitRepositoryDescriptor repo, int number, CancellationToken ct = default)
            => Task.FromResult(Reviews.TryGetValue((repo.Id, number), out var value) ? value : (IReadOnlyList<GitReviewSnapshot>)[]);
        public override Task<GitPullRequestReadSet> ListPullRequestsAsync(GitRepositoryDescriptor repo, string state, CancellationToken ct = default)
        {
            ListedRepositories.Add(repo.Id);
            return Task.FromResult(new GitPullRequestReadSet(PullRequests.Where(p => p.Key.Item1 == repo.Id && (state == "all" || p.Value.State == state))
                .Select(p => p.Value).ToArray(), !Incomplete.Contains(repo.Id), "git_read_incomplete"));
        }
    }
    private sealed class RecordingMovement(TransitionStore store) : IMethodMoveService
    {
        public List<MethodMoveRequest> Requests { get; } = [];
        public Task<MethodMoveResult> MoveAsync(MethodMoveRequest request, CancellationToken ct = default)
        {
            Requests.Add(request); var task = store.Tasks.Single(t => t.WorkItemId == request.WorkItemId);
            store.Applied.Add(Convert.ToHexString(request.GitFact!.SemanticKey));
            var status = store.Project.StageMappings.Single(pair => pair.Value == request.DestinationStageId).Key;
            store.Tasks[store.Tasks.IndexOf(task)] = task with { StatusCode = status, StageId = request.DestinationStageId };
            return Task.FromResult(new MethodMoveResult(task.WorkItemId, task.StageId, request.DestinationStageId, true, false, Guid.NewGuid(), Guid.NewGuid(), "moved"));
        }
    }
}
