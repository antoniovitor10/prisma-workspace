using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodGit.Webhooks;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Infrastructure.Services.MethodGit;

public sealed record GitProcessingOutcome(string Status, string Code, bool Moved = false);

public sealed class GitDeliveryProcessor(IMethodGitStore store, IGitProvider provider, GitTaskResolver resolver,
    IMethodMoveService movement, ILogger<GitDeliveryProcessor> logger, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    public const string LegacyCandidateEvent = "prisma_legacy_candidate";
    public const string LinkEvaluationEvent = "prisma_link_confirmation";
    private sealed record Candidate(string Type, string Destination, DateTimeOffset OccurredAt, string EntityId, string Revision, long? ReviewId = null);

    public async Task<GitProcessingOutcome> ProcessAsync(GitDeliverySnapshot delivery, CancellationToken ct = default)
    {
        var repository = await store.GetRepositoryAsync(delivery.IntegrationId, ct);
        if (repository is null || !repository.Enabled || repository.ProjectId != delivery.ProjectId)
            return await Finish(delivery, GitDeliveryStates.RetryableFailure, "git_repository_unavailable", ct);
        var e = delivery.Event;
        if (e.RepositoryId != repository.RepositoryId || (e.InstallationId.HasValue && e.InstallationId != repository.InstallationId))
            return await Finish(delivery, GitDeliveryStates.PermanentFailure, "git_repository_mismatch", ct);
        var project = await store.GetProjectAsync(repository.ProjectId, ct);
        if (project is null || !project.Enabled) return await Finish(delivery, GitDeliveryStates.RetryableFailure, "method_disabled", ct);
        if (project.GitPhase == MethodGitPhase.NoGit) return await Finish(delivery, GitDeliveryStates.RetryableFailure, "method_git_disabled", ct);
        if (e.EventName == "ping") return await Finish(delivery, GitDeliveryStates.Ignored, "ping_received", ct);
        if (e.EventName == "create")
        {
            if (e.Action != "branch" || e.BranchName is null) return await Finish(delivery, GitDeliveryStates.Ignored, "unsupported_reference", ct);
            var branchTask = await resolver.ResolveAsync(repository, null, e.BranchName, null, ct);
            if (branchTask.Task is not null)
                await store.LinkInferredAsync(repository.Id, branchTask.Task.WorkItemId, null, e.BranchName, "branch", ct);
            return await Finish(delivery, GitDeliveryStates.Ignored, branchTask.Task is null ? branchTask.Reason ?? "task_not_identified" : "branch_recorded", ct);
        }
        var legacyCandidate = e.EventName == LegacyCandidateEvent && delivery.DeliveryId.StartsWith("legacy:", StringComparison.Ordinal);
        if (legacyCandidate && e.PullRequestNumber is { } legacyNumber)
        {
            var candidatePr = await provider.GetPullRequestAsync(repository, legacyNumber, ct);
            var candidateObservation = await store.UpsertObservationAsync(repository.Id, candidatePr, "legacy_confirmation_required", ct);
            if (candidateObservation.WorkItemId.HasValue) await store.UpsertObservationAsync(repository.Id, candidatePr, null, ct);
            return await Finish(delivery, GitDeliveryStates.Processed, "legacy_confirmation_required", ct);
        }
        var internalEvaluation = e.EventName == LinkEvaluationEvent && delivery.DeliveryId.StartsWith("link:", StringComparison.Ordinal);
        if (e.EventName is not ("pull_request" or "pull_request_review") && !internalEvaluation)
            return await Finish(delivery, GitDeliveryStates.Ignored, "unsupported_event", ct);
        if (e.PullRequestNumber is not { } number) return await Finish(delivery, GitDeliveryStates.PermanentFailure, "missing_pull_request", ct);
        var pr = await provider.GetPullRequestAsync(repository, number, ct);
        var resolution = await resolver.ResolveAsync(repository, number, pr.HeadBranch, pr.Title, ct,
            allowInference: pr.HeadRepositoryId == repository.RepositoryId);
        var observation = await store.UpsertObservationAsync(repository.Id, pr, resolution.Task is null ? resolution.Reason : null, ct);
        if (resolution.Task is null) return await Finish(delivery, GitDeliveryStates.Ignored, resolution.Reason ?? "task_not_identified", ct);
        if (resolution.Source != "explicit")
            await store.LinkInferredAsync(repository.Id, resolution.Task.WorkItemId, observation.Id, resolution.Source == "branch" ? pr.HeadBranch : null, resolution.Source, ct);
        else
            logger.LogInformation("PR {PullRequestNumber} da integração {IntegrationId} resolvido pelo vínculo explícito para {MethodTaskId}; branch/título permanecem na observação para conferência.", number, repository.Id, resolution.Task.MethodTaskId);
        var task = await store.GetTaskAsync(resolution.Task.WorkItemId, ct);
        if (task is null || !task.IsTopLevel || task.IsArchived || task.BoardId != project.BoardId)
            return await Finish(delivery, GitDeliveryStates.Ignored, "method_task_unavailable", ct);
        if (pr.IsDraft && pr.State == "open") return await Finish(delivery, GitDeliveryStates.Ignored, "draft_recorded", ct);
        var reviews = pr.State == "open" || pr.IsMerged || e.EventName == "pull_request_review"
            ? EffectiveReviews(await provider.GetReviewsAsync(repository, number, ct), pr.HeadSha) : [];
        var changesRequested = reviews.Any(review => review.State.Equals("changes_requested", StringComparison.OrdinalIgnoreCase));
        var candidate = SelectCandidate(e, pr, reviews, internalEvaluation);
        if (candidate is null) return await Finish(delivery, GitDeliveryStates.Ignored, "observation_only", ct);
        if (candidate.Type == MethodGitFactCodes.PullRequestMerged && pr.BaseBranch != repository.DefaultBranch)
            return await Finish(delivery, GitDeliveryStates.Ignored, "merge_non_default_branch", ct);
        if (task.ActiveAuthorizationEventId is null || task.AuthorizedAtUtc is null || task.CycleId == Guid.Empty)
            return await Finish(delivery, GitDeliveryStates.Ignored, "execution_not_authorized", ct);
        var changedAt = new[] { task.LastInteractiveChangeAtUtc, task.LastManualChangeAtUtc }.Max();
        if (candidate.OccurredAt < task.AuthorizedAtUtc || (changedAt.HasValue && candidate.OccurredAt <= changedAt))
            return await Finish(delivery, GitDeliveryStates.Ignored, "stale_git_fact", ct);
        var expectedSource = candidate.Type is MethodGitFactCodes.PullRequestOpened or MethodGitFactCodes.PullRequestReadyForReview or MethodGitFactCodes.PullRequestReopened
            ? MethodStatusCodes.InDevelopment : MethodStatusCodes.InReview;
        if (task.StatusCode != expectedSource) return await Finish(delivery, GitDeliveryStates.Ignored, "incompatible_source", ct);
        if (!project.StageMappings.TryGetValue(candidate.Destination, out var destination))
            return await Finish(delivery, GitDeliveryStates.RetryableFailure, "method_mapping", ct);
        var complete = false; var hasOtherOpen = false;
        if (candidate.Type == MethodGitFactCodes.PullRequestMerged)
        {
            if (pr.MergedAtUtc is null || string.IsNullOrEmpty(pr.MergeSha))
                return await Finish(delivery, GitDeliveryStates.RetryableFailure, "merge_evidence_incomplete", ct);
            var check = await CheckRelatedPullRequests(repository, pr, task, ct);
            complete = check.Complete; hasOtherOpen = check.HasOtherOpen;
            changesRequested |= check.HasChangesRequested;
            if (!complete) return await Finish(delivery, GitDeliveryStates.RetryableFailure, check.Reason ?? "git_read_incomplete", ct);
            if (check.ClosedAfterRetainedMerge)
                return await Finish(delivery, GitDeliveryStates.Ignored, "retained_merge_closed_without_merge", ct);
            if (hasOtherOpen) return await Finish(delivery, GitDeliveryStates.RetryableFailure, "merge_waiting_open_pr", ct);
        }
        if (changesRequested && candidate.Destination is MethodStatusCodes.InReview or MethodStatusCodes.InTest)
            return await Finish(delivery, GitDeliveryStates.RetryableFailure, "effective_changes_requested", ct);
        var revision = Digest(candidate.Revision);
        var semanticKey = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', repository.Host, repository.RepositoryId.ToString(CultureInfo.InvariantCulture),
            candidate.EntityId, candidate.Type, revision, task.CycleId.ToString("D"))));
        if (await store.HasAppliedFactAsync(task.ProjectId, task.WorkItemId, semanticKey, ct))
            return await Finish(delivery, GitDeliveryStates.Processed, "already_applied", ct);
        var fact = new VerifiedGitFact(repository.Id, task.ProjectId, task.WorkItemId, task.CycleId, candidate.Type,
            candidate.EntityId, revision, candidate.OccurredAt, pr.HeadSha, pr.MergeSha, semanticKey, pr.Number, candidate.ReviewId,
            complete, hasOtherOpen, changesRequested, e.ObservedGitActorId, e.ObservedGitActorName);
        var result = await movement.MoveAsync(new(task.WorkItemId, destination, $"GitHub: {candidate.Type}; PR #{pr.Number}.",
            GitFact: fact, DeliveryId: delivery.Id, ExpectedRowVersion: task.RowVersion, ExpectedDeliveryRowVersion: delivery.RowVersion), ct);
        if (result.Applied)
        {
            // F1 consumiu inbox/fato/movimento/histórico na mesma transação; não há segunda confirmação.
            return new(GitDeliveryStates.Processed, "moved", true);
        }
        return await Finish(delivery, result.AlreadyApplied ? GitDeliveryStates.Processed : GitDeliveryStates.Ignored,
            result.AlreadyApplied ? "already_applied" : "incompatible_source", ct);
    }
    private static Candidate? SelectCandidate(NormalizedGitEvent e, GitPullRequestSnapshot pr, IReadOnlyList<GitReviewSnapshot> reviews, bool internalEvaluation)
    {
        if (e.EventName == "pull_request_review" || (internalEvaluation && pr.State == "open"))
        {
            var requested = reviews.Where(r => r.State.Equals("changes_requested", StringComparison.OrdinalIgnoreCase)
                && (internalEvaluation || (e.Action == "submitted" && e.ReviewId == r.Id && e.ReviewCommitSha == pr.HeadSha)))
                .OrderByDescending(r => r.SubmittedAtUtc).FirstOrDefault();
            if (requested is not null && pr.State == "open" && !pr.IsMerged)
                return new(MethodGitFactCodes.ChangesRequested, MethodStatusCodes.InDevelopment, requested.SubmittedAtUtc,
                    $"review:{requested.Id}", $"review:{requested.Id}:{requested.CommitSha}:{requested.SubmittedAtUtc:O}", requested.Id);
            if (!internalEvaluation) return null;
        }
        if ((e.Action == "closed" || internalEvaluation) && pr.State == "closed")
        {
            if (pr.IsMerged && pr.MergedAtUtc.HasValue)
                return new(MethodGitFactCodes.PullRequestMerged, MethodStatusCodes.InTest, pr.MergedAtUtc.Value,
                    $"pull:{pr.Number}", $"merge:{pr.Number}:{pr.MergeSha}:{pr.MergedAtUtc:O}");
            if (!pr.IsMerged && pr.ClosedAtUtc.HasValue)
                return new(MethodGitFactCodes.PullRequestClosedWithoutMerge, MethodStatusCodes.InDevelopment, pr.ClosedAtUtc.Value,
                    $"pull:{pr.Number}", $"closed:{pr.Number}:{pr.HeadSha}:{pr.ClosedAtUtc:O}");
            return null;
        }
        if (pr.State != "open" || pr.IsDraft || pr.IsMerged) return null;
        var type = internalEvaluation ? MethodGitFactCodes.PullRequestOpened : e.Action switch
        {
            "opened" => MethodGitFactCodes.PullRequestOpened, "ready_for_review" => MethodGitFactCodes.PullRequestReadyForReview,
            "reopened" => MethodGitFactCodes.PullRequestReopened, _ => null
        };
        var occurred = internalEvaluation ? pr.OpenedAtUtc : e.OccurredAtUtc;
        return type is null || occurred is null ? null : new(type, MethodStatusCodes.InReview, occurred.Value,
            $"pull:{pr.Number}", $"{type}:{pr.Number}:{(internalEvaluation ? pr.HeadSha : e.HeadSha)}:{occurred:O}");
    }
    internal static IReadOnlyList<GitReviewSnapshot> EffectiveReviews(IReadOnlyList<GitReviewSnapshot> reviews, string head)
        => reviews.Where(r => r.CommitSha == head && (r.State.Equals("approved", StringComparison.OrdinalIgnoreCase)
            || r.State.Equals("changes_requested", StringComparison.OrdinalIgnoreCase)))
            .GroupBy(r => r.ReviewerId, StringComparer.Ordinal).Select(group => group.OrderByDescending(r => r.SubmittedAtUtc).ThenByDescending(r => r.Id).First()).ToArray();
    private sealed record RelatedCheck(bool Complete, bool HasOtherOpen, bool HasChangesRequested, bool ClosedAfterRetainedMerge, string? Reason = null);
    private async Task<RelatedCheck> CheckRelatedPullRequests(GitRepositoryDescriptor currentRepository, GitPullRequestSnapshot merged,
        MethodTaskSnapshot task, CancellationToken ct)
    {
        var knownLinks = await store.GetTaskLinksAsync(task.WorkItemId, ct);
        var repositories = await store.ListRepositoriesAsync(task.ProjectId, ct);
        if (repositories.Any(r => !r.Enabled)) return new(false, false, false, false, "git_repository_disabled");
        var otherOpen = false; var changes = false; var closedAfterMerge = false;
        foreach (var repository in repositories)
        {
            var read = await provider.ListPullRequestsForCycleAsync(repository, task.AuthorizedAtUtc!.Value, ct);
            if (!read.Complete) return new(false, otherOpen, changes, closedAfterMerge, read.IncompleteReason);
            if (repository.Id == currentRepository.Id && !read.PullRequests.Any(pr => pr.Number == merged.Number && pr.State == "closed" && pr.IsMerged))
                return new(false, otherOpen, changes, closedAfterMerge, "git_view_not_current");
            foreach (var candidate in read.PullRequests)
            {
                if (repository.Id == currentRepository.Id && candidate.Number == merged.Number) continue;
                var known = knownLinks.Any(link => link.IntegrationId == repository.Id && PullRequestNumber(link.Url) == candidate.Number);
                if (!known && !MayIdentify(candidate, task.MethodTaskId)) continue;
                var resolution = await resolver.ResolveAsync(repository, candidate.Number, candidate.HeadBranch, candidate.Title, ct,
                    allowInference: candidate.HeadRepositoryId == repository.RepositoryId);
                if (resolution.Conflict) return new(false, otherOpen, changes, closedAfterMerge, "ambiguous_related_task");
                if (resolution.Task?.WorkItemId != task.WorkItemId)
                {
                    if (known) return new(false, otherOpen, changes, closedAfterMerge, "related_link_identity_changed");
                    continue;
                }
                if (candidate.State == "open") otherOpen = true;
                else if (!candidate.IsMerged && candidate.ClosedAtUtc.HasValue && candidate.ClosedAtUtc > merged.MergedAtUtc
                    && candidate.ClosedAtUtc >= task.AuthorizedAtUtc) closedAfterMerge = true;
                else if (candidate.IsMerged && candidate.MergedAtUtc >= task.AuthorizedAtUtc)
                    changes |= EffectiveReviews(await provider.GetReviewsAsync(repository, candidate.Number, ct), candidate.HeadSha)
                        .Any(review => review.State.Equals("changes_requested", StringComparison.OrdinalIgnoreCase));
            }
        }
        return new(true, otherOpen, changes, closedAfterMerge);
    }
    private static int? PullRequestNumber(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        return int.TryParse(uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault(), NumberStyles.None,
            CultureInfo.InvariantCulture, out var number) ? number : null;
    }
    private static bool MayIdentify(GitPullRequestSnapshot pr, string id) => pr.HeadBranch.StartsWith($"task/{id}-", StringComparison.Ordinal)
        || pr.Title.Contains($"[{id}]", StringComparison.Ordinal);
    private static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private async Task<GitProcessingOutcome> Finish(GitDeliverySnapshot delivery, string status, string code, CancellationToken ct)
    {
        DateTimeOffset? next = status == GitDeliveryStates.RetryableFailure
            ? _clock.GetUtcNow().AddMinutes(Math.Min(60, Math.Pow(2, Math.Clamp(delivery.Attempts - 1, 0, 6)))) : null;
        var completed = await store.CompleteDeliveryAsync(delivery.Id, delivery.RowVersion, status, code, next, ct);
        return new(completed ? status : "lease_lost", completed ? code : "lease_lost");
    }
}
