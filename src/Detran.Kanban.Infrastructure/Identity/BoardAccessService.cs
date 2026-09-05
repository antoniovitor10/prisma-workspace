using Detran.Kanban.Application.Common.Exceptions;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Detran.Kanban.Infrastructure.Identity;

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
        if (!board.ProjectId.HasValue) throw new NaoEncontradoException("Projeto");
        await _projectAccess.EnsureAtLeastAsync(
            board.ProjectId.Value, userId, minimumRole, cancellationToken);
        await _permissions.EnsureAsync(
            userId, permission, PermissionScope.Project, board.ProjectId.Value, cancellationToken);
        return board;
    }
}
