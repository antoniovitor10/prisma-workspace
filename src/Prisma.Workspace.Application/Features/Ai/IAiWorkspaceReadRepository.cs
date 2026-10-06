using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Features.Ai;

public sealed record AiWorkItemScope(Guid BoardId, Guid ProjectId);
public sealed record AiWorkItemCandidate(Guid Id, long Number, string Title, Guid ProjectId,
    Guid? StageId, string Stage, DateOnly? DueDate, DateTimeOffset? CompletedAt,
    string? ResponsibleId, Priority Priority, DateTimeOffset UpdatedAt);
public sealed record AiWorkItemFilters(Guid? ProjectId, Guid? StageId, Guid? BoardId,
    string? ResponsibleId, int? Priority, bool Overdue, DateOnly? DueFrom, DateOnly? DueTo,
    DateTimeOffset? UpdatedSince, string? MyUserId);
public sealed record AiSourceCandidate(Guid Id, string Title, Guid ProjectId, long Number = 0);

// Consultas tenant-scoped; a aplicação autoriza cada candidato antes de enviá-lo ao modelo.
public interface IAiWorkspaceReadRepository
{
    Task<bool> ProjectAvailableAsync(Guid id, CancellationToken ct);
    Task<Guid?> BoardProjectAsync(Guid id, CancellationToken ct);
    Task<AiWorkItemScope?> WorkItemScopeAsync(Guid id, CancellationToken ct);
    Task<WorkItem?> WorkItemDetailAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<AiSourceCandidate>> ChildrenAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<AiWorkItemCandidate>> CandidatesAsync(AiWorkItemFilters filters, CancellationToken ct);
    Task<object?> ActiveSprintAsync(Guid projectId, DateOnly today, CancellationToken ct);
    Task<object> RecentActivityAsync(Guid[] allowedIds, DateTimeOffset since, CancellationToken ct);
    Task<AiSourceCandidate?> TaskSourceAsync(long number, CancellationToken ct);
    Task<AiSourceCandidate?> ProjectSourceAsync(string key, CancellationToken ct);
}
