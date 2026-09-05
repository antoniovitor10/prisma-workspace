using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Interfaces;

/// <summary>Autoriza acesso a uma tarefa combinando vínculo no projeto e permissão funcional.</summary>
public interface IWorkItemAccessService
{
    Task EnsureAsync(
        Guid workItemId,
        string userId,
        PlatformPermission permission,
        ProjectRole minimumRole = ProjectRole.Viewer,
        CancellationToken cancellationToken = default);
}
