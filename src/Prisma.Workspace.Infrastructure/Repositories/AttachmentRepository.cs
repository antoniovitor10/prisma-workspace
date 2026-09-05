using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Repositories;

/// <summary>
/// Implementacao do repositorio de anexos usando EF Core.
/// </summary>
public class AttachmentRepository : IAttachmentRepository
{
    private readonly AppDbContext _context;

    public AttachmentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Attachment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Attachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Attachment>> GetByWorkItemIdAsync(
        Guid workItemId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Attachments
            .AsNoTracking()
            .Where(a => a.WorkItemId == workItemId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Attachment> AddAsync(Attachment attachment, CancellationToken cancellationToken = default)
    {
        await _context.Attachments.AddAsync(attachment, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return attachment;
    }

    public async Task DeleteAsync(Attachment attachment, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.Attachments
            .FirstOrDefaultAsync(a => a.Id == attachment.Id, cancellationToken);
        if (tracked is null) return;
        _context.Attachments.Remove(tracked);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
