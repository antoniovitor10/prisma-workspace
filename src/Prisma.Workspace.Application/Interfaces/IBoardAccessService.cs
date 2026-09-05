using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Interfaces;

/// <summary>Autoriza recursos do quadro usando o projeto ao qual ele pertence.</summary>
public interface IBoardAccessService
{
    Task<Board> EnsureAsync(
        Guid boardId,
        string userId,
        PlatformPermission permission,
        ProjectRole minimumRole = ProjectRole.Viewer,
        CancellationToken cancellationToken = default);
}
