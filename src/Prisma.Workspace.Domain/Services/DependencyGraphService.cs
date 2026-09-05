using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;

namespace Prisma.Workspace.Domain.Services;

public readonly record struct DependencyEdge(Guid From, Guid To);

public static class DependencyGraphService
{
    public static bool TryCanonicalize(
        Guid sourceWorkItemId,
        Guid targetWorkItemId,
        WorkItemLinkType type,
        out DependencyEdge edge)
    {
        edge = type switch
        {
            WorkItemLinkType.DependsOn => new(sourceWorkItemId, targetWorkItemId),
            WorkItemLinkType.Blocks => new(targetWorkItemId, sourceWorkItemId),
            _ => default
        };
        return type is WorkItemLinkType.DependsOn or WorkItemLinkType.Blocks;
    }

    public static bool WouldCreateCycle(
        IEnumerable<DependencyEdge> existingEdges,
        DependencyEdge proposedEdge,
        int maximumVisitedNodes = 100_000)
    {
        if (proposedEdge.From == proposedEdge.To) return true;

        var adjacency = existingEdges
            .Append(proposedEdge)
            .GroupBy(edge => edge.From)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.To).Distinct().ToArray());
        var visited = new HashSet<Guid>();
        var pending = new Stack<Guid>();
        pending.Push(proposedEdge.To);

        while (pending.TryPop(out var current))
        {
            if (current == proposedEdge.From) return true;
            if (!visited.Add(current)) continue;
            if (visited.Count > maximumVisitedNodes)
                throw new DomainException("O limite defensivo da inspeção de dependências foi excedido.");
            if (!adjacency.TryGetValue(current, out var targets)) continue;
            foreach (var target in targets) pending.Push(target);
        }

        return false;
    }
}
