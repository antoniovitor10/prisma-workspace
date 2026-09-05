namespace Detran.Kanban.Application.Features.Boards.Dtos;

/// <summary>
/// DTO de estatísticas de Lead Time por Etapa do Kanban.
/// </summary>
public class StageLeadTimeDto
{
    public Guid StageId { get; set; }
    public string StageName { get; set; } = string.Empty;
    public double AverageSeconds { get; set; }
    public double AverageHours { get; set; }
    public double AverageDays { get; set; }
    public int ItemsCount { get; set; }
}
