namespace Prisma.Workspace.Domain.Entities;

/// <summary>
/// Registro histórico de permanência de um item de trabalho em uma etapa.
/// Usado para calcular lead time por etapa.
/// </summary>
public class StageHistory
{
    /// <summary>Identificador único do registro.</summary>
    public Guid Id { get; set; }

    /// <summary>Identificador do item de trabalho.</summary>
    public Guid WorkItemId { get; set; }

    /// <summary>Identificador da etapa.</summary>
    public Guid StageId { get; set; }

    /// <summary>Data/hora em que o item entrou na etapa.</summary>
    public DateTimeOffset EnteredAt { get; set; }

    /// <summary>Data/hora em que o item saiu da etapa. Nulo = ainda nesta etapa.</summary>
    public DateTimeOffset? LeftAt { get; set; }

    /// <summary>Usuário que realizou a transição, quando autenticado.</summary>
    public string? ActorId { get; set; }

    /// <summary>Nome funcional preservado no momento da transição.</summary>
    public string? ActorName { get; set; }

    /// <summary>Motivo opcional informado para a movimentação.</summary>
    public string? Reason { get; set; }

    // ── Navegação ──────────────────────────────────────────────

    /// <summary>Item de trabalho associado.</summary>
    public WorkItem WorkItem { get; set; } = null!;

    /// <summary>Etapa associada.</summary>
    public Stage Stage { get; set; } = null!;
}
