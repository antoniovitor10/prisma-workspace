using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Interfaces;

public sealed record NotificationEnvelope(
    Guid OrganizationId,
    string RecipientId,
    NotificationType Type,
    string Title,
    string Message,
    string? Link = null,
    Guid? WorkItemId = null,
    Guid? ExternalRequestId = null,
    Guid? ProjectId = null,
    Guid? SprintId = null,
    string? DeduplicationKey = null);

public interface IPlatformNotificationPublisher
{
    Task<bool> PublishAsync(NotificationEnvelope notification, CancellationToken cancellationToken = default);
    Task<int> PublishManyAsync(IEnumerable<NotificationEnvelope> notifications,
        CancellationToken cancellationToken = default);
}

public interface INotificationRepository
{
    Task<(IReadOnlyList<Notification> Items, int Total, int Unread)> GetAsync(
        string userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Notification?> GetAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationPreference>> GetPreferencesAsync(
        string userId, CancellationToken cancellationToken = default);
    Task<NotificationPreference?> GetPreferenceAsync(
        string userId, NotificationType type, CancellationToken cancellationToken = default);
    void AddPreference(NotificationPreference preference);
    Task MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}

public sealed record GlobalSearchHit(
    string Kind,
    string Id,
    string Title,
    string Subtitle,
    string Path,
    DateTimeOffset? UpdatedAt = null);

public interface IGlobalSearchRepository
{
    Task<IReadOnlyList<GlobalSearchHit>> SearchAsync(
        string query, int limitPerGroup, string userId, CancellationToken cancellationToken = default);
}

public sealed record AuditSearchCriteria(
    string? EntityType,
    string? EntityId,
    string? Action,
    string? UserId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PageSize);

public interface IAuditLogRepository
{
    Task<(IReadOnlyList<AuditLog> Items, int Total)> SearchAsync(
        AuditSearchCriteria criteria, CancellationToken cancellationToken = default);
}

public interface IAuditContext
{
    string? UserId { get; }
    string Origin { get; }
    string? IpAddress { get; }
    string? CorrelationId { get; }
}

public sealed record RefreshTokenIssue(string Token, string UserId, Guid FamilyId, DateTimeOffset ExpiresAt);

public interface IRefreshTokenService
{
    Task<RefreshTokenIssue> IssueAsync(string userId, string? ipAddress,
        CancellationToken cancellationToken = default);
    Task<RefreshTokenIssue?> RotateAsync(string token, string? ipAddress,
        CancellationToken cancellationToken = default);
    Task RevokeFamilyAsync(string token, string? ipAddress,
        CancellationToken cancellationToken = default);
    Task RevokeAllForUserAsync(string userId, string? ipAddress,
        CancellationToken cancellationToken = default);
}

public interface IApplicationEmailSender
{
    bool IsConfigured { get; }
    bool CanExposeLocalToken { get; }
    Task<bool> SendAsync(string email, string subject, string body,
        CancellationToken cancellationToken = default);
}
