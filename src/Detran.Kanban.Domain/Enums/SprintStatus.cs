namespace Detran.Kanban.Domain.Enums;

public enum SprintStatus
{
    Planned = 1,
    Active = 2,
    Closed = 3,
    Cancelled = 4
}

public enum SprintIncompleteItemsAction
{
    ReturnToBacklog = 1,
    MoveToSprint = 2
}

public enum SprintItemOutcome
{
    Completed = 1,
    ReturnedToBacklog = 2,
    MovedToSprint = 3
}
