using Detran.Kanban.Application.Common.Exceptions;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Detran.Kanban.Infrastructure.Identity;

public class ProjectAccessService : IProjectAccessService
{
    private readonly AppDbContext _context;
    public ProjectAccessService(AppDbContext context) => _context = context;

    public async Task<ProjectRole?> GetRoleAsync(Guid projectId, string userId, CancellationToken cancellationToken = default)
    {
        var globalAdmin = await (from userRole in _context.UserRoles
                                 join role in _context.Roles on userRole.RoleId equals role.Id
                                 where userRole.UserId == userId
                                     && (role.Name == "GlobalAdmin" || role.Name == "Administrator")
                                 select userRole.UserId).AnyAsync(cancellationToken);
        if (globalAdmin)
            return ProjectRole.ProjectAdmin;

        var organizationRole = await _context.OrganizationMembers.AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive)
            .Select(x => (OrganizationRole?)x.Role)
            .FirstOrDefaultAsync(cancellationToken);
        if (organizationRole is OrganizationRole.Administrator
            or OrganizationRole.Manager
            or OrganizationRole.ProjectManager)
            return ProjectRole.ProjectAdmin;

        var project = await _context.Projects.AsNoTracking()
            .Where(x => x.Id == projectId)
            .Select(x => new
            {
                x.OwnerId,
                Role = x.Members.Where(m => m.UserId == userId).Select(m => (ProjectRole?)m.Role).FirstOrDefault()
            }).FirstOrDefaultAsync(cancellationToken);
        if (project is null) return null;
        return project.OwnerId == userId ? ProjectRole.ProjectAdmin : project.Role;
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

        var globalAdmin = await (from userRole in _context.UserRoles
                                 join role in _context.Roles on userRole.RoleId equals role.Id
                                 where userRole.UserId == userId
                                     && (role.Name == "GlobalAdmin" || role.Name == "Administrator")
                                 select userRole.UserId).AnyAsync(cancellationToken);
        var organizationRole = await _context.OrganizationMembers.AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive)
            .Select(x => (OrganizationRole?)x.Role)
            .FirstOrDefaultAsync(cancellationToken);
        var canSeeAll = globalAdmin || organizationRole is OrganizationRole.Administrator
            or OrganizationRole.Manager or OrganizationRole.ProjectManager;

        var query = _context.Projects.AsNoTracking().Where(x => ids.Contains(x.Id));
        if (!canSeeAll)
            query = query.Where(x => x.OwnerId == userId || x.Members.Any(m => m.UserId == userId));
        return (await query.Select(x => x.Id).ToListAsync(cancellationToken)).ToHashSet();
    }
}
