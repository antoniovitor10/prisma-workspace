using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Identity;

public sealed class BoardAccessService : IBoardAccessService
{
    private readonly AppDbContext _context;
    private readonly IProjectAccessService _projectAccess;
    private readonly IPermissionService _permissions;

    public BoardAccessService(
        AppDbContext context,
        IProjectAccessService projectAccess,
        IPermissionService permissions)
        => (_context, _projectAccess, _permissions) = (context, projectAccess, permissions);

    public async Task<Board> EnsureAsync(
        Guid boardId,
        string userId,
        PlatformPermission permission,
        ProjectRole minimumRole = ProjectRole.Viewer,
        CancellationToken cancellationToken = default)
    {
        var board = await _context.Boards.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == boardId, cancellationToken)
            ?? throw new NaoEncontradoException("Quadro");
        await _projectAccess.EnsureAtLeastAsync(
            board.ProjectId, userId, minimumRole, cancellationToken);
        await _permissions.EnsureAsync(
            userId, permission, PermissionScope.Project, board.ProjectId, cancellationToken);
        return board;
    }
}
