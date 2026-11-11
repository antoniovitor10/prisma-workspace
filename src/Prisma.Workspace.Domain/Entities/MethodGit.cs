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
    /// <summary>"app" (GitHub App + webhook) ou "poll" (consulta com token de leitura da organização).</summary>
    public string Mode { get; set; } = GitIntegrationModes.App;
    // Obrigatórios no modo "app"; opcionais só no modo "poll" (CK_GitRepositoryIntegrations_Ids).
    public long? AppId { get; set; }
    public long? InstallationId { get; set; }
    public string? AppPrivateKeySecretRef { get; set; }
    public string? WebhookSecretRef { get; set; }
    public string? PreviousWebhookSecretRef { get; set; }
    public DateTimeOffset? PreviousSecretValidUntilUtc { get; set; }
    public Guid SystemPrincipalId { get; set; }
    public bool Enabled { get; set; }
    /// <summary>Credencial de leitura usada no modo "poll"; nunca guarda o valor aqui.</summary>
    public Guid? ReadCredentialId { get; set; }
    public DateTimeOffset? LastPolledAtUtc { get; set; }
    /// <summary>Código do resultado da última consulta (sem texto do GitHub).</summary>
    public string? LastPollStatus { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public MethodProjectSettings Settings { get; set; } = null!;
    public MethodPrincipal SystemPrincipal { get; set; } = null!;
}

public static class GitIntegrationModes
{
    public const string App = "app";
    public const string Poll = "poll";
}

/// <summary>Token de leitura de uma organização GitHub no projeto; o valor só existe cifrado (Data Protection) e nunca sai da API.</summary>
public sealed class GitReadCredential : IOrganizationOwned
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ProjectId { get; set; }
    /// <summary>Dono no GitHub (organização ou usuário), por exemplo "detranse".</summary>
    public string GitHubOwner { get; set; } = string.Empty;
    public string ProtectedValue { get; set; } = string.Empty;
    /// <summary>Começo do token, só para identificação na tela: "github_pat_" + 5 caracteres (máximo 16).</summary>
    public string TokenPrefix { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public string? RevokedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
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
    /// <summary>Aprovações efetivas na head atual (resumo para o card, sem chamar o GitHub).</summary>
    public int ApprovalsCount { get; set; }
    public bool ChangesRequested { get; set; }
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
    /// <summary>"local" (vinculada antes do push) ou "remote" (já vista no GitHub).</summary>
    public string BranchState { get; set; } = GitBranchStates.Remote;
    public DateTimeOffset? LastSeenAtUtc { get; set; }
    public string Source { get; set; } = string.Empty;
    public Guid? CreatedByPrincipalId { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public GitRepositoryIntegration Integration { get; set; } = null!;
    public GitPullRequestObservation? Observation { get; set; }
}

public static class GitBranchStates
{
    public const string Local = "local";
    public const string Remote = "remote";
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
