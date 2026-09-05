using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Detran.Kanban.Infrastructure.Repositories;

public class OrganizationRepository : IOrganizationRepository
{
    private readonly AppDbContext _context;
    public OrganizationRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<Organization>> GetForUserAsync(
        string userId, CancellationToken cancellationToken = default)
        => await _context.Organizations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.IsActive && x.Members.Any(m => m.UserId == userId && m.IsActive))
            .Include(x => x.Members.Where(m => m.UserId == userId && m.IsActive))
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.Organizations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default)
        => _context.Organizations.IgnoreQueryFilters().AnyAsync(x => x.Slug == slug, cancellationToken);

    public async Task AddAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        await _context.Organizations.AddAsync(organization, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrganizationMember>> GetMembersAsync(
        Guid organizationId, CancellationToken cancellationToken = default)
        => await _context.OrganizationMembers
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Role)
            .ToListAsync(cancellationToken);

    public Task<OrganizationMember?> GetMemberAsync(
        Guid organizationId, string userId, CancellationToken cancellationToken = default)
        => _context.OrganizationMembers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.UserId == userId, cancellationToken);

    public Task<int> CountActiveAdministratorsAsync(
        Guid organizationId, CancellationToken cancellationToken = default)
        => _context.OrganizationMembers.CountAsync(x => x.OrganizationId == organizationId
            && x.IsActive && x.Role == OrganizationRole.Administrator, cancellationToken);

    public async Task AddInvitationAsync(
        OrganizationInvitation invitation, CancellationToken cancellationToken = default)
    {
        await _context.OrganizationInvitations.AddAsync(invitation, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<OrganizationInvitation?> GetInvitationByTokenHashAsync(
        string tokenHash, CancellationToken cancellationToken = default)
        => _context.OrganizationInvitations
            .IgnoreQueryFilters()
            .Include(x => x.Organization)
            .ThenInclude(x => x.Members)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<PermissionGrant>> GetGrantsAsync(
        Guid organizationId, CancellationToken cancellationToken = default)
        => await _context.PermissionGrants
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.UserId).ThenBy(x => x.Scope).ThenBy(x => x.Permission)
            .ToListAsync(cancellationToken);

    public Task<PermissionGrant?> GetGrantAsync(
        Guid organizationId, string userId, PermissionScope scope, Guid? scopeId,
        PlatformPermission permission, CancellationToken cancellationToken = default)
        => _context.PermissionGrants.FirstOrDefaultAsync(x => x.OrganizationId == organizationId
            && x.UserId == userId && x.Scope == scope && x.ScopeId == scopeId
            && x.Permission == permission, cancellationToken);

    public async Task AddGrantAsync(PermissionGrant grant, CancellationToken cancellationToken = default)
    {
        await _context.PermissionGrants.AddAsync(grant, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveGrantAsync(PermissionGrant grant, CancellationToken cancellationToken = default)
    {
        _context.PermissionGrants.Remove(grant);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
