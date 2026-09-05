using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Domain.Interfaces;

namespace Prisma.Workspace.Domain.Entities;

public class Notification : IOrganizationOwned
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Link { get; set; }
    public Guid? WorkItemId { get; set; }
    public Guid? ExternalRequestId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? SprintId { get; set; }
    public string? DeduplicationKey { get; set; }
    public bool IsInAppVisible { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public NotificationEmailStatus EmailStatus { get; set; }
    public int EmailAttempts { get; set; }
    public DateTimeOffset? EmailSentAt { get; set; }
    public DateTimeOffset? LastEmailAttemptAt { get; set; }
    public string? EmailFailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public static Notification Create(
        Guid organizationId,
        string userId,
        NotificationType type,
        string title,
        string message,
        bool inAppEnabled,
        bool emailEnabled,
        string? link = null,
        Guid? workItemId = null,
        Guid? externalRequestId = null,
        Guid? projectId = null,
        Guid? sprintId = null,
        string? deduplicationKey = null)
    {
        DomainException.Garantir(organizationId != Guid.Empty, "Organização obrigatória.");
        DomainException.Garantir(!string.IsNullOrWhiteSpace(userId), "Destinatário obrigatório.");
        DomainException.Garantir(!string.IsNullOrWhiteSpace(title), "Título da notificação obrigatório.");
        DomainException.Garantir(!string.IsNullOrWhiteSpace(message), "Mensagem da notificação obrigatória.");
        DomainException.Garantir(inAppEnabled || emailEnabled,
            "Ao menos um canal de notificação precisa estar habilitado.");

        return new Notification
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            Type = type,
            Title = title.Trim(),
            Message = message.Trim(),
            Link = string.IsNullOrWhiteSpace(link) ? null : link.Trim(),
            WorkItemId = workItemId,
            ExternalRequestId = externalRequestId,
            ProjectId = projectId,
            SprintId = sprintId,
            DeduplicationKey = string.IsNullOrWhiteSpace(deduplicationKey) ? null : deduplicationKey.Trim(),
            IsInAppVisible = inAppEnabled,
            EmailStatus = emailEnabled ? NotificationEmailStatus.Pending : NotificationEmailStatus.NotRequested,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void MarkAsRead()
    {
        if (IsRead) return;
        IsRead = true;
        ReadAt = DateTimeOffset.UtcNow;
    }

    public void MarkEmailSent()
    {
        EmailAttempts++;
        LastEmailAttemptAt = DateTimeOffset.UtcNow;
        EmailSentAt = LastEmailAttemptAt;
        EmailFailureReason = null;
        EmailStatus = NotificationEmailStatus.Sent;
    }

    public void MarkEmailSkipped()
    {
        EmailAttempts++;
        LastEmailAttemptAt = DateTimeOffset.UtcNow;
        EmailFailureReason = null;
        EmailStatus = NotificationEmailStatus.Skipped;
    }

    public void MarkEmailFailed(string reason, int maximumAttempts = 5)
    {
        EmailAttempts++;
        LastEmailAttemptAt = DateTimeOffset.UtcNow;
        EmailFailureReason = string.IsNullOrWhiteSpace(reason) ? "Falha de entrega." : reason[..Math.Min(reason.Length, 500)];
        EmailStatus = EmailAttempts >= maximumAttempts
            ? NotificationEmailStatus.Failed
            : NotificationEmailStatus.Pending;
    }
}

public class NotificationPreference : IOrganizationOwned
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public bool InAppEnabled { get; set; }
    public bool EmailEnabled { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public void Update(bool inAppEnabled, bool emailEnabled)
    {
        InAppEnabled = inAppEnabled;
        EmailEnabled = emailEnabled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
