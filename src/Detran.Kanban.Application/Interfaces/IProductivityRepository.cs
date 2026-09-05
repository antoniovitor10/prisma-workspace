using Detran.Kanban.Domain.Entities;

namespace Detran.Kanban.Application.Interfaces;

public interface IProductivityRepository
{
    Task<Board?> GetBoardAsync(Guid boardId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SavedFilter>> GetFiltersAsync(Guid boardId, string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutomationRule>> GetRulesAsync(Guid boardId, CancellationToken cancellationToken = default);
    Task<AutomationRule?> GetRuleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Stage?> GetStageAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Sprint?> GetSprintAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> TagExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkItem>> GetItemsAsync(Guid boardId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    Task AddFilterAsync(SavedFilter filter, CancellationToken cancellationToken = default);
    Task DeleteFilterAsync(Guid boardId, Guid id, string userId, CancellationToken cancellationToken = default);
    Task AddRuleAsync(AutomationRule rule, CancellationToken cancellationToken = default);
    Task DeleteRuleAsync(AutomationRule rule, CancellationToken cancellationToken = default);
    void AddTaskEvents(IEnumerable<TaskEvent> events);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
