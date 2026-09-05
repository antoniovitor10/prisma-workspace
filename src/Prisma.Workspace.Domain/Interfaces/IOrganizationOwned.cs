namespace Prisma.Workspace.Domain.Interfaces;

/// <summary>
/// Marca uma raiz de agregado pertencente a uma organização.
/// Entidades filhas herdam o escopo pela navegação para a raiz.
/// </summary>
public interface IOrganizationOwned
{
    Guid OrganizationId { get; set; }
}
