using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Repositories;

public class ReportRepository : IReportRepository
{
    private readonly AppDbContext _context;
    public ReportRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<WorkItem>> GetTimeReportItemsAsync(
        Guid projectId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        Guid? teamId,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.WorkItems.AsNoTracking().AsSplitQuery()
            .Include(x => x.Board).ThenInclude(x => x.Project)
            .Include(x => x.Board).ThenInclude(x => x.Team)
            .Include(x => x.Team)
            .Include(x => x.WorkflowStatus)
            .Include(x => x.Stage)
            .Include(x => x.Assignees)
            .Include(x => x.TimeEntries.Where(entry =>
                entry.StartedAt < toExclusive
                && (entry.EndedAt == null || entry.EndedAt > fromInclusive)))
            .Where(x => x.Board.ProjectId == projectId);

        if (teamId.HasValue)
            query = query.Where(x => (x.TeamId ?? x.Board.TeamId) == teamId.Value);
        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(x => x.ResponsibleId == userId
                || x.Assignees.Any(a => a.UserId == userId)
                || x.TimeEntries.Any(entry => entry.UserId == userId
                    && entry.StartedAt < toExclusive
                    && (entry.EndedAt == null || entry.EndedAt > fromInclusive)));

        return await query.OrderBy(x => x.Number).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TimeEntry>> GetOrganizationTimeEntriesAsync(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        string? userId,
        Guid? teamId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.TimeEntries.AsNoTracking().AsSplitQuery()
            .Include(entry => entry.WorkItem).ThenInclude(item => item.Board).ThenInclude(board => board.Project)
            .Include(entry => entry.WorkItem).ThenInclude(item => item.Team)
            .Include(entry => entry.WorkItem).ThenInclude(item => item.Board).ThenInclude(board => board.Team)
            .Where(entry => entry.StartedAt < toExclusive
                && (entry.EndedAt == null || entry.EndedAt > fromInclusive));

        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(entry => entry.UserId == userId);
        if (teamId.HasValue)
            query = query.Where(entry => (entry.WorkItem.TeamId ?? entry.WorkItem.Board.TeamId) == teamId.Value);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkItem>> GetCustomFieldReportItemsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => await _context.WorkItems.AsNoTracking().AsSplitQuery()
            .Include(x => x.Board).ThenInclude(x => x.Project)
            .Include(x => x.Stage)
            .Include(x => x.WorkflowStatus)
            .Include(x => x.CustomFieldValues)
            .Where(x => x.Board.ProjectId == projectId && !x.IsArchived)
            .OrderBy(x => x.Number)
            .ToListAsync(cancellationToken);
}
