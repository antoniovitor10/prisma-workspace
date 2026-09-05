namespace Detran.Kanban.Domain.Entities;

/// <summary>
/// Anexo vinculado a um item de trabalho.
/// </summary>
public class Attachment
{
    /// <summary>Identificador único do anexo.</summary>
    public Guid Id { get; set; }

    /// <summary>Identificador do item de trabalho ao qual o anexo pertence.</summary>
    public Guid WorkItemId { get; set; }

    /// <summary>Caminho de armazenamento do arquivo.</summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>Nome original do arquivo.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Tamanho do arquivo em bytes.</summary>
    public long? FileSize { get; set; }

    /// <summary>Tipo MIME do arquivo (ex: application/pdf).</summary>
    public string? MimeType { get; set; }

    /// <summary>Identificador do usuário que enviou o anexo.</summary>
    public string? UploadedBy { get; set; }

    /// <summary>Indica que o anexo pode ser consultado pelo solicitante no portal.</summary>
    public bool IsExternalVisible { get; set; }

    /// <summary>Data/hora do upload.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    // ── Navegação ──────────────────────────────────────────────

    /// <summary>Item de trabalho ao qual o anexo pertence.</summary>
    public WorkItem WorkItem { get; set; } = null!;
}
