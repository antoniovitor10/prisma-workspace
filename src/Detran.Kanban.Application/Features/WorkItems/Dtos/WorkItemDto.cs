using Detran.Kanban.Domain.Enums;

namespace Detran.Kanban.Application.Features.WorkItems.Dtos;

/// <summary>
/// DTO de retorno de um WorkItem (Tarefa/Card).
/// </summary>
public class WorkItemDto
{
    public Guid Id { get; set; }
    public long Number { get; set; }
    public Guid BoardId { get; set; }
    public Guid? TeamId { get; set; }
    public Guid? StageId { get; set; }
    public Guid? WorkflowStatusId { get; set; }
    public string? StatusName { get; set; }
    public string? StatusColor { get; set; }
    public Guid? ParentId { get; set; }
    public Guid? SprintId { get; set; }
    public WorkItemKind Kind { get; set; }
    public WorkItemOrigin Origin { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Description { get; set; }
    public Priority Priority { get; set; }
    public string? ResponsibleId { get; set; }
    public string? TeamName { get; set; }
    public string? RequesterId { get; set; }
    public string? RequesterName { get; set; }
    public string? RequesterEmail { get; set; }
    public decimal? EstimatedHours { get; set; }
    public decimal? RemainingHours { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateOnly? StartDate { get; set; }
    public string? AcceptanceCriteria { get; set; }
    public double Position { get; set; }
    public decimal BacklogRank { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public bool IsArchived { get; set; }
    public string Version { get; set; } = string.Empty;
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public IReadOnlyList<WorkItemAssigneeDto> Assignees { get; set; } = Array.Empty<WorkItemAssigneeDto>();
    public int SubItemsCount { get; set; }
    public int AttachmentsCount { get; set; }
    public int TotalTimeSeconds { get; set; }
    public int UserTimeSeconds { get; set; }

    // Fase 2 — taxonomia
    public Guid? TaskTypeId { get; set; }
    public string? TaskTypeName { get; set; }
    public string? TaskTypeColor { get; set; }
    public int? Points { get; set; }
    public IReadOnlyList<TagDto> Tags { get; set; } = Array.Empty<TagDto>();
    public int ChecklistTotal { get; set; }
    public int ChecklistDone { get; set; }
    public bool IsBlocked { get; set; }
    public IReadOnlyList<WorkItemCustomValueDto> CustomFields { get; set; } = Array.Empty<WorkItemCustomValueDto>();
    /// <summary>Quadros em que o item aparece (quadro home + placements extras).</summary>
    public IReadOnlyList<Guid>? BoardIds { get; set; }
}

public record WorkItemCustomValueDto(Guid FieldId, string? Value);

public class WorkItemAssigneeDto
{
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset AssignedAt { get; set; }
}

public class TagDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
}
