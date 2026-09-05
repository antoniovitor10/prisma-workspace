namespace Prisma.Workspace.Domain.Enums;

public enum NotificationType
{
    TaskAssigned = 1,
    Mention = 2,
    Comment = 3,
    PublicReply = 4,
    DeadlineNear = 5,
    TaskOverdue = 6,
    ExternalRequestReceived = 7,
    ExternalReply = 8,
    SlaNearDue = 9,
    SlaOverdue = 10,
    SprintStarted = 11,
    SprintCompleted = 12,
    StatusChanged = 13
}

public enum NotificationEmailStatus
{
    NotRequested = 0,
    Pending = 1,
    Sent = 2,
    Failed = 3,
    Skipped = 4
}
