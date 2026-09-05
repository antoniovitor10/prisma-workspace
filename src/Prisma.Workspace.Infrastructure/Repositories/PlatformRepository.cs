using Prisma.Workspace.Application.Features.Notifications;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Repositories;

public sealed class NotificationRepository : INotificationRepository, IPlatformNotificationPublisher
{
    private readonly AppDbContext _db;
    private readonly IOrganizationContext _organization;
    public NotificationRepository(AppDbContext db, IOrganizationContext organization)
        => (_db, _organization) = (db, organization);

    public async Task<(IReadOnlyList<Notification> Items, int Total, int Unread)> GetAsync(
        string userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var baseQuery = _db.Notifications.AsNoTracking()
            .Where(x => x.UserId == userId && x.IsInAppVisible);
        var unread = await baseQuery.CountAsync(x => !x.IsRead, cancellationToken);
        var query = unreadOnly ? baseQuery.Where(x => !x.IsRead) : baseQuery;
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total, unread);
    }

    public Task<Notification?> GetAsync(Guid id, string userId, CancellationToken cancellationToken = default)
        => _db.Notifications.FirstOrDefaultAsync(
            x => x.Id == id && x.UserId == userId && x.IsInAppVisible, cancellationToken);

    public async Task<IReadOnlyList<NotificationPreference>> GetPreferencesAsync(
        string userId, CancellationToken cancellationToken = default)
        => await _db.NotificationPreferences.AsNoTracking()
            .Where(x => x.UserId == userId).OrderBy(x => x.Type).ToListAsync(cancellationToken);

    public Task<NotificationPreference?> GetPreferenceAsync(
        string userId, NotificationType type, CancellationToken cancellationToken = default)
        => _db.NotificationPreferences.FirstOrDefaultAsync(
            x => x.UserId == userId && x.Type == type, cancellationToken);

    public void AddPreference(NotificationPreference preference) => _db.NotificationPreferences.Add(preference);

    public async Task MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        await _db.Notifications.Where(x => x.UserId == userId && x.IsInAppVisible && !x.IsRead)
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.IsRead, true)
                .SetProperty(x => x.ReadAt, now), cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);

    public async Task<bool> PublishAsync(
        NotificationEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(envelope.RecipientId) || envelope.OrganizationId == Guid.Empty)
            return false;
        if (_organization.OrganizationId is not null && _organization.OrganizationId != envelope.OrganizationId)
            return false;

        var isActiveMember = await _db.OrganizationMembers.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(x => x.OrganizationId == envelope.OrganizationId
                && x.UserId == envelope.RecipientId && x.IsActive, cancellationToken);
        if (!isActiveMember) return false;

        if (!string.IsNullOrWhiteSpace(envelope.DeduplicationKey))
        {
            var exists = await _db.Notifications.IgnoreQueryFilters().AsNoTracking().AnyAsync(
                x => x.OrganizationId == envelope.OrganizationId
                    && x.UserId == envelope.RecipientId
                    && x.DeduplicationKey == envelope.DeduplicationKey,
                cancellationToken);
            if (exists) return false;
        }

        var preference = await _db.NotificationPreferences.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrganizationId == envelope.OrganizationId
                && x.UserId == envelope.RecipientId && x.Type == envelope.Type, cancellationToken);
        var defaults = NotificationCatalog.Default(envelope.Type);
        var inApp = preference?.InAppEnabled ?? defaults.InApp;
        var email = preference?.EmailEnabled ?? defaults.Email;
        if (!inApp && !email) return false;

        _db.Notifications.Add(Notification.Create(
            envelope.OrganizationId, envelope.RecipientId, envelope.Type,
            envelope.Title, envelope.Message, inApp, email, envelope.Link,
            envelope.WorkItemId, envelope.ExternalRequestId, envelope.ProjectId,
            envelope.SprintId, envelope.DeduplicationKey));
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> PublishManyAsync(
        IEnumerable<NotificationEnvelope> notifications,
        CancellationToken cancellationToken = default)
    {
        var count = 0;
        foreach (var notification in notifications
                     .GroupBy(x => new { x.OrganizationId, x.RecipientId, x.Type, x.DeduplicationKey,
                         x.WorkItemId, x.ExternalRequestId, x.SprintId })
                     .Select(x => x.First()))
            if (await PublishAsync(notification, cancellationToken)) count++;
        return count;
    }
}

public sealed class GlobalSearchRepository : IGlobalSearchRepository
{
    private readonly AppDbContext _db;
    private readonly IOrganizationContext _organization;
    public GlobalSearchRepository(AppDbContext db, IOrganizationContext organization)
        => (_db, _organization) = (db, organization);

    public async Task<IReadOnlyList<GlobalSearchHit>> SearchAsync(
        string query, int limitPerGroup, string userId, CancellationToken cancellationToken = default)
    {
        var organizationId = _organization.RequireOrganizationId();
        var member = await _db.OrganizationMembers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.UserId == userId && x.IsActive,
                cancellationToken);
        if (member is null) return Array.Empty<GlobalSearchHit>();

        var externalOnly = member.Role is OrganizationRole.ExternalRequester;
        var deniedProjects = await _db.PermissionGrants.AsNoTracking()
            .Where(x => x.UserId == userId && x.Permission == PlatformPermission.View
                && !x.IsAllowed && x.Scope == PermissionScope.Project && x.ScopeId.HasValue)
            .Select(x => x.ScopeId!.Value).ToListAsync(cancellationToken);

        var hits = new List<GlobalSearchHit>();
        if (!externalOnly)
        {
            var projects = await _db.Projects.AsNoTracking()
                .Where(x => !x.IsArchived && !deniedProjects.Contains(x.Id)
                    && (x.Name.Contains(query) || x.Key.Contains(query)))
                .OrderBy(x => x.Name).Take(limitPerGroup)
                .Select(x => new GlobalSearchHit("projects", x.Id.ToString(), x.Name,
                    x.Key, "/projects/" + x.Id + "/backlog", x.UpdatedAt))
                .ToListAsync(cancellationToken);
            hits.AddRange(projects);

            var tasks = await _db.WorkItems.AsNoTracking()
                .Where(x => !x.IsArchived && x.Board.ProjectId.HasValue
                    && !deniedProjects.Contains(x.Board.ProjectId.Value)
                    && (x.Title.Contains(query) || x.Number.ToString().Contains(query)))
                .OrderByDescending(x => x.UpdatedAt).Take(limitPerGroup)
                .Select(x => new GlobalSearchHit("tasks", x.Id.ToString(), x.Title,
                    "#" + x.Number + " · " + (x.Board.Project != null ? x.Board.Project.Key : x.Board.Name),
                    "/projects/" + x.Board.ProjectId + "/backlog?item=" + x.Id, x.UpdatedAt))
                .ToListAsync(cancellationToken);
            hits.AddRange(tasks);

            var users = await (from organizationMember in _db.OrganizationMembers.AsNoTracking()
                               join identityUser in _db.Users.AsNoTracking()
                                   on organizationMember.UserId equals identityUser.Id
                               where organizationMember.IsActive
                                   && ((identityUser.Email ?? "").Contains(query)
                                       || (identityUser.UserName ?? "").Contains(query))
                               orderby identityUser.Email
                               select new GlobalSearchHit("users", identityUser.Id,
                                   identityUser.UserName ?? identityUser.Email ?? "Usuário",
                                   organizationMember.Role.ToString(),
                                   "/settings/organization?member=" + identityUser.Id, null))
                .Take(limitPerGroup).ToListAsync(cancellationToken);
            hits.AddRange(users);

            var teams = await _db.Teams.AsNoTracking()
                .Where(x => x.IsActive && x.Name.Contains(query))
                .OrderBy(x => x.Name).Take(limitPerGroup)
                .Select(x => new GlobalSearchHit("teams", x.Id.ToString(), x.Name,
                    "Equipe · " + x.Members.Count + " membro(s)", "/teams?team=" + x.Id, null))
                .ToListAsync(cancellationToken);
            hits.AddRange(teams);

            var sprints = await _db.Sprints.AsNoTracking()
                .Where(x => !deniedProjects.Contains(x.ProjectId)
                    && (x.Name.Contains(query) || (x.Goal != null && x.Goal.Contains(query))))
                .OrderByDescending(x => x.StartDate).Take(limitPerGroup)
                .Select(x => new GlobalSearchHit("sprints", x.Id.ToString(), x.Name,
                    x.Project.Key + " · " + x.Status,
                    "/projects/" + x.ProjectId + "/sprints?sprint=" + x.Id, null))
                .ToListAsync(cancellationToken);
            hits.AddRange(sprints);

            var wiki = await _db.WikiPages.AsNoTracking()
                .Where(x => !x.IsDeleted && !deniedProjects.Contains(x.ProjectId)
                    && (x.Title.Contains(query) || x.ContentHtml.Contains(query)))
                .OrderByDescending(x => x.UpdatedAt).Take(limitPerGroup)
                .Select(x => new GlobalSearchHit("wiki", x.Id.ToString(), x.Title,
                    x.Project.Key + " · Wiki",
                    "/projects/" + x.ProjectId + "/wiki/" + x.Id, x.UpdatedAt))
                .ToListAsync(cancellationToken);
            hits.AddRange(wiki);
        }

        var requesterEmail = externalOnly
            ? await _db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => x.Email)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var requests = await _db.ExternalRequests.AsNoTracking()
            .Where(x => (x.Protocol.Contains(query) || x.WorkItem.Title.Contains(query))
                && (!externalOnly || x.WorkItem.RequesterId == userId
                    || (requesterEmail != null && x.RequesterEmail == requesterEmail)))
            .OrderByDescending(x => x.UpdatedAt).Take(limitPerGroup)
            .Select(x => new GlobalSearchHit("requests", x.Id.ToString(), x.WorkItem.Title,
                x.Protocol + " · " + x.TriageStatus,
                externalOnly ? "/portal/" + x.ExternalPortal.PublicSlug + "/acompanhar"
                    : "/requests?request=" + x.Id, x.UpdatedAt))
            .ToListAsync(cancellationToken);
        hits.AddRange(requests);
        return hits;
    }
}

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _db;
    public AuditLogRepository(AppDbContext db) => _db = db;

    public async Task<(IReadOnlyList<AuditLog> Items, int Total)> SearchAsync(
        AuditSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(criteria.EntityType))
            query = query.Where(x => x.EntityType == criteria.EntityType);
        if (!string.IsNullOrWhiteSpace(criteria.EntityId))
        {
            var entityId = criteria.EntityId.Trim();
            query = query.Where(x => x.EntityId == entityId || x.EntityId.EndsWith("=" + entityId));
        }
        if (!string.IsNullOrWhiteSpace(criteria.Action))
            query = query.Where(x => x.Action == criteria.Action);
        if (!string.IsNullOrWhiteSpace(criteria.UserId))
            query = query.Where(x => x.UserId == criteria.UserId);
        if (criteria.From.HasValue) query = query.Where(x => x.OccurredAt >= criteria.From);
        if (criteria.To.HasValue) query = query.Where(x => x.OccurredAt <= criteria.To);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.OccurredAt)
            .Skip((criteria.Page - 1) * criteria.PageSize).Take(criteria.PageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }
}
