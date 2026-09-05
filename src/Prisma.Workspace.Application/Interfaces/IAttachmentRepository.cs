using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Application.Interfaces;

/// <summary>
/// Repositorio de metadados de anexos.
/// </summary>
public interface IAttachmentRepository
{
    Task<Attachment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Attachment>> GetByWorkItemIdAsync(Guid workItemId, CancellationToken cancellationToken = default);
    Task<Attachment> AddAsync(Attachment attachment, CancellationToken cancellationToken = default);
    Task DeleteAsync(Attachment attachment, CancellationToken cancellationToken = default);
}
