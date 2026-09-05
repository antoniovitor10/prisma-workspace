namespace Detran.Kanban.Application.Features.Attachments.Dtos;

/// <summary>
/// DTO de retorno de anexos de WorkItem.
/// </summary>
public class AttachmentDto
{
    public Guid Id { get; set; }
    public Guid WorkItemId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public string? MimeType { get; set; }
    public string? UploadedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
