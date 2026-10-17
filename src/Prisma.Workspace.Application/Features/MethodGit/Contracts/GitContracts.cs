namespace Prisma.Workspace.Application.Features.MethodGit.Contracts;

public sealed record GitRepositoryDescriptor(
    Guid Id, Guid OrganizationId, Guid ProjectId, string Provider, string Host,
    long RepositoryId, string RepositoryFullName, string DefaultBranch, long AppId, long InstallationId,
    string AppPrivateKeySecretRef, string WebhookSecretRef, string? PreviousWebhookSecretRef,
    DateTimeOffset? PreviousSecretValidUntilUtc, Guid SystemPrincipalId, bool Enabled, byte[]? RowVersion = null,
    string? OperationalError = null);

public sealed record RegisterGitRepositoryRequest(
    string Host, long RepositoryId, string RepositoryFullName, string DefaultBranch,
    long AppId, long InstallationId, string AppPrivateKeySecretRef, string WebhookSecretRef,
    Guid SystemPrincipalId, bool Enabled = true,
    string? PreviousWebhookSecretRef = null, DateTimeOffset? PreviousSecretValidUntilUtc = null);

public sealed record UpdateGitRepositoryRequest(
    bool Enabled, string? DefaultBranch = null, string? AppPrivateKeySecretRef = null,
    string? WebhookSecretRef = null, string? PreviousWebhookSecretRef = null,
    DateTimeOffset? PreviousSecretValidUntilUtc = null);

public sealed record GitRepositoryDto(
    Guid Id, Guid ProjectId, string Host, long RepositoryId, string RepositoryFullName,
    string DefaultBranch, long AppId, long InstallationId, Guid SystemPrincipalId,
    bool Enabled, string? OperationalError);

/// <summary>Corpo normalizado mínimo persistível; nunca contém corpo bruto, segredo ou cabeçalho Bearer.</summary>
public sealed record NormalizedGitEvent(
    string Provider, long RepositoryId, long? InstallationId, string EventName, string? Action,
    DateTimeOffset OccurredAtUtc, string ProviderRevision, string? BranchName,
    int? PullRequestNumber, string? PullRequestUrl, string? Title, string? HeadBranch,
    string? BaseBranch, string? HeadSha, string? MergeSha, string? State,
    bool IsDraft = false, bool IsMerged = false,
    long? ReviewId = null, string? ReviewState = null, string? ReviewCommitSha = null,
    string? ObservedGitActorId = null, string? ObservedGitActorName = null, long? HeadRepositoryId = null);

public sealed record GitWebhookInput(
    Guid IntegrationId, string DeliveryId, string EventName, string Signature,
    ReadOnlyMemory<byte> OriginalBody, string CorrelationId);
public sealed record GitDeliveryReceipt(Guid DeliveryId, bool Duplicate, bool Conflict, string Status);
public sealed record GitDeliveryLease(Guid Id, Guid IntegrationId, Guid ProjectId, byte[] RowVersion);
public sealed record GitDeliverySnapshot(
    Guid Id, Guid IntegrationId, Guid ProjectId, string Provider, string DeliveryId,
    NormalizedGitEvent Event, string Status, int Attempts, string CorrelationId, byte[] RowVersion);

public sealed record GitPullRequestSnapshot(
    int Number, string Url, string Title, string HeadBranch, string BaseBranch,
    string HeadSha, string? MergeSha, string State, bool IsDraft, bool IsMerged,
    DateTimeOffset UpdatedAtUtc, DateTimeOffset? OpenedAtUtc = null,
    DateTimeOffset? MergedAtUtc = null, DateTimeOffset? ReadyForReviewAtUtc = null,
    DateTimeOffset? ClosedAtUtc = null, long? HeadRepositoryId = null,
    string? PullRequestAuthorId = null, string? PullRequestAuthorName = null);
public sealed record GitReviewSnapshot(long Id, string ReviewerId, string State, string CommitSha, DateTimeOffset SubmittedAtUtc);
public sealed record GitPullRequestReadSet(IReadOnlyList<GitPullRequestSnapshot> PullRequests, bool Complete, string? IncompleteReason = null);
public sealed record GitInstallationValidation(bool Allowed, string? ErrorCode, string? Message);

public sealed record GitPullRequestObservationDto(
    Guid Id, Guid IntegrationId, Guid ProjectId, int PullRequestNumber, string Url,
    string Title, string HeadBranch, string BaseBranch, string State, bool IsDraft,
    Guid? WorkItemId, string? MethodTaskId, string? UnlinkedReason, DateTimeOffset UpdatedAtUtc);
public sealed record GitPullRequestPage(IReadOnlyList<GitPullRequestObservationDto> Items, int Page, int PageSize, int Total);
public sealed record LinkObservedPullRequestRequest(Guid WorkItemId, string Reason);
public sealed record TaskGitLinkDto(
    Guid Id, Guid IntegrationId, Guid WorkItemId, Guid? ObservationId, string? BranchName,
    string Source, string? Url, Guid? CreatedByPrincipalId, string? CreatedByUserId, DateTimeOffset CreatedAtUtc);
public sealed record GitTaskResolution(MethodTaskSnapshot? Task, string Source, string? Reason, bool Conflict = false);

/// <summary>Prova produzida pelo provedor/processador interno. Não é um contrato de entrada HTTP.</summary>
public sealed record VerifiedGitFact(
    Guid IntegrationId, Guid ProjectId, Guid WorkItemId, Guid CycleId,
    string FactType, string ProviderEntityId, string ProviderRevision,
    DateTimeOffset OccurredAtUtc, string HeadSha, string? MergeSha,
    byte[] SemanticKey, int? PullRequestNumber = null, long? ReviewId = null,
    bool OpenPullRequestsReadComplete = false, bool HasOtherOpenPullRequests = false,
    bool HasEffectiveChangesRequested = false,
    string? ObservedGitActorId = null, string? ObservedGitActorName = null);
