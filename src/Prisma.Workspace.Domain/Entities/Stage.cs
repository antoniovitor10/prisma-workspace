using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Domain.Entities;

/// <summary>
/// Etapa (coluna) do fluxo do projeto. O quadro é só visão; as colunas pertencem ao projeto (D83).
/// </summary>
public class Stage
{
    /// <summary>Identificador único da etapa.</summary>
    public Guid Id { get; set; }

    /// <summary>Projeto ao qual esta etapa pertence.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Status de negócio representado por esta coluna.</summary>
    public Guid? WorkflowStatusId { get; set; }

    /// <summary>Nome da etapa.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Posição ordinal da etapa dentro do fluxo do projeto.</summary>
    public double Position { get; set; }

    /// <summary>Categoria semântica usada por backlog e métricas.</summary>
    public StageCategory Category { get; set; } = StageCategory.InProgress;

    /// <summary>Data/hora de criação.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    // ── Navegação ──────────────────────────────────────────────

    /// <summary>Projeto ao qual esta etapa pertence.</summary>
    public Project Project { get; set; } = null!;

    public WorkflowStatus? WorkflowStatus { get; set; }

    /// <summary>Itens de trabalho atualmente nesta etapa.</summary>
    public ICollection<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
}
