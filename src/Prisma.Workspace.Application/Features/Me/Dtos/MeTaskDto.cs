namespace Prisma.Workspace.Application.Features.Me.Dtos;

/// <summary>
/// Item da fila "Tarefas para mim", com os tempos agregados que o card exibe.
/// </summary>
public class MeTaskDto
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    public string BoardName { get; set; } = string.Empty;
    public Guid? StageId { get; set; }
    public string? StageName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public int Priority { get; set; }
    public decimal? EstimatedHours { get; set; }
    public string? DueDate { get; set; }
    public int PersonalPriority { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public int TotalTimeSeconds { get; set; }
    public int UserTimeSeconds { get; set; }
}

/// <summary>Cronômetro ativo exibido na topbar.</summary>
public class ActiveTimerDto
{
    public Guid Id { get; set; }
    public Guid WorkItemId { get; set; }
    public string? WorkItemTitle { get; set; }
    public DateTimeOffset StartedAt { get; set; }
}
