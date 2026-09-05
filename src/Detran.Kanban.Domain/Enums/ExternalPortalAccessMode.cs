namespace Detran.Kanban.Domain.Enums;

[Flags]
public enum ExternalPortalAccessMode
{
    None = 0,
    PublicLink = 1,
    Login = 2,
    Invitation = 4,
    EmailCode = 8
}

public enum ExternalRequestMessageAuthor
{
    Requester = 1,
    Agent = 2
}
