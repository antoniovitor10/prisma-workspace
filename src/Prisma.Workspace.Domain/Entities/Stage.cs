using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Domain.Entities;

/// <summary>
/// Etapa (coluna) de um quadro Kanban.
/// </summary>
public class Stage
{
    /// <summary>Identificador único da etapa.</summary>
    public Guid Id { get; set; }

    /// <summary>Identificador do quadro ao qual a etapa pertence.</summary>
    public Guid BoardId { get; set; }

    /// <summary>Status de negócio representado por esta coluna.</summary>
    public Guid? WorkflowStatusId { get; set; }

    /// <summary>Nome da etapa.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Posição ordinal da etapa dentro do quadro.</summary>
    public double Position { get; set; }

    /// <summary>Limite de trabalho em progresso (WIP). Nulo = sem limite.</summary>
    public int? WipLimit { get; set; }

    /// <summary>Categoria semântica usada por backlog e métricas.</summary>
    public StageCategory Category { get; set; } = StageCategory.InProgress;

    /// <summary>Data/hora de criação.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    // ── Navegação ──────────────────────────────────────────────

    /// <summary>Quadro ao qual esta etapa pertence.</summary>
    public Board Board { get; set; } = null!;

    public WorkflowStatus? WorkflowStatus { get; set; }

    /// <summary>Itens de trabalho atualmente nesta etapa.</summary>
    public ICollection<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
}
