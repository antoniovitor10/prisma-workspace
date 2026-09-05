using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Application.Interfaces;

public interface IMyWorkRepository
{
    Task<IReadOnlyList<WorkItem>> GetTasksAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<Comment>> GetRecentCommentsAsync(
        string userId, string? userEmail, int limit, CancellationToken ct = default);
}
