using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Application.Interfaces;

public interface ISlaRepository
{
    Task<ProjectSlaPolicy?> GetByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);
    void Add(ProjectSlaPolicy policy);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
