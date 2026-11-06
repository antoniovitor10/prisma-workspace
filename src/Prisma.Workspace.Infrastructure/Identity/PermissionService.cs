using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Authorization;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.MethodSecurity;

namespace Prisma.Workspace.Infrastructure.Identity;

public class PermissionService : IPermissionService
{
    private readonly AppDbContext _context;
    private readonly IOrganizationContext _organizationContext;
    private readonly IMethodAccessContext? _method;
    private readonly IMethodPermissionEvaluator? _methodPermissions;

    public PermissionService(AppDbContext context, IOrganizationContext organizationContext,
        IMethodAccessContext? method = null, IMethodPermissionEvaluator? methodPermissions = null)
        => (_context, _organizationContext, _method, _methodPermissions) = (context, organizationContext, method, methodPermissions);

    public async Task<bool> HasAsync(
        string userId,
        PlatformPermission permission,
        PermissionScope scope = PermissionScope.Organization,
        Guid? scopeId = null,
        CancellationToken cancellationToken = default)
    {
        // Também cobre token humano cujo ActorId é um usuário Identity real: nunca emprestar seu papel global.
        if (_method?.Current is { AuthenticationKind: MethodAuthenticationKind.MethodToken or MethodAuthenticationKind.IntegrationWorker } actor)
            return userId == actor.ActorId && _methodPermissions is not null
                && await _methodPermissions.HasAsync(actor, permission, scope, scopeId, cancellationToken);
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

    public async Task<IReadOnlySet<Guid>> GetAllowedProjectIdsAsync(
        string userId,
        PlatformPermission permission,
        IEnumerable<Guid> projectIds,
        CancellationToken cancellationToken = default)
    {
        var ids = projectIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        var allowed = new HashSet<Guid>();
        if (ids.Length == 0 || _organizationContext.OrganizationId is not { } organizationId)
            return allowed;
        if (_method?.Current is { AuthenticationKind: MethodAuthenticationKind.MethodToken or MethodAuthenticationKind.IntegrationWorker } actor)
        {
            // O token possui exatamente um projeto; nunca consultar permissões humanas de outros projetos.
            if (actor.ProjectId is { } projectId && ids.Contains(projectId) && await HasAsync(userId, permission,
                    PermissionScope.Project, projectId, cancellationToken))
                allowed.Add(projectId);
            return allowed;
        }
        var role = await _context.OrganizationMembers.AsNoTracking()
            .Where(member => member.OrganizationId == organizationId && member.UserId == userId && member.IsActive)
            .Select(member => (OrganizationRole?)member.Role).FirstOrDefaultAsync(cancellationToken);
        if (role is null) return allowed;
        var grants = await _context.PermissionGrants.AsNoTracking()
            .Where(grant => grant.OrganizationId == organizationId && grant.UserId == userId
                && grant.Permission == permission
                && ((grant.Scope == PermissionScope.Organization && grant.ScopeId == null)
                    || (grant.Scope == PermissionScope.Project && grant.ScopeId.HasValue && ids.Contains(grant.ScopeId.Value))))
            .Select(grant => new { grant.ScopeId, grant.IsAllowed }).ToListAsync(cancellationToken);
        var defaultAllowed = RolePermissionCatalog.Allows(role.Value, permission, PermissionScope.Project);
        foreach (var id in ids)
        {
            var applicable = grants.Where(grant => grant.ScopeId == null || grant.ScopeId == id).ToList();
            if (!applicable.Any(grant => !grant.IsAllowed)
                && (applicable.Any(grant => grant.IsAllowed) || defaultAllowed))
                allowed.Add(id);
        }
        return allowed;
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
