using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Domain.Interfaces;

namespace Prisma.Workspace.Domain.Entities;

/// <summary>
/// Etiqueta (tag) aplicável a tarefas. Ex.: "SP#28", "urgente".
/// </summary>
public class Tag : IOrganizationOwned
{
    public const string CorPadrao = "#64748B";

    /// <summary>Identificador único.</summary>
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    /// <summary>Nome da tag.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Cor do chip (hex).</summary>
    public string Color { get; set; } = CorPadrao;

    /// <summary>Data/hora de criação.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Vínculos com tarefas.</summary>
    public ICollection<WorkItemTag> WorkItemTags { get; set; } = new List<WorkItemTag>();

    public static Tag Criar(string nome, string? cor)
    {
        DomainException.Garantir(!string.IsNullOrWhiteSpace(nome), "Nome obrigatório.");
        return new Tag
        {
            Id = Guid.NewGuid(),
            Name = nome.Trim(),
            Color = string.IsNullOrWhiteSpace(cor) ? CorPadrao : cor.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
