using Detran.Kanban.Domain.Entities;

namespace Detran.Kanban.Application.Features.TaskFeed.Dtos;

/// <summary>Comentário de usuário no feed da tarefa.</summary>
public class CommentDto
{
    public Guid Id { get; init; }
    public Guid WorkItemId { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }

    public static CommentDto From(Comment c) => new()
    {
        Id = c.Id,
        WorkItemId = c.WorkItemId,
        UserId = c.UserId,
        DisplayName = c.UserName,
        UserName = c.UserName,
        Text = c.Content,
        CreatedAt = c.CreatedAt
    };
}

/// <summary>Evento de sistema no feed da tarefa.</summary>
public class TaskEventDto
{
    public Guid Id { get; init; }
    public Guid WorkItemId { get; init; }
    public string ActorId { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string? Payload { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    public static TaskEventDto From(TaskEvent e) => new()
    {
        Id = e.Id,
        WorkItemId = e.WorkItemId,
        ActorId = e.ActorId,
        Kind = e.Kind,
        Payload = e.Payload,
        CreatedAt = e.CreatedAt
    };
}

/// <summary>Snapshot navegavel do caminho percorrido pela tarefa entre etapas.</summary>
public sealed record TaskStateGraphDto(
    IReadOnlyList<TaskStateNodeDto> Nodes,
    IReadOnlyList<TaskStateEdgeDto> Edges);

public sealed record TaskStateNodeDto(
    Guid Id,
    string Name,
    int Visits,
    long TotalSeconds,
    bool IsCurrent);

public sealed record TaskStateEdgeDto(
    Guid Id,
    Guid Source,
    Guid Target,
    DateTimeOffset OccurredAt,
    string? ActorId,
    string? ActorName,
    string? Reason);
