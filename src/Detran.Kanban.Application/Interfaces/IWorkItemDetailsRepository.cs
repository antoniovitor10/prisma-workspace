using Detran.Kanban.Domain.Entities;

namespace Detran.Kanban.Application.Interfaces;

/// <summary>
/// Repositório dos detalhes da tarefa: taxonomia (tags carregadas) e checklist.
/// </summary>
public interface IWorkItemDetailsRepository
{
    Task<WorkItem?> GetWithTagsAsync(Guid workItemId, CancellationToken cancellationToken = default);
    Task<WorkItem?> GetAsync(Guid workItemId, CancellationToken cancellationToken = default);
    Task<bool> WorkItemExistsAsync(Guid workItemId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChecklistItem>> GetChecklistAsync(Guid workItemId, CancellationToken cancellationToken = default);
    Task<ChecklistItem?> GetChecklistItemAsync(Guid workItemId, Guid itemId, CancellationToken cancellationToken = default);
    Task<double> GetMaxChecklistPositionAsync(Guid workItemId, CancellationToken cancellationToken = default);
    Task AddChecklistItemAsync(ChecklistItem item, CancellationToken cancellationToken = default);
    Task DeleteChecklistItemAsync(ChecklistItem item, CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);
}
