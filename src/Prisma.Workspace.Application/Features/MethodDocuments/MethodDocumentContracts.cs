namespace Prisma.Workspace.Application.Features.MethodDocuments;

// DTOs HTTP dos documentos do método (HU-0008, T-0020). JSON camelCase; nunca levam texto nas listas.
public sealed record MethodDocumentSummaryDto(
    string Key, string Kind, Guid WorkItemId, string? FilePath, string? Title, string? Status,
    int Version, string ContentHash, string UpdatedByName, string UpdatedVia, DateTimeOffset UpdatedAtUtc);

public sealed record MethodDocumentDto(
    string Key, string Kind, Guid WorkItemId, string? FilePath, string? Title, string? Status,
    int Version, string ContentHash, string UpdatedByName, string UpdatedVia, DateTimeOffset UpdatedAtUtc,
    string Content, string? ParentKey, bool Locked, bool CanEdit);

public sealed record MethodDocumentRevisionDto(
    int Version, string ContentHash, string AuthorName, string Via, DateTimeOffset CreatedAtUtc, string? Content = null);

public sealed record SaveMethodDocumentRequest(string Content, int BaseVersion, string? FilePath = null, string? ParentKey = null);

public sealed record UpdateMethodDocumentHeaderRequest(int BaseVersion, string? Title = null, string? Status = null);

public sealed record RelinkMethodTaskRequest(string HuKey);

public sealed record MethodTaskParentDto(string TaskId, Guid WorkItemId, string HuKey, Guid ParentWorkItemId);

public interface IMethodDocumentOperations
{
    Task<IReadOnlyList<MethodDocumentSummaryDto>> ListAsync(Guid projectId, DateTimeOffset? since, Guid? workItemId, CancellationToken ct);
    Task<MethodDocumentDto> GetAsync(Guid projectId, string key, CancellationToken ct);
    Task<IReadOnlyList<MethodDocumentRevisionDto>> RevisionsAsync(Guid projectId, string key, CancellationToken ct);
    Task<MethodDocumentRevisionDto> RevisionAsync(Guid projectId, string key, int version, CancellationToken ct);
    Task<MethodDocumentDto> SaveAsync(Guid projectId, string key, SaveMethodDocumentRequest request, CancellationToken ct);
    Task<MethodDocumentDto> UpdateHeaderAsync(Guid projectId, string key, UpdateMethodDocumentHeaderRequest request, CancellationToken ct);
    Task<MethodTaskParentDto> RelinkTaskAsync(Guid projectId, string taskId, RelinkMethodTaskRequest request, CancellationToken ct);
}
