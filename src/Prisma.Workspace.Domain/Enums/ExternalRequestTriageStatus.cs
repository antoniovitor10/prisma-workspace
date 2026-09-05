namespace Prisma.Workspace.Domain.Enums;

public enum ExternalRequestTriageStatus
{
    New = 1,
    Accepted = 2,
    WaitingForInformation = 3,
    Rejected = 4,
    Duplicate = 5,
    Routed = 6
}

public enum ExternalRequestTriageAction
{
    Submitted = 1,
    Accepted = 2,
    Rejected = 3,
    InformationRequested = 4,
    CategoryChanged = 5,
    PriorityChanged = 6,
    ResponsibleChanged = 7,
    TeamChanged = 8,
    ProjectChanged = 9,
    SentToBacklog = 10,
    SentToKanban = 11,
    MarkedDuplicate = 12,
    LinkedWorkItem = 13
}
