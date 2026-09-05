using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Detran.Kanban.Infrastructure.Identity;

/// <summary>
/// Consulta usuários do Identity e resolve o nome funcional específico do tenant.
/// </summary>
public class UserDirectory : IUserDirectory
{
    private readonly AppDbContext _context;
    private readonly IOrganizationContext _organizationContext;

    public UserDirectory(AppDbContext context, IOrganizationContext organizationContext)
    {
        _context = context;
        _organizationContext = organizationContext;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        var organizationId = _organizationContext.OrganizationId;
        if (organizationId is null)
        {
            var unscopedUsers = await _context.Users.AsNoTracking()
                .Where(u => ids.Contains(u.Id))
                .Select(u => new UserRow
                {
                    Id = u.Id,
                    Email = u.Email,
                    NormalizedEmail = u.NormalizedEmail,
                    UserName = u.UserName
                })
                .ToListAsync(cancellationToken);
            return unscopedUsers.ToDictionary(u => u.Id, ResolveDisplayName);
        }

        var users = await ActiveMembers(organizationId.Value)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        return users.ToDictionary(u => u.Id, ResolveDisplayName);
    }

    public async Task<IReadOnlyList<UserSummary>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var organizationId = _organizationContext.OrganizationId;
        if (organizationId is null)
            return Array.Empty<UserSummary>();

        var users = await ActiveMembers(organizationId.Value).ToListAsync(cancellationToken);

        return users.Select(ToSummary).OrderBy(u => u.DisplayName).ToList();
    }

    public async Task<IReadOnlyList<UserSummary>> GetByIdsAsync(
        IEnumerable<string> userIds,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var organizationId = _organizationContext.OrganizationId;
        if (organizationId is null) return Array.Empty<UserSummary>();

        var ids = userIds.Distinct().ToList();
        var users = await Members(organizationId.Value, includeInactive)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
        return users.Select(ToSummary).OrderBy(u => u.DisplayName).ToList();
    }

    public async Task<UserSummary?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var organizationId = _organizationContext.OrganizationId;
        if (organizationId is null) return null;
        var user = await ActiveMembers(organizationId.Value)
            .Where(x => x.Id == userId)
            .FirstOrDefaultAsync(cancellationToken);
        return user is null ? null : ToSummary(user);
    }

    public async Task<UserSummary?> GetByIdUnscopedAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserRow
            {
                Id = u.Id,
                Email = u.Email,
                NormalizedEmail = u.NormalizedEmail,
                UserName = u.UserName
            })
            .FirstOrDefaultAsync(cancellationToken);
        return user is null ? null : ToSummary(user);
    }

    public async Task<UserSummary?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var organizationId = _organizationContext.OrganizationId;
        if (organizationId is null) return null;
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await ActiveMembers(organizationId.Value)
            .Where(x => x.NormalizedEmail == normalizedEmail)
            .FirstOrDefaultAsync(cancellationToken);
        return user is null ? null : ToSummary(user);
    }

    private IQueryable<UserRow> ActiveMembers(Guid organizationId) => Members(organizationId, false);

    private IQueryable<UserRow> Members(Guid organizationId, bool includeInactive) =>
        from member in _context.OrganizationMembers.AsNoTracking()
        join user in _context.Users.AsNoTracking() on member.UserId equals user.Id
        where member.OrganizationId == organizationId && (includeInactive || member.IsActive)
        select new UserRow
        {
            Id = user.Id,
            Email = user.Email,
            NormalizedEmail = user.NormalizedEmail,
            UserName = user.UserName,
            DisplayName = member.DisplayName
        };

    private static string ResolveDisplayName(UserRow user) =>
        UserDisplayName.Resolve(user.Id, user.DisplayName, user.UserName, user.Email);

    private static UserSummary ToSummary(UserRow user) =>
        new(user.Id, user.Email, user.UserName, ResolveDisplayName(user));

    private sealed class UserRow
    {
        public required string Id { get; init; }
        public string? Email { get; init; }
        public string? NormalizedEmail { get; init; }
        public string? UserName { get; init; }
        public string? DisplayName { get; init; }
    }
}
