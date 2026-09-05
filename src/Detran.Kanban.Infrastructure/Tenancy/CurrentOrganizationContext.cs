using Detran.Kanban.Application.Interfaces;

namespace Detran.Kanban.Infrastructure.Tenancy;

/// <summary>Contexto scoped preenchido uma única vez pelo middleware da API.</summary>
public sealed class CurrentOrganizationContext : IOrganizationContext
{
    public Guid? OrganizationId { get; private set; }

    public void Set(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("Organização inválida.", nameof(organizationId));
        if (OrganizationId is not null && OrganizationId != organizationId)
            throw new InvalidOperationException("A organização da requisição não pode ser alterada.");
        OrganizationId = organizationId;
    }
}
