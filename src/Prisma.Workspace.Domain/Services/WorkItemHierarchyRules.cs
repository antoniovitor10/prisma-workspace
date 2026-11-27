using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Domain.Services;

public static class WorkItemHierarchyRules
{
    public static bool IsAllowed(WorkItemKind parent, WorkItemKind child)
        => (parent, child) switch
        {
            (WorkItemKind.Epic, WorkItemKind.Feature) => true,
            (WorkItemKind.Epic, WorkItemKind.UserStory) => true,
            (WorkItemKind.Feature, WorkItemKind.UserStory) => true,
            (WorkItemKind.UserStory, WorkItemKind.Task) => true,
            (WorkItemKind.Task, WorkItemKind.Subtask) => true,
            _ => false
        };

    /// <summary>
    /// Tipo efetivo de um item pedido como <paramref name="requested"/> sob um pai do tipo <paramref name="parent"/>.
    /// Tarefa só vira Subtarefa quando o pai também é Tarefa (regra única de criação e edição, T-0019).
    /// </summary>
    public static WorkItemKind EffectiveKind(WorkItemKind requested, WorkItemKind? parent)
        => requested == WorkItemKind.Task && parent == WorkItemKind.Task ? WorkItemKind.Subtask : requested;
}
