using Detran.Kanban.Domain.Enums;

namespace Detran.Kanban.Application.Interfaces;

public interface IProjectAccessService
{
    Task<ProjectRole?> GetRoleAsync(Guid projectId, string userId, CancellationToken cancellationToken = default);
    async Task<IReadOnlySet<Guid>> GetAccessibleProjectIdsAsync(
        IEnumerable<Guid> projectIds,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var accessible = new HashSet<Guid>();
        foreach (var projectId in projectIds.Distinct())
            if (await GetRoleAsync(projectId, userId, cancellationToken) is not null)
                accessible.Add(projectId);
        return accessible;
    }
    Task EnsureAtLeastAsync(Guid projectId, string userId, ProjectRole minimumRole, CancellationToken cancellationToken = default);
}
