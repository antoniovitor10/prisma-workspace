using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Repositories;

public class BacklogRepository : IBacklogRepository
{
    private readonly AppDbContext _context;
    public BacklogRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<WorkItem>> GetProjectBacklogAsync(
        Guid projectId, bool includeArchived = false, CancellationToken cancellationToken = default)
        => await _context.WorkItems.AsNoTracking().AsSplitQuery()
            .Include(x => x.Board).Include(x => x.Stage).Include(x => x.Assignees)
            .Include(x => x.WorkItemTags).ThenInclude(x => x.Tag)
            .Include(x => x.OutgoingLinks).ThenInclude(x => x.TargetWorkItem)
            .Include(x => x.IncomingLinks).ThenInclude(x => x.SourceWorkItem)
            .Where(x => x.Board.ProjectId == projectId && (includeArchived || !x.IsArchived))
            .OrderBy(x => x.BacklogRank).ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WorkItem>> GetTrackedByIdsAsync(Guid projectId, IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
        => await _context.WorkItems.Include(x => x.Board)
            .Where(x => x.Board.ProjectId == projectId && !x.IsArchived && ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

    public Task SaveAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
}
