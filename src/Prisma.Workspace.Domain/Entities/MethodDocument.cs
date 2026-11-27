using Prisma.Workspace.Domain.Interfaces;

namespace Prisma.Workspace.Domain.Entities;

/// <summary>Tipo do documento do método guardado no Prisma (HU-0008).</summary>
public enum MethodDocumentKind
{
    Epic = 1,
    Hu = 2,
    Prd = 3,
    Task = 4,
}

/// <summary>
/// Texto exato de um arquivo do método (épico, HU, PRD ou corpo da task), ligado ao item do quadro do método.
/// O conteúdo é o arquivo inteiro, sem conversão para HTML; só o fim de linha é normalizado para LF (PRD D1, D3).
/// Toda gravação gera uma <see cref="MethodDocumentRevision"/> imutável na mesma transação.
/// </summary>
public sealed class MethodDocument : IOrganizationOwned
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid WorkItemId { get; set; }
    public MethodDocumentKind Kind { get; set; }

    /// <summary><c>EPIC-0001</c>, <c>HU-0001</c> ou <c>AVULSAS</c>; nulo em PRD e Task.</summary>
    public string? MethodKey { get; set; }

    /// <summary>Caminho relativo em <c>docs/trabalho/</c>; nulo na História "Avulsas".</summary>
    public string? FilePath { get; set; }

    public string Content { get; set; } = string.Empty;

    /// <summary>SHA-256 do <see cref="Content"/> em UTF-8, hexadecimal minúsculo.</summary>
    public string ContentHash { get; set; } = string.Empty;

    public int Version { get; set; }

    /// <summary>Status do cabeçalho, só em Hu e Epic (aberta, planejada, concluida, cancelada).</summary>
    public string? Status { get; set; }

    public string? UpdatedByUserId { get; set; }
    public string UpdatedByName { get; set; } = string.Empty;

    /// <summary>Canal da última gravação: <c>ui</c> ou <c>mcp</c>.</summary>
    public string UpdatedVia { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public WorkItem WorkItem { get; set; } = null!;
    public ICollection<MethodDocumentRevision> Revisions { get; set; } = new List<MethodDocumentRevision>();
}

/// <summary>Versão completa e imutável de um <see cref="MethodDocument"/> (só inclusão, G-HISTORY).</summary>
public sealed class MethodDocumentRevision : IOrganizationOwned
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }

    /// <summary>Projeto do documento, repetido para o filtro por projeto e a barreira do token do método.</summary>
    public Guid ProjectId { get; set; }

    public Guid DocumentId { get; set; }
    public int Version { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string? AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Via { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }

    public MethodDocument Document { get; set; } = null!;
}
