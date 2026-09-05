namespace Detran.Kanban.Application.Features.Stages.Dtos;

/// <summary>
/// DTO de retorno para uma etapa (Stage).
/// </summary>
public class StageDto
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Position { get; set; }
    public int? WipLimit { get; set; }
    public Guid? WorkflowStatusId { get; set; }
    public string? StatusName { get; set; }
    public string? StatusColor { get; set; }
    public bool IsInitial { get; set; }
    public bool IsFinal { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
