using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;

namespace Detran.Kanban.Application.Interfaces;

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
