using Detran.Kanban.Domain.Entities;

namespace Detran.Kanban.Application.Interfaces;

public interface ISprintRepository
{
    Task<IReadOnlyList<Sprint>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Sprint?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> HasActiveAsync(Guid projectId, Guid? excludingId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Sprint sprint, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
