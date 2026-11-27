namespace Prisma.Workspace.Application.Features.MethodGit.Contracts;

public sealed record GitRepositoryDescriptor(
    Guid Id, Guid OrganizationId, Guid ProjectId, string Provider, string Host,
    long RepositoryId, string RepositoryFullName, string DefaultBranch, long AppId, long InstallationId,
    string AppPrivateKeySecretRef, string WebhookSecretRef, string? PreviousWebhookSecretRef,
    DateTimeOffset? PreviousSecretValidUntilUtc, Guid SystemPrincipalId, bool Enabled, byte[]? RowVersion = null,
    string? OperationalError = null, string Mode = "app", Guid? ReadCredentialId = null,
    DateTimeOffset? LastPolledAtUtc = null, string? LastPollStatus = null)
{
    /// <summary>Modo consulta: sem App nem webhook; o provedor lê com o token de leitura da organização.</summary>
    public bool IsPoll => Mode == "poll";
}

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
    string DefaultBranch, long? AppId, long? InstallationId, Guid SystemPrincipalId,
    bool Enabled, string? OperationalError, string Mode = "app", Guid? ReadCredentialId = null,
    DateTimeOffset? LastPolledAtUtc = null, string? LastPollStatus = null);

/// <summary>Cadastro no modo consulta (sem App nem webhook): o ID do repositório vem do GitHub, lido com o token da organização.</summary>
public sealed record RegisterPollGitRepositoryRequest(string RepositoryFullName, string DefaultBranch, Guid SystemPrincipalId, bool Enabled = true);

/// <summary>Token de leitura de uma organização GitHub: só prefixo, autoria e datas; o valor nunca sai da API.</summary>
public sealed record GitReadCredentialDto(Guid Id, string GitHubOwner, string TokenPrefix, DateTimeOffset CreatedAtUtc,
    string? CreatedByName, DateTimeOffset? RevokedAtUtc, int RepositoriesCount);
/// <summary>Cadastro ou troca do token da organização; o valor é cifrado ao gravar e nunca devolvido.</summary>
public sealed record SaveGitReadCredentialRequest(string GitHubOwner, string Token)
{
    // Nunca imprime o token (logs, exceções com o objeto, depuração).
    public override string ToString() => $"SaveGitReadCredentialRequest {{ GitHubOwner = {GitHubOwner} }}";
}
/// <summary>Repositório visto no GitHub pelo modo consulta (GET repos/{dono}/{nome}).</summary>
public sealed record GitRepositoryIdentity(long Id, string FullName, string DefaultBranch);

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
    string Source, string? Url, Guid? CreatedByPrincipalId, string? CreatedByUserId, DateTimeOffset CreatedAtUtc,
    string BranchState = "remote", DateTimeOffset? LastSeenAtUtc = null, string? RepositoryFullName = null,
    int? PullRequestNumber = null, string? PullRequestState = null);
/// <summary>Resultado de ver uma branch no GitHub: sem vínculo, registrada, ou vínculo cuja task está indisponível (nada gravado).</summary>
public enum GitBranchPushResult { NotLinked, Recorded, TaskUnavailable }
/// <summary>Vínculo de branch antes do push; o repositório é o nome completo de um repositório do projeto.</summary>
public sealed record LinkMethodBranchRequest(string Repository, string Branch);
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

/// <summary>Aba Git da tarefa (HU-0002, T-0006): vínculos, PRs com revisões, linha do tempo e quem pode vincular.</summary>
public sealed record TaskGitViewDto(
    Guid WorkItemId, string MethodTaskId, bool CanLink,
    IReadOnlyList<TaskGitRepositoryOptionDto> Repositories, IReadOnlyList<TaskGitBranchDto> Branches,
    IReadOnlyList<TaskGitPullRequestDto> PullRequests, IReadOnlyList<TaskGitTimelineEntryDto> Timeline);
/// <summary>Repositório habilitado do projeto, para o vínculo manual (o modo e a última consulta servem ao "Consultar agora").</summary>
public sealed record TaskGitRepositoryOptionDto(Guid Id, string RepositoryFullName, string Mode,
    DateTimeOffset? LastPolledAtUtc = null, string? LastPollStatus = null);
public sealed record TaskGitBranchDto(
    Guid? LinkId, Guid IntegrationId, string RepositoryFullName, string BranchName, string BranchState,
    DateTimeOffset? LastSeenAtUtc, string Source, DateTimeOffset CreatedAtUtc, string Url);
public sealed record TaskGitPullRequestDto(
    Guid LinkId, Guid IntegrationId, string RepositoryFullName, int Number, string Title, string State,
    bool IsDraft, bool Merged, int ApprovalsCount, bool ChangesRequested, string Url, string HeadBranch,
    string BaseBranch, string? AuthorName, string Source, DateTimeOffset UpdatedAtUtc, DateTimeOffset LinkedAtUtc);
/// <summary>
/// Um fato da linha do tempo do Git. Kind: git_branch_linked, git_branch_pushed, git_pr_linked, git_pr_unlinked,
/// git_pr_inferred, git_fact (movimento por fato Git, com FactType), git_pr_merged ou git_pr_closed (observados sem movimento).
/// Channel: ui, mcp, webhook, consulta ou nulo (automático, sem registro do canal).
/// </summary>
public sealed record TaskGitTimelineEntryDto(
    string Kind, DateTimeOffset AtUtc, string? ActorName, string? Channel, string? RepositoryFullName = null,
    string? Branch = null, int? PullRequestNumber = null, string? Url = null, string? FactType = null,
    string? FromStatus = null, string? ToStatus = null);

/// <summary>Resultado de uma consulta do modo sem admin (HU-0002, T-0008): código sem texto do GitHub e a hora da última consulta com sucesso.</summary>
public sealed record GitPollResultDto(Guid IntegrationId, string RepositoryFullName, string Status, bool Success,
    DateTimeOffset? LastPolledAtUtc, int PullRequestsEvaluated, int BranchesPushed);
