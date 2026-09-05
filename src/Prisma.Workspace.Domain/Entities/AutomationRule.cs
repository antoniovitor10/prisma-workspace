using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Domain.Entities;

/// <summary>
/// Regra de automação reativa do quadro Kanban.
/// </summary>
public class AutomationRule
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    public Guid TriggerStageId { get; set; }
    public AutomationActionType ActionType { get; set; }
    public string ActionValue { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Navegação
    public Board Board { get; set; } = null!;
    public Stage TriggerStage { get; set; } = null!;
}
