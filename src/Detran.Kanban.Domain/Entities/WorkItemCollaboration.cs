using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Domain.Exceptions;

namespace Detran.Kanban.Domain.Entities;

public class WorkItemLink
{
    public Guid Id { get; set; }
    public Guid SourceWorkItemId { get; set; }
    public Guid TargetWorkItemId { get; set; }
    public WorkItemLinkType Type { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public WorkItem SourceWorkItem { get; set; } = null!;
    public WorkItem TargetWorkItem { get; set; } = null!;

    public static WorkItemLink Create(
        Guid sourceWorkItemId,
        Guid targetWorkItemId,
        WorkItemLinkType type,
        string actorId)
    {
        DomainException.Garantir(sourceWorkItemId != targetWorkItemId,
            "Uma tarefa nao pode depender dela mesma.");
        return new WorkItemLink
        {
            Id = Guid.NewGuid(),
            SourceWorkItemId = sourceWorkItemId,
            TargetWorkItemId = targetWorkItemId,
            Type = type,
            CreatedBy = actorId,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}

public class WorkItemFollower
{
    public Guid WorkItemId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset FollowedAt { get; set; }
    public WorkItem WorkItem { get; set; } = null!;
}

public class WorkItemCustomFieldValue
{
    public Guid WorkItemId { get; set; }
    public Guid FieldDefinitionId { get; set; }
    public string? Value { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
    public WorkItem WorkItem { get; set; } = null!;
    public ProjectCustomFieldDefinition FieldDefinition { get; set; } = null!;
}
