using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Identity;

public sealed class WorkItemAccessService : IWorkItemAccessService
{
    private readonly AppDbContext _context;
    private readonly IProjectAccessService _projectAccess;
    private readonly IPermissionService _permissions;

    public WorkItemAccessService(
        AppDbContext context,
        IProjectAccessService projectAccess,
        IPermissionService permissions)
        => (_context, _projectAccess, _permissions) = (context, projectAccess, permissions);

    public async Task EnsureAsync(
        Guid workItemId,
        string userId,
        PlatformPermission permission,
        ProjectRole minimumRole = ProjectRole.Viewer,
        CancellationToken cancellationToken = default)
    {
        var row = await _context.WorkItems.AsNoTracking()
            .Where(x => x.Id == workItemId)
            .Select(x => new { x.Board.ProjectId })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null) throw new NaoEncontradoException("Tarefa");

        await _projectAccess.EnsureAtLeastAsync(row.ProjectId, userId, minimumRole, cancellationToken);
        await _permissions.EnsureAsync(
            userId, permission, PermissionScope.WorkItem, workItemId, cancellationToken);
    }
}
