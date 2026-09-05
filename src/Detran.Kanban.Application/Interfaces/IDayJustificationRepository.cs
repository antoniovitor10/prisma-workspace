using Detran.Kanban.Domain.Entities;

namespace Detran.Kanban.Application.Interfaces;

/// <summary>
/// Repositório de justificativas de dia do timesheet.
/// </summary>
public interface IDayJustificationRepository
{
    Task<IReadOnlyList<DayJustification>> GetByUserAndDateAsync(string userId, DateOnly date, CancellationToken cancellationToken = default);
    Task<DayJustification?> GetByIdForUserAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task AddAsync(DayJustification justification, CancellationToken cancellationToken = default);
    Task DeleteAsync(DayJustification justification, CancellationToken cancellationToken = default);
}
