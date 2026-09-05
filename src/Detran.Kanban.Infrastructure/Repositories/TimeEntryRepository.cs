using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Detran.Kanban.Infrastructure.Repositories;

/// <summary>
/// Implementacao do repositorio de lancamentos de tempo usando EF Core.
/// </summary>
public class TimeEntryRepository : ITimeEntryRepository
{
    private readonly AppDbContext _context;

    public TimeEntryRepository(AppDbContext context)
    {
        _context = context;
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

    public async Task UpdateAsync(TimeEntry timeEntry, CancellationToken cancellationToken = default)
    {
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
