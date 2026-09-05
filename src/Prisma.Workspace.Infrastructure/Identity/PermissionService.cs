using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Authorization;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Identity;

public class PermissionService : IPermissionService
{
    private readonly AppDbContext _context;
    private readonly IOrganizationContext _organizationContext;

    public PermissionService(AppDbContext context, IOrganizationContext organizationContext)
        => (_context, _organizationContext) = (context, organizationContext);

    public async Task<bool> HasAsync(
        string userId,
        PlatformPermission permission,
        PermissionScope scope = PermissionScope.Organization,
        Guid? scopeId = null,
        CancellationToken cancellationToken = default)
    {
        var organizationId = _organizationContext.OrganizationId;
        if (organizationId is null)
            return false;

        var member = await _context.OrganizationMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId
                && x.UserId == userId && x.IsActive, cancellationToken);
        if (member is null)
            return false;

        var grants = await _context.PermissionGrants
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId
                && x.UserId == userId
                && x.Permission == permission
                && ((x.Scope == PermissionScope.Organization && x.ScopeId == null)
                    || (x.Scope == scope && x.ScopeId == scopeId)))
            .Select(x => x.IsAllowed)
            .ToListAsync(cancellationToken);

        if (grants.Any(x => !x))
            return false;
        if (grants.Any(x => x))
            return true;
        return RolePermissionCatalog.Allows(member.Role, permission, scope);
    }

    public async Task EnsureAsync(
        string userId,
        PlatformPermission permission,
        PermissionScope scope = PermissionScope.Organization,
        Guid? scopeId = null,
        CancellationToken cancellationToken = default)
    {
        if (!await HasAsync(userId, permission, scope, scopeId, cancellationToken))
            throw new AcessoNegadoException();
    }
}
