using Prisma.Workspace.Domain.Interfaces;

namespace Prisma.Workspace.Domain.Entities;

public sealed class GitRepositoryIntegration : IOrganizationOwned
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ProjectId { get; set; }
    public string Provider { get; set; } = "github";
    public string Host { get; set; } = "github.com";
    public long RepositoryId { get; set; }
    public string RepositoryFullName { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = string.Empty;
    public long AppId { get; set; }
    public long InstallationId { get; set; }
    public string AppPrivateKeySecretRef { get; set; } = string.Empty;
    public string WebhookSecretRef { get; set; } = string.Empty;
    public string? PreviousWebhookSecretRef { get; set; }
    public DateTimeOffset? PreviousSecretValidUntilUtc { get; set; }
    public Guid SystemPrincipalId { get; set; }
    public bool Enabled { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public MethodProjectSettings Settings { get; set; } = null!;
    public MethodPrincipal SystemPrincipal { get; set; } = null!;
}

public sealed class GitPullRequestObservation
{
    public Guid Id { get; set; }
    public Guid IntegrationId { get; set; }
    public Guid ProjectId { get; set; }
    public int PullRequestNumber { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string HeadBranch { get; set; } = string.Empty;
    public string BaseBranch { get; set; } = string.Empty;
    public string HeadSha { get; set; } = string.Empty;
    public string? MergeSha { get; set; }
    public long? HeadRepositoryId { get; set; }
    public string? PullRequestAuthorId { get; set; }
    public string? PullRequestAuthorName { get; set; }
    public string State { get; set; } = string.Empty;
    public bool IsDraft { get; set; }
    public DateTimeOffset ProviderUpdatedAtUtc { get; set; }
    public DateTimeOffset ObservedAtUtc { get; set; }
    public string? UnlinkedReason { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public GitRepositoryIntegration Integration { get; set; } = null!;
}

public sealed class TaskGitLink
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid IntegrationId { get; set; }
    public Guid WorkItemId { get; set; }
    public Guid? PullRequestObservationId { get; set; }
    public string? BranchName { get; set; }
    public string Source { get; set; } = string.Empty;
    public Guid? CreatedByPrincipalId { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public GitRepositoryIntegration Integration { get; set; } = null!;
    public GitPullRequestObservation? Observation { get; set; }
}

public static class GitDeliveryStates
{
    public const string Received = "received";
    public const string Processing = "processing";
    public const string Processed = "processed";
    public const string Ignored = "ignored";
    public const string RetryableFailure = "retryable_failure";
    public const string PermanentFailure = "permanent_failure";
}

public sealed class GitWebhookDelivery
{
    public Guid Id { get; set; }
    public Guid IntegrationId { get; set; }
    public Guid ProjectId { get; set; }
    public string Provider { get; set; } = "github";
    public string DeliveryId { get; set; } = string.Empty;
    public byte[] BodyHash { get; set; } = [];
    public string EventName { get; set; } = string.Empty;
    public string? EventAction { get; set; }
    public string NormalizedEventJson { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public string Status { get; set; } = GitDeliveryStates.Received;
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public DateTimeOffset? ProcessedAtUtc { get; set; }
    public string? LastErrorCode { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
    public GitRepositoryIntegration Integration { get; set; } = null!;
}

/// <summary>Evidência permanente; DeliveryId é correlação lógica sem FK para a inbox expurgável.</summary>
public sealed class GitAppliedFact
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid IntegrationId { get; set; }
    public Guid WorkItemId { get; set; }
    public Guid CycleId { get; set; }
    public byte[] SemanticKey { get; set; } = [];
    public string FactType { get; set; } = string.Empty;
    public string ProviderEntityId { get; set; } = string.Empty;
    public string ProviderRevision { get; set; } = string.Empty;
    public Guid FromStageId { get; set; }
    public Guid ToStageId { get; set; }
    public Guid TaskEventId { get; set; }
    public Guid? DeliveryId { get; set; }
    public DateTimeOffset AppliedAtUtc { get; set; }
    public GitRepositoryIntegration Integration { get; set; } = null!;
}
