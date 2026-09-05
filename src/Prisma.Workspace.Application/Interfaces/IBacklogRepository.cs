using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Application.Interfaces;

public interface IBacklogRepository
{
    Task<IReadOnlyList<WorkItem>> GetProjectBacklogAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkItem>> GetTrackedByIdsAsync(Guid projectId, IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
