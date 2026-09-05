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

    Task EnsureAsync(
        string userId,
        PlatformPermission permission,
        PermissionScope scope = PermissionScope.Organization,
        Guid? scopeId = null,
        CancellationToken cancellationToken = default);
}
