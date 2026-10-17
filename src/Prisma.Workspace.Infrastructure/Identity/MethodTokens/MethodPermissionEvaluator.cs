using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodSecurity;
using Prisma.Workspace.Domain.Authorization;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;

namespace Prisma.Workspace.Infrastructure.Identity.MethodTokens;

public sealed class MethodPermissionEvaluator(AppDbContext db, TimeProvider clock) : IMethodPermissionEvaluator
{
    public async Task<bool> HasAsync(MethodActorIdentity actor, PlatformPermission permission, PermissionScope scope,
        Guid? scopeId, CancellationToken cancellationToken = default)
    {
        if (actor.PrincipalId is null || actor.OrganizationId is null || actor.ProjectId is null
            || actor.AuthenticationKind == MethodAuthenticationKind.MethodToken && (actor.ExpiresAtUtc is null || actor.ExpiresAtUtc <= clock.GetUtcNow())
            || actor.AuthenticationKind == MethodAuthenticationKind.IntegrationWorker && actor.Role != MethodRole.System
            || !actor.PermissionCeiling.Contains(permission)) return false;
        var principal = await db.Set<MethodPrincipal>().IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == actor.PrincipalId && x.IsActive
                && x.ProjectId == actor.ProjectId && x.OrganizationId == actor.OrganizationId, cancellationToken);
        if (principal is null || principal.Role != actor.Role || !Ceiling(principal).Contains(permission)) return false;
        var expectedActor = principal.Role == MethodRole.Human ? principal.HumanUserId : $"method:{principal.Id:D}";
        if (actor.ActorId != expectedActor || !RoleAllows(principal.Role, permission, scope)) return false;
        if (!await ScopeMatchesAsync(principal.ProjectId, scope, scopeId, cancellationToken)) return false;
        if (!await HasUserPermissionAsync(principal.OrganizationId, principal.ProjectId, principal.DelegatedByUserId,
                permission, scope, scopeId, cancellationToken)) return false;
        if (actor.AuthenticationKind == MethodAuthenticationKind.MethodToken)
        {
            // Rotação por outro emissor nunca empresta a delegação antiga para ampliar seus próprios direitos.
            var token = await db.Set<MethodApiToken>().IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == actor.TokenId && x.PrincipalId == principal.Id && x.ProjectId == principal.ProjectId
                && x.RevokedAtUtc == null, cancellationToken);
            if (token is null || !await HasUserPermissionAsync(principal.OrganizationId, principal.ProjectId,
                    token.IssuedByUserId, permission, scope, scopeId, cancellationToken)) return false;
        }
        return actor.AuthenticationKind is MethodAuthenticationKind.MethodToken or MethodAuthenticationKind.IntegrationWorker;
    }

    internal static HashSet<PlatformPermission> Ceiling(MethodPrincipal principal)
    {
        try
        {
            var values = JsonSerializer.Deserialize<PlatformPermission[]>(principal.PermissionCeilingJson) ?? [];
            return values.All(x => Enum.IsDefined(x)) ? values.ToHashSet() : [];
        }
        catch (JsonException) { return []; }
    }

    internal static bool RoleAllows(MethodRole role, PlatformPermission permission, PermissionScope scope)
        => role == MethodRole.Human || role == MethodRole.Agent
            && RolePermissionCatalog.Allows(OrganizationRole.TeamMember, permission, scope)
            || role == MethodRole.System && permission is PlatformPermission.View or PlatformPermission.Edit or PlatformPermission.ChangeStatus;

    internal async Task<bool> HasUserPermissionAsync(Guid organizationId, Guid projectId, string userId,
        PlatformPermission permission, PermissionScope scope, Guid? scopeId, CancellationToken ct)
    {
        var member = await db.OrganizationMembers.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x =>
            x.OrganizationId == organizationId && x.UserId == userId && x.IsActive && x.Organization.IsActive, ct);
        if (member is null || !await db.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.EmailConfirmed, ct)) return false;
        var project = await db.Projects.IgnoreQueryFilters().AsNoTracking().Where(x => x.Id == projectId
            && x.OrganizationId == organizationId && !x.IsArchived && x.Status == ProjectStatus.Active)
            .Select(x => new
            {
                x.OwnerId,
                Role = x.Members.Where(m => m.UserId == userId).Select(m => (ProjectRole?)m.Role).FirstOrDefault(),
                TeamMember = x.Teams.Any(t => t.Team.IsActive && t.Team.Members.Any(m => m.UserId == userId))
            }).SingleOrDefaultAsync(ct);
        if (project is null) return false;
        var admin = member.Role is OrganizationRole.Administrator or OrganizationRole.Manager or OrganizationRole.ProjectManager
            || project.OwnerId == userId
            || await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
                      where ur.UserId == userId && (r.Name == "GlobalAdmin" || r.Name == "Administrator")
                      select ur.UserId).AnyAsync(ct);
        var role = admin ? ProjectRole.ProjectAdmin : project.Role ?? (project.TeamMember ? ProjectRole.Member : (ProjectRole?)null);
        if (role is null || role == ProjectRole.Viewer && permission is not (PlatformPermission.View or PlatformPermission.Comment or PlatformPermission.ViewReport))
            return false;
        // Leitura de regras de delegação, nunca chamada a PermissionService como se o emissor fosse o ator.
        var grants = await db.PermissionGrants.IgnoreQueryFilters().AsNoTracking().Where(x => x.OrganizationId == organizationId
            && x.UserId == userId && (x.Permission == permission || x.Permission == PlatformPermission.View)
            && (x.Scope == PermissionScope.Organization && x.ScopeId == null
                || x.Scope == PermissionScope.Project && x.ScopeId == projectId
                || x.Scope == scope && x.ScopeId == scopeId))
            .Select(x => new { x.Permission, x.IsAllowed }).ToListAsync(ct);
        if (grants.Any(x => !x.IsAllowed && (x.Permission == permission || x.Permission == PlatformPermission.View))) return false;
        if (grants.Any(x => x.Permission == permission && x.IsAllowed)) return true;
        return RolePermissionCatalog.Allows(member.Role, permission, scope);
    }

    private async Task<bool> ScopeMatchesAsync(Guid projectId, PermissionScope scope, Guid? scopeId, CancellationToken ct)
    {
        if (scopeId is null) return false;
        return scope switch
        {
            PermissionScope.Project => scopeId == projectId,
            PermissionScope.WorkItem => await db.WorkItems.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(x => x.Id == scopeId && x.Board.ProjectId == projectId, ct),
            PermissionScope.Report => await db.SavedReports.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(x => x.Id == scopeId && x.ProjectId == projectId, ct),
            // Catálogos de organização/equipe não são concedidos por um token de projeto.
            _ => false
        };
    }
}
