namespace Prisma.Workspace.Domain.Entities;

/// <summary>
/// Associação entre um item de trabalho e um usuário responsável (chave composta).
/// </summary>
public class WorkItemAssignee
{
    /// <summary>Identificador do item de trabalho.</summary>
    public Guid WorkItemId { get; set; }

    /// <summary>Identificador do usuário responsável.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Data/hora da atribuição.</summary>
    public DateTimeOffset AssignedAt { get; set; }

    /// <summary>Ordem pessoal da tarefa na fila "Tarefas para mim" do usuário.</summary>
    public int PersonalPriority { get; set; }

    // ── Navegação ──────────────────────────────────────────────

    /// <summary>Item de trabalho associado.</summary>
    public WorkItem WorkItem { get; set; } = null!;
}
