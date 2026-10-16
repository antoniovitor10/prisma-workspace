using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Features.MethodGit.Contracts;

/// <summary>Identidade produzida somente pela autenticação validada ou pelo escopo interno do worker.</summary>
public sealed record MethodActorIdentity(
    MethodAuthenticationKind AuthenticationKind, MethodRole Role,
    Guid? PrincipalId, string ActorId, string DisplayName,
    Guid? OrganizationId, Guid? ProjectId, Guid? TokenId, DateTimeOffset? ExpiresAtUtc,
    IReadOnlySet<PlatformPermission> PermissionCeiling, string Channel, string CorrelationId)
{
    public bool IsInteractiveHuman => AuthenticationKind == MethodAuthenticationKind.UiSession && Role == MethodRole.Human;
}

public sealed record MethodProjectSnapshot(
    Guid OrganizationId, Guid ProjectId, string ProjectName, Guid BoardId, string BoardName,
    Guid MethodIdFieldId, Guid HuFieldId, Guid PrFieldId, bool Enabled,
    MethodGitPhase GitPhase, int AutonomyLevel, int PolicyVersion,
    IReadOnlyDictionary<string, Guid> StageMappings,
    IReadOnlyDictionary<string, Guid>? Fields = null);

/// <summary>Estado canônico vem do WorkItem; o restante comprova autorização, bloqueio e concorrência.</summary>
public sealed record MethodTaskSnapshot(
    Guid OrganizationId, Guid ProjectId, Guid BoardId, Guid WorkItemId, string MethodTaskId,
    Guid? StageId, string StatusCode, Guid CycleId,
    Guid? ActiveAuthorizationEventId, DateTimeOffset? AuthorizedAtUtc,
    DateTimeOffset? LastInteractiveChangeAtUtc, Guid? BlockingEventId,
    Guid? BlockingPrincipalId, string? BlockingOriginStatusCode, Guid? ResumeApprovalEventId,
    bool IsTopLevel, bool IsArchived, byte[] RowVersion);

public sealed record MethodMutation(
    Guid ProjectId, Guid BoardId, Guid? WorkItemId, Guid? FromStageId, Guid? ToStageId,
    string Reason, bool IsCreation = false, bool ChangesMethodIdentity = false,
    Guid? DestinationProjectId = null, Guid? DestinationBoardId = null,
    VerifiedGitFact? GitFact = null);

public sealed record MethodTransitionDecision(bool Allowed, string Code, string Message)
{
    public static MethodTransitionDecision Permit() => new(true, "allowed", "Operação permitida.");
    public static MethodTransitionDecision Deny(string code, string message) => new(false, code, message);
}

public sealed class MethodOperationException : Exception
{
    public int StatusCode { get; }
    public string Code { get; }
    public MethodOperationException(int statusCode, string code, string message) : base(message)
        => (StatusCode, Code) = (statusCode, code);
}

// DTOs HTTP: JSON camelCase; papel/códigos de status usam os valores estáveis do método.
public sealed record MethodIdentityDto(
    string AuthenticationKind, string Role, Guid? PrincipalId, string UserId, string DisplayName,
    Guid OrganizationId, Guid ProjectId, string ProjectName, Guid BoardId, string BoardName,
    Guid? TokenId, DateTimeOffset? ExpiresAtUtc, IReadOnlyList<string> Capabilities);

public sealed record MethodRulesDto(
    MethodIdentityDto Identity, MethodProjectSnapshot Project,
    IReadOnlyDictionary<string, IReadOnlyList<string>> AllowedTransitions,
    IReadOnlyList<string> Conventions, bool RequiresInteractiveAuthorization = true,
    bool RequiresInteractiveResumeApproval = true);

public sealed record IssueMethodTokenRequest(
    string Name, string Role, int ValidDays = 30, Guid? PrincipalId = null,
    IReadOnlyList<PlatformPermission>? Permissions = null, string? PrincipalDisplayName = null);
public sealed record MethodTokenDto(
    Guid Id, Guid PrincipalId, string Name, string PrincipalDisplayName, string Role,
    string TokenPrefix, DateTimeOffset IssuedAtUtc, DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc, DateTimeOffset? LastUsedAtUtc,
    IReadOnlyList<PlatformPermission> Permissions);
public sealed record IssuedMethodTokenDto(MethodTokenDto Metadata, string Token);
public sealed record ProvisionMethodProjectRequest(Guid? BoardId = null, int AutonomyLevel = 0);
public sealed record UpdateMethodProjectRequest(bool Enabled, int AutonomyLevel);

public sealed record MethodTaskDto(
    Guid WorkItemId, string Id, string Title, string Status, string? Hu,
    string? FilePath, IReadOnlyList<string> Covers, string? Risk, string? Pr,
    IReadOnlyList<string> DependsOn, bool Authorized, bool CanResume,
    Guid? CycleId = null, Guid? BlockingEventId = null, Guid? BlockingPrincipalId = null,
    string? BlockingOriginStatus = null);
public sealed record CreateMethodTaskRequest(
    string Id, string Title, string Hu, string FilePath, IReadOnlyList<string> Covers,
    string Risk, string Status = MethodStatusCodes.Backlog);
public sealed record UpdateMethodTaskRequest(
    string Title, string Hu, string FilePath, IReadOnlyList<string> Covers, string Risk);
public sealed record MoveMethodTaskRequest(string NewStatus, string Reason);
public sealed record MethodCommentRequest(string Text);
public sealed record MethodCommentDto(Guid Id, Guid WorkItemId, string Text, string Author, DateTimeOffset CreatedAtUtc);
public sealed record LinkMethodPullRequestRequest(string Url, string Reason = "Vínculo explícito do PR.");
public sealed record ResumeMethodTaskRequest(Guid BlockingEventId, Guid CycleId, string Reason);
public sealed record ResumeMethodTaskApprovalDto(Guid EventId, Guid WorkItemId, Guid BlockingEventId, Guid CycleId);

/// <summary>Comando interno. Nenhum DTO HTTP aceita GitFact, principal ou canal do chamador.</summary>
public sealed record MethodMoveRequest(
    Guid WorkItemId, Guid DestinationStageId, string Reason, double Position = 100,
    VerifiedGitFact? GitFact = null, Guid? DeliveryId = null, byte[]? ExpectedRowVersion = null, byte[]? ExpectedDeliveryRowVersion = null);
public sealed record MethodMoveResult(
    Guid WorkItemId, Guid? FromStageId, Guid ToStageId, bool Applied, bool AlreadyApplied,
    Guid? TaskEventId, Guid? StageHistoryId, string Code);
