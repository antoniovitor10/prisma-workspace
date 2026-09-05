namespace Prisma.Workspace.Domain.Enums;

public enum SlaStatus
{
    WithinDeadline = 1,
    NearDue = 2,
    Overdue = 3,
    Paused = 4,
    Met = 5,
    NotApplicable = 6
}
