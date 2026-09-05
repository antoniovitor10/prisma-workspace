namespace Detran.Kanban.Domain.Entities;

/// <summary>Arquivo anexado a uma página do wiki (documentos; as imagens
/// embutidas no texto ficam no próprio conteúdo).</summary>
public class WikiAttachment
{
    public Guid Id { get; private set; }
    public Guid WikiPageId { get; private set; }
    public string StoragePath { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public long FileSize { get; private set; }
    public string? MimeType { get; private set; }
    public string UploadedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public WikiPage Page { get; private set; } = null!;

    private WikiAttachment() { }

    public static WikiAttachment Create(
        Guid pageId, string storagePath, string fileName, long fileSize, string? mimeType, string userId)
        => new()
        {
            Id = Guid.NewGuid(),
            WikiPageId = pageId,
            StoragePath = storagePath,
            FileName = fileName,
            FileSize = fileSize,
            MimeType = mimeType,
            UploadedByUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
}
