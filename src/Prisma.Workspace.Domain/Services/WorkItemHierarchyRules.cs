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
}
