using Detran.Kanban.Domain.Entities;

namespace Detran.Kanban.Application.Interfaces;

/// <summary>
/// Repositorio de lancamentos de tempo.
/// </summary>
public interface ITimeEntryRepository
{
    Task<TimeEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TimeEntry?> GetRunningByUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimeEntry>> GetByWorkItemIdAsync(Guid workItemId, CancellationToken cancellationToken = default);
    Task<TimeEntry> AddAsync(TimeEntry timeEntry, CancellationToken cancellationToken = default);
    Task UpdateAsync(TimeEntry timeEntry, CancellationToken cancellationToken = default);
    Task<int> GetTotalSecondsByWorkItemIdAsync(Guid workItemId, CancellationToken cancellationToken = default);
    Task<int> GetTotalSecondsByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<int> GetTotalSecondsByBoardIdAsync(Guid boardId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimeEntry>> GetByUserInRangeAsync(string userId, DateTimeOffset fromInclusive, DateTimeOffset toExclusive, CancellationToken cancellationToken = default);

    /// <summary>Mesma consulta acima, com o WorkItem carregado (para exibir títulos).</summary>
    Task<IReadOnlyList<TimeEntry>> GetByUserInRangeWithWorkItemAsync(string userId, DateTimeOffset fromInclusive, DateTimeOffset toExclusive, CancellationToken cancellationToken = default);

    /// <summary>Lançamentos de vários usuários no intervalo (visão de equipes).</summary>
    Task<IReadOnlyList<TimeEntry>> GetByUsersInRangeAsync(IReadOnlyCollection<string> userIds, DateTimeOffset fromInclusive, DateTimeOffset toExclusive, CancellationToken cancellationToken = default);
}
