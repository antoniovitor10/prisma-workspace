using Detran.Kanban.Domain.Interfaces;

namespace Detran.Kanban.Domain.Entities;

/// <summary>
/// Justificativa de um dia no timesheet (férias, atestado, feriado etc.).
/// Abate horas da meta do dia no "Meu Tempo".
/// </summary>
public class DayJustification : IOrganizationOwned
{
    /// <summary>Identificador único.</summary>
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    /// <summary>Usuário dono da justificativa.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Dia justificado.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Motivo (ex.: "Férias", "Atestado", "Feriado").</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Horas justificadas no dia.</summary>
    public decimal Hours { get; set; }

    /// <summary>Data/hora de criação.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
