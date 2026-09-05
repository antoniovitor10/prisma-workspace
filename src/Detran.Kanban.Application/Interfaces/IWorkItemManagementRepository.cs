using Detran.Kanban.Domain.Entities;

namespace Detran.Kanban.Application.Interfaces;

public interface IWorkItemManagementRepository
{
    Task<WorkItem?> GetDetailedAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WorkItem?> GetEditableAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WorkItemLink?> GetLinkAsync(Guid workItemId, Guid linkId, CancellationToken cancellationToken = default);
    Task AddLinkAsync(WorkItemLink link, CancellationToken cancellationToken = default);
    Task<bool> TryAddLinkAcyclicAsync(WorkItemLink link, CancellationToken cancellationToken = default);
    Task RemoveLinkAsync(WorkItemLink link, CancellationToken cancellationToken = default);
    Task AddFollowerAsync(Guid workItemId, string userId, CancellationToken cancellationToken = default);
    Task RemoveFollowerAsync(Guid workItemId, string userId, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
    Task SaveWithEventAsync(TaskEvent taskEvent, CancellationToken cancellationToken = default);
}

public sealed record WorkItemSearchEntry(
    Guid Id,
    Guid ProjectId,
    string ProjectKey,
    long Number,
    string Title,
    string Status);

public interface IWorkItemSearchRepository
{
    Task<IReadOnlyList<WorkItemSearchEntry>> SearchVisibleAsync(
        string query,
        string userId,
        int limit,
        CancellationToken cancellationToken = default);
}
