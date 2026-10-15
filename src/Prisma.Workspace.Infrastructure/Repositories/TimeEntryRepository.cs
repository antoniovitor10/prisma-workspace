using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Repositories;

/// <summary>
/// Implementacao do repositorio de lancamentos de tempo usando EF Core.
/// </summary>
public class TimeEntryRepository : ITimeEntryRepository
{
    private readonly AppDbContext _context;
    private readonly IProjectAccessService? _projectAccess;

    public TimeEntryRepository(AppDbContext context, IProjectAccessService? projectAccess = null)
    {
        _context = context;
        _projectAccess = projectAccess;
    }

    public async Task<TimeEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.TimeEntries
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<TimeEntry?> GetRunningByUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.TimeEntries
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.EndedAt == null)
            .OrderByDescending(t => t.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TimeEntry>> GetByWorkItemIdAsync(
        Guid workItemId,
        CancellationToken cancellationToken = default)
    {
        return await _context.TimeEntries
            .AsNoTracking()
            .Where(t => t.WorkItemId == workItemId)
            .OrderByDescending(t => t.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<TimeEntry> AddAsync(TimeEntry timeEntry, CancellationToken cancellationToken = default)
    {
        await _context.TimeEntries.AddAsync(timeEntry, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return timeEntry;
    }

    public async Task DeleteAsync(TimeEntry timeEntry, CancellationToken cancellationToken = default)
    {
        _context.TimeEntries.Remove(timeEntry);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> HasOverlapAsync(string userId, Guid exceptId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
        => _context.TimeEntries.IgnoreQueryFilters().AnyAsync(t => t.UserId == userId && t.Id != exceptId
            && t.StartedAt < end && (t.EndedAt == null || t.EndedAt > start), cancellationToken);

    public async Task<IReadOnlyList<Prisma.Workspace.Application.Features.MeTime.Queries.TimeAdjustmentTaskDto>> SearchAdjustmentTasksAsync(
        string userId, string query, CancellationToken cancellationToken = default)
    {
        // Uma consulta de candidatos, sem carregar cartões nem fazer uma requisição por quadro.
        var text = query.Trim().TrimStart('#');
        var candidates = await _context.WorkItems.AsNoTracking()
            .Where(w => !w.IsArchived && !w.Board.Project.IsArchived
                && (text == "" || w.Title.Contains(text) || w.Number.ToString().Contains(text)))
            .OrderByDescending(w => w.ResponsibleId == userId || w.Assignees.Any(a => a.UserId == userId))
            .ThenBy(w => w.Title).ThenBy(w => w.Id)
            .Select(w => new { w.Id, w.Number, w.Title, BoardName = w.Board.Name,
                w.Board.ProjectId, Assigned = w.ResponsibleId == userId || w.Assignees.Any(a => a.UserId == userId) })
            .ToListAsync(cancellationToken);
        var accessible = _projectAccess is null ? new HashSet<Guid>()
            : await _projectAccess.GetAccessibleProjectIdsAsync(candidates.Select(w => w.ProjectId), userId, cancellationToken);
        return candidates.Where(w => accessible.Contains(w.ProjectId)).Take(50)
            .Select(w => new Prisma.Workspace.Application.Features.MeTime.Queries.TimeAdjustmentTaskDto(
                w.Id, w.Number, w.Title, w.BoardName, w.Assigned)).ToList();
    }

    public async Task UpdateAsync(TimeEntry timeEntry, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(timeEntry).State == EntityState.Detached)
            _context.TimeEntries.Update(timeEntry);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetTotalSecondsByWorkItemIdAsync(
        Guid workItemId,
        CancellationToken cancellationToken = default)
    {
        var entries = await _context.TimeEntries
            .AsNoTracking()
            .Where(t => t.WorkItemId == workItemId)
            .ToListAsync(cancellationToken);

        return SumDurations(entries);
    }

    public async Task<int> GetTotalSecondsByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var entries = await _context.TimeEntries
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .ToListAsync(cancellationToken);

        return SumDurations(entries);
    }

    public async Task<int> GetTotalSecondsByBoardIdAsync(
        Guid boardId,
        CancellationToken cancellationToken = default)
    {
        var entries = await _context.TimeEntries
            .AsNoTracking()
            .Where(t => t.WorkItem.BoardId == boardId)
            .ToListAsync(cancellationToken);

        return SumDurations(entries);
    }

    public async Task<IReadOnlyList<TimeEntry>> GetByUserInRangeAsync(
        string userId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken = default)
    {
        return await _context.TimeEntries
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.StartedAt >= fromInclusive && t.StartedAt < toExclusive)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TimeEntry>> GetByUserInRangeWithWorkItemAsync(
        string userId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken = default)
    {
        return await _context.TimeEntries
            .AsNoTracking()
            .Include(t => t.WorkItem)
            .Where(t => t.UserId == userId && t.StartedAt >= fromInclusive && t.StartedAt < toExclusive)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TimeEntry>> GetByUsersInRangeAsync(
        IReadOnlyCollection<string> userIds,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken = default)
    {
        return await _context.TimeEntries
            .AsNoTracking()
            .Where(t => userIds.Contains(t.UserId) && t.StartedAt >= fromInclusive && t.StartedAt < toExclusive)
            .ToListAsync(cancellationToken);
    }

    private static int SumDurations(IEnumerable<TimeEntry> entries)
    {
        var now = DateTimeOffset.UtcNow;
        return entries.Sum(entry =>
        {
            var endedAt = entry.EndedAt ?? now;
            return Math.Max(0, (int)(endedAt - entry.StartedAt).TotalSeconds);
        });
    }
}
