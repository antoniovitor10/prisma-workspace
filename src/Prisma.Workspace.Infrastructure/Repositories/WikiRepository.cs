using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Repositories;

public class WikiRepository : IWikiRepository
{
    private readonly AppDbContext _context;
    public WikiRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<WikiPage>> GetTreeAsync(
        Guid projectId, bool includeDeleted, CancellationToken cancellationToken = default)
        => await _context.WikiPages
            .Where(x => x.ProjectId == projectId && (includeDeleted || !x.IsDeleted))
            .OrderBy(x => x.Position).ThenBy(x => x.Title)
            .ToListAsync(cancellationToken);

    public Task<WikiPage?> GetByIdAsync(Guid pageId, CancellationToken cancellationToken = default)
        => _context.WikiPages.FirstOrDefaultAsync(x => x.Id == pageId, cancellationToken);

    public Task<bool> HasAnyAsync(Guid projectId, CancellationToken cancellationToken = default)
        => _context.WikiPages.AnyAsync(x => x.ProjectId == projectId, cancellationToken);

    public async Task<double> GetNextPositionAsync(
        Guid projectId, Guid? parentPageId, CancellationToken cancellationToken = default)
    {
        var max = await _context.WikiPages
            .Where(x => x.ProjectId == projectId && x.ParentPageId == parentPageId && !x.IsDeleted)
            .Select(x => (double?)x.Position)
            .MaxAsync(cancellationToken);
        return (max ?? 0) + 10;
    }

    public async Task AddAsync(WikiPage page, CancellationToken cancellationToken = default)
    {
        await _context.WikiPages.AddAsync(page, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    // ── Revisões ────────────────────────────────────────────────────────

    public Task<WikiPageRevision?> GetLatestRevisionAsync(Guid pageId, CancellationToken cancellationToken = default)
        => _context.WikiPageRevisions
            .Where(x => x.WikiPageId == pageId)
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<WikiPageRevision>> GetRevisionsAsync(Guid pageId, CancellationToken cancellationToken = default)
        => await _context.WikiPageRevisions.AsNoTracking()
            .Where(x => x.WikiPageId == pageId)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);

    public Task<WikiPageRevision?> GetRevisionAsync(Guid revisionId, Guid pageId, CancellationToken cancellationToken = default)
        => _context.WikiPageRevisions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == revisionId && x.WikiPageId == pageId, cancellationToken);

    public void AddRevision(WikiPageRevision revision) => _context.WikiPageRevisions.Add(revision);

    // ── Anexos ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<WikiAttachment>> GetAttachmentsAsync(Guid pageId, CancellationToken cancellationToken = default)
        => await _context.WikiAttachments.AsNoTracking()
            .Where(x => x.WikiPageId == pageId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<WikiAttachment?> GetAttachmentAsync(Guid attachmentId, Guid pageId, CancellationToken cancellationToken = default)
        => _context.WikiAttachments
            .FirstOrDefaultAsync(x => x.Id == attachmentId && x.WikiPageId == pageId, cancellationToken);

    public void AddAttachment(WikiAttachment attachment) => _context.WikiAttachments.Add(attachment);

    public void RemoveAttachment(WikiAttachment attachment) => _context.WikiAttachments.Remove(attachment);

    // ── Vínculos ────────────────────────────────────────────────────────

    public Task<WorkItem?> GetProjectWorkItemByNumberAsync(long number, Guid projectId, CancellationToken cancellationToken = default)
        => _context.WorkItems.AsNoTracking()
            .Include(x => x.Board).ThenInclude(x => x.Project)
            .FirstOrDefaultAsync(x => x.Number == number && x.Board.ProjectId == projectId && !x.IsArchived, cancellationToken);

    public Task<WikiPageWorkItemLink?> GetLinkAsync(Guid pageId, Guid workItemId, CancellationToken cancellationToken = default)
        => _context.WikiPageWorkItemLinks
            .FirstOrDefaultAsync(x => x.WikiPageId == pageId && x.WorkItemId == workItemId, cancellationToken);

    public async Task<IReadOnlyList<WikiPageWorkItemLink>> GetLinksByPageAsync(Guid pageId, CancellationToken cancellationToken = default)
        => await _context.WikiPageWorkItemLinks.AsNoTracking()
            .Include(x => x.WorkItem).ThenInclude(x => x.Board).ThenInclude(x => x.Project)
            .Where(x => x.WikiPageId == pageId)
            .OrderBy(x => x.WorkItem.Number)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WikiPageWorkItemLink>> GetLinksByWorkItemAsync(Guid workItemId, CancellationToken cancellationToken = default)
        => await _context.WikiPageWorkItemLinks.AsNoTracking()
            .Include(x => x.Page)
            .Where(x => x.WorkItemId == workItemId && !x.Page.IsDeleted)
            .OrderBy(x => x.Page.Title)
            .ToListAsync(cancellationToken);

    public void AddLink(WikiPageWorkItemLink link) => _context.WikiPageWorkItemLinks.Add(link);

    public void RemoveLink(WikiPageWorkItemLink link) => _context.WikiPageWorkItemLinks.Remove(link);
}
