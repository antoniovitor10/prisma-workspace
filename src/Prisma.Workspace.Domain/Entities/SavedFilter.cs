using System;

namespace Prisma.Workspace.Domain.Entities;

/// <summary>
/// Filtros salvos por usuários em um determinado quadro.
/// </summary>
public class SavedFilter
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FilterJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    // Navegação
    public Board Board { get; set; } = null!;
}
