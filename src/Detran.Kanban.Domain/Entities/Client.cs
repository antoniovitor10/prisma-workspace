using Detran.Kanban.Domain.Exceptions;
using Detran.Kanban.Domain.Interfaces;

namespace Detran.Kanban.Domain.Entities;

/// <summary>
/// Cliente ao qual os quadros/projetos podem pertencer.
/// </summary>
public class Client : IOrganizationOwned
{
    /// <summary>Identificador único.</summary>
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    /// <summary>Nome do cliente.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Data/hora de criação.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Quadros vinculados a este cliente.</summary>
    public ICollection<Board> Boards { get; set; } = new List<Board>();

    public static Client Criar(string nome)
    {
        DomainException.Garantir(!string.IsNullOrWhiteSpace(nome), "Nome obrigatório.");
        return new Client
        {
            Id = Guid.NewGuid(),
            Name = nome.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
