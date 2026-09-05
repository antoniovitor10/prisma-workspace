namespace Prisma.Workspace.Application.Features.MeTime.Dtos;

/// <summary>Horas apontadas em uma tarefa num dia (aba "Ajustar tarefas").</summary>
public class TaskDayTimeDto
{
    public Guid WorkItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Seconds { get; set; }
}

/// <summary>Justificativa de dia (férias, atestado, feriado...).</summary>
public class DayJustificationDto
{
    public Guid Id { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal Hours { get; set; }
}
