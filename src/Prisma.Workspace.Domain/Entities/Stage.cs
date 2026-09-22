using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Domain.Entities;

/// <summary>
/// Etapa (coluna) independente de um quadro. Etapas legadas mantêm <see cref="BoardId"/>
/// nulo apenas para preservar o histórico anterior à migração de colunas por quadro.
/// </summary>
public class Stage
{
    /// <summary>Identificador único da etapa.</summary>
    public Guid Id { get; set; }

    /// <summary>Projeto ao qual esta etapa pertence.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Quadro ao qual a etapa operacional pertence. Nulo somente em etapa legada histórica.</summary>
    public Guid? BoardId { get; set; }

    /// <summary>
    /// Etapa compartilhada de origem da qual esta etapa foi clonada na migração.
    /// Nulo em etapas legadas e em etapas criadas após a migração.
    /// </summary>
    public Guid? LegacyStageId { get; set; }

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

    /// <summary>Quadro proprietário da etapa operacional.</summary>
    public Board? Board { get; set; }

    /// <summary>Etapa compartilhada preservada como referência históica.</summary>
    public Stage? LegacyStage { get; set; }

    public WorkflowStatus? WorkflowStatus { get; set; }

    /// <summary>Itens de trabalho atualmente nesta etapa.</summary>
    public ICollection<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
}
