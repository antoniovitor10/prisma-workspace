using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Domain.Interfaces;

namespace Prisma.Workspace.Domain.Entities;

/// <summary>
/// Tipo de tarefa (ex.: "Análise de Sistemas", "Suporte Técnico").
/// Chip colorido exibido no card e no modal da tarefa.
/// </summary>
public class TaskType : IOrganizationOwned
{
    public const string CorPadrao = "#1E7BD7";

    /// <summary>Identificador único.</summary>
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    /// <summary>Nome do tipo.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Cor do chip (hex, ex.: "#1E7BD7").</summary>
    public string Color { get; set; } = CorPadrao;

    /// <summary>Data/hora de criação.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    public static TaskType Criar(string nome, string? cor)
    {
        DomainException.Garantir(!string.IsNullOrWhiteSpace(nome), "Nome obrigatório.");
        return new TaskType
        {
            Id = Guid.NewGuid(),
            Name = nome.Trim(),
            Color = string.IsNullOrWhiteSpace(cor) ? CorPadrao : cor.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
