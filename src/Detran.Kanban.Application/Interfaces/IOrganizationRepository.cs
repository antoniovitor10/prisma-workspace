using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;

namespace Detran.Kanban.Application.Interfaces;

public interface IOrganizationRepository
{
    Task<IReadOnlyList<Organization>> GetForUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default);
    Task AddAsync(Organization organization, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrganizationMember>> GetMembersAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<OrganizationMember?> GetMemberAsync(Guid organizationId, string userId, CancellationToken cancellationToken = default);
    Task<int> CountActiveAdministratorsAsync(Guid organizationId, CancellationToken cancellationToken = default);

    Task AddInvitationAsync(OrganizationInvitation invitation, CancellationToken cancellationToken = default);
    Task<OrganizationInvitation?> GetInvitationByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermissionGrant>> GetGrantsAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<PermissionGrant?> GetGrantAsync(
        Guid organizationId, string userId, PermissionScope scope, Guid? scopeId,
        PlatformPermission permission, CancellationToken cancellationToken = default);
    Task AddGrantAsync(PermissionGrant grant, CancellationToken cancellationToken = default);
    Task RemoveGrantAsync(PermissionGrant grant, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
