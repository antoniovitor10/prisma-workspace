namespace Prisma.Workspace.Application.Features.TimeEntries.Dtos;

/// <summary>
/// DTO de retorno de lancamento de tempo.
/// </summary>
public class TimeEntryDto
{
    public Guid Id { get; set; }
    public Guid WorkItemId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int? DurationSeconds { get; set; }
}

public class TimeTotalDto
{
    public Guid? WorkItemId { get; set; }
    public Guid? BoardId { get; set; }
    public string? UserId { get; set; }
    public int TotalSeconds { get; set; }
    public decimal TotalHours { get; set; }
}

/// <summary>
/// Total de tempo de um dia (para a visao semanal "Meu Tempo").
/// </summary>
public class DailyTimeDto
{
    public string Date { get; set; } = string.Empty;   // yyyy-MM-dd (fuso America/Sao_Paulo)
    public int TotalSeconds { get; set; }
    public decimal TotalHours { get; set; }
}

/// <summary>
/// Resposta da visao semanal: inicio da semana + 7 dias.
/// </summary>
public class WeeklyTimeDto
{
    public string WeekStart { get; set; } = string.Empty;
    public IReadOnlyList<DailyTimeDto> Days { get; set; } = new List<DailyTimeDto>();
}
