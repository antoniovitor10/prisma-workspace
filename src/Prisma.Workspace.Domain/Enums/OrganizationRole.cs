namespace Prisma.Workspace.Domain.Enums;

/// <summary>Perfis-base disponíveis dentro de cada organização.</summary>
public enum OrganizationRole
{
    Administrator = 1,
    Manager = 2,
    ProjectManager = 3,
    ScrumMaster = 4,
    ProductOwner = 5,
    TeamMember = 6,
    Developer = 7,
    ExternalRequester = 8,
    Client = 9,
    Viewer = 10
}
