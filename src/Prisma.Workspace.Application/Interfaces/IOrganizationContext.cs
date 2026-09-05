namespace Prisma.Workspace.Application.Interfaces;

/// <summary>Organização ativa da requisição corrente.</summary>
public interface IOrganizationContext
{
    Guid? OrganizationId { get; }

    Guid RequireOrganizationId() => OrganizationId
        ?? throw new InvalidOperationException("Nenhuma organização foi selecionada.");
}
