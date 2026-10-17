using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Interfaces;

namespace Prisma.Workspace.Domain.Entities;

public sealed class MethodProjectSettings : IOrganizationOwned
{
    public Guid ProjectId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? BoardId { get; set; }
    public Guid MethodIdFieldId { get; set; }
    public Guid HuFieldId { get; set; }
    public Guid PrFieldId { get; set; }
    public bool Enabled { get; set; }
    public MethodGitPhase GitPhase { get; set; }
    public int AutonomyLevel { get; set; }
    public int PolicyVersion { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<MethodStageMapping> StageMappings { get; set; } = new List<MethodStageMapping>();
}

public sealed class MethodStageMapping
{
    public Guid ProjectId { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public Guid StageId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public MethodProjectSettings Settings { get; set; } = null!;
}

public sealed class MethodPrincipal : IOrganizationOwned
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ProjectId { get; set; }
    public MethodRole Role { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? HumanUserId { get; set; }
    public string DelegatedByUserId { get; set; } = string.Empty;
    public string PermissionCeilingJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class MethodApiToken
{
    public Guid Id { get; set; }
    public Guid PrincipalId { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public byte[] TokenHash { get; set; } = [];
    public string TokenPrefix { get; set; } = string.Empty;
    public string IssuedByUserId { get; set; } = string.Empty;
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public string? RevokedByUserId { get; set; }
    public DateTimeOffset? LastUsedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public MethodPrincipal Principal { get; set; } = null!;
}

/// <summary>Índice do método e prova do ciclo; StageId permanece somente no WorkItem.</summary>
public sealed class MethodTaskState : IOrganizationOwned
{
    public Guid WorkItemId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ProjectId { get; set; }
    public string? MethodTaskId { get; set; }
    public Guid CycleId { get; set; }
    public Guid? ActiveAuthorizationEventId { get; set; }
    public Guid? BlockingEventId { get; set; }
    public Guid? BlockingPrincipalId { get; set; }
    public Guid? ResumeApprovalEventId { get; set; }
    public Guid? LastObservedFactId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>Prova append-only. Autoridade vem do contexto validado e do TaskEvent correlacionado.</summary>
public sealed class MethodAuthorizationEvent : IOrganizationOwned
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid WorkItemId { get; set; }
    public Guid CycleId { get; set; }
    public string EventKind { get; set; } = string.Empty;
    public Guid? RelatedEventId { get; set; }
    public Guid? ActorPrincipalId { get; set; }
    public string? ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public Guid TaskEventId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
