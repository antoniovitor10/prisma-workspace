using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Interfaces;

public interface IPermissionService
{
    Task<bool> HasAsync(
        string userId,
        PlatformPermission permission,
        PermissionScope scope = PermissionScope.Organization,
        Guid? scopeId = null,
        CancellationToken cancellationToken = default);

    async Task<IReadOnlySet<Guid>> GetAllowedProjectIdsAsync(
        string userId,
        PlatformPermission permission,
        IEnumerable<Guid> projectIds,
        CancellationToken cancellationToken = default)
    {
        var allowed = new HashSet<Guid>();
        foreach (var projectId in projectIds.Distinct())
            if (await HasAsync(userId, permission, PermissionScope.Project, projectId, cancellationToken))
                allowed.Add(projectId);
        return allowed;
    }

    Task EnsureAsync(
        string userId,
        PlatformPermission permission,
        PermissionScope scope = PermissionScope.Organization,
        Guid? scopeId = null,
        CancellationToken cancellationToken = default);
}
