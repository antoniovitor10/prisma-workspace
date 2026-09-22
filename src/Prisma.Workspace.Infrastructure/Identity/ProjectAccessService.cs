using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Identity;

public class ProjectAccessService : IProjectAccessService
{
    private readonly AppDbContext _context;
    private readonly IPermissionService _permissions;
    public ProjectAccessService(AppDbContext context, IPermissionService permissions)
        => (_context, _permissions) = (context, permissions);

    public async Task<ProjectRole?> GetRoleAsync(Guid projectId, string userId, CancellationToken cancellationToken = default)
    {
        var globalAdmin = await (from userRole in _context.UserRoles
                                 join role in _context.Roles on userRole.RoleId equals role.Id
                                 where userRole.UserId == userId
                                     && (role.Name == "GlobalAdmin" || role.Name == "Administrator")
                                 select userRole.UserId).AnyAsync(cancellationToken);
        if (globalAdmin)
            return ProjectRole.ProjectAdmin;

        var project = await _context.Projects.AsNoTracking()
            .Where(x => x.Id == projectId)
            .Select(x => new { x.OrganizationId })
            .FirstOrDefaultAsync(cancellationToken);
        if (project is null) return null;
        var activeOrganizationMember = await _context.OrganizationMembers.AsNoTracking()
            .Where(x => x.OrganizationId == project.OrganizationId && x.UserId == userId && x.IsActive)
            .Select(x => (OrganizationRole?)x.Role)
            .FirstOrDefaultAsync(cancellationToken);
        if (activeOrganizationMember is null) return null;
        if (!await _permissions.HasAsync(userId, PlatformPermission.View,
            PermissionScope.Project, projectId, cancellationToken))
            return null;

        if (activeOrganizationMember is OrganizationRole.Administrator
            or OrganizationRole.Manager
            or OrganizationRole.ProjectManager)
            return ProjectRole.ProjectAdmin;

        var projectMembership = await _context.Projects.AsNoTracking()
            .Where(x => x.Id == projectId)
            .Select(x => new
            {
                x.OwnerId,
                Role = x.Members.Where(m => m.UserId == userId).Select(m => (ProjectRole?)m.Role).FirstOrDefault(),
                HasTeamAccess = x.Teams.Any(link => link.Team.IsActive
                    && link.Team.Members.Any(member => member.UserId == userId))
            }).FirstOrDefaultAsync(cancellationToken);
        if (projectMembership is null) return null;
        if (projectMembership.OwnerId == userId) return ProjectRole.ProjectAdmin;
        if (projectMembership.Role is not null) return projectMembership.Role;
        return projectMembership.HasTeamAccess ? ProjectRole.Member : null;
    }

    public async Task EnsureAtLeastAsync(Guid projectId, string userId, ProjectRole minimumRole, CancellationToken cancellationToken = default)
    {
        var role = await GetRoleAsync(projectId, userId, cancellationToken);
        if (role is null || role.Value < minimumRole) throw new NaoEncontradoException("Projeto");
    }

    public async Task<IReadOnlySet<Guid>> GetAccessibleProjectIdsAsync(
        IEnumerable<Guid> projectIds,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var ids = projectIds.Where(x => x != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0) return new HashSet<Guid>();

        var accessible = new HashSet<Guid>();
        foreach (var projectId in ids)
        {
            if (await GetRoleAsync(projectId, userId, cancellationToken) is not null)
                accessible.Add(projectId);
        }
        return accessible;
    }
}
