using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Repositories;

public class AnalyticsRepository : IAnalyticsRepository
{
    private const int RowLimit = 20_000;
    private readonly AppDbContext _context;
    public AnalyticsRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<WorkItem>> GetWorkItemsAsync(
        Guid? projectId, CancellationToken cancellationToken = default)
    {
        var query = _context.WorkItems.AsNoTracking().AsSplitQuery()
            .Include(x => x.Board).ThenInclude(x => x.Project)
            .Include(x => x.Board).ThenInclude(x => x.Team)
            .Include(x => x.Team)
            .Include(x => x.Stage)
            .Include(x => x.WorkflowStatus)
            .Include(x => x.TaskType)
            .Include(x => x.Sprint)
            .Include(x => x.Assignees)
            .Include(x => x.Followers)
            .Include(x => x.TimeEntries)
            .Include(x => x.CustomFieldValues)
            .Include(x => x.OutgoingLinks).ThenInclude(x => x.TargetWorkItem)
            .Include(x => x.IncomingLinks).ThenInclude(x => x.SourceWorkItem)
            .AsQueryable();
        if (projectId.HasValue) query = query.Where(x => x.Board.ProjectId == projectId);
        return await query.OrderByDescending(x => x.CreatedAt).Take(RowLimit).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExternalRequest>> GetExternalRequestsAsync(
        Guid? projectId, CancellationToken cancellationToken = default)
    {
        var query = _context.ExternalRequests.AsNoTracking().AsSplitQuery()
            .Include(x => x.ExternalPortal).ThenInclude(x => x.Project)
            .Include(x => x.WorkItem).ThenInclude(x => x.Board).ThenInclude(x => x.Project)
            .Include(x => x.WorkItem).ThenInclude(x => x.Stage)
            .Include(x => x.WorkItem).ThenInclude(x => x.WorkflowStatus)
            .Include(x => x.WorkItem).ThenInclude(x => x.TimeEntries)
            .Include(x => x.WorkItem).ThenInclude(x => x.CustomFieldValues)
            .Include(x => x.Messages)
            .AsQueryable();
        if (projectId.HasValue)
            query = query.Where(x => x.WorkItem.Board.ProjectId == projectId
                || x.ExternalPortal.ProjectId == projectId);
        return await query.OrderByDescending(x => x.CreatedAt).Take(RowLimit).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Project>> GetProjectsAsync(CancellationToken cancellationToken = default)
        => await _context.Projects.AsNoTracking().AsSplitQuery()
            .Include(x => x.Boards).ThenInclude(x => x.WorkItems)
            .Include(x => x.Teams)
            .Include(x => x.Sprints)
            .OrderBy(x => x.Name).Take(RowLimit).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Team>> GetTeamsAsync(CancellationToken cancellationToken = default)
        => await _context.Teams.AsNoTracking().AsSplitQuery()
            .Include(x => x.Members)
            .Include(x => x.Projects)
            .OrderBy(x => x.Name).Take(RowLimit).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Sprint>> GetSprintsAsync(
        Guid? projectId, CancellationToken cancellationToken = default)
    {
        var query = _context.Sprints.AsNoTracking().AsSplitQuery()
            .Include(x => x.Project)
            .Include(x => x.Team)
            .Include(x => x.WorkItems)
            .Include(x => x.ItemSnapshots)
            .AsQueryable();
        if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId);
        return await query.OrderByDescending(x => x.StartDate).Take(RowLimit).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TimeEntry>> GetTimeEntriesAsync(
        Guid? projectId, CancellationToken cancellationToken = default)
    {
        var query = _context.TimeEntries.AsNoTracking().AsSplitQuery()
            .Include(x => x.WorkItem).ThenInclude(x => x.Board).ThenInclude(x => x.Project)
            .Include(x => x.WorkItem).ThenInclude(x => x.Board).ThenInclude(x => x.Team)
            .Include(x => x.WorkItem).ThenInclude(x => x.Team)
            .AsQueryable();
        if (projectId.HasValue) query = query.Where(x => x.WorkItem.Board.ProjectId == projectId);
        return await query.OrderByDescending(x => x.StartedAt).Take(RowLimit).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Comment>> GetCommentsForUserAsync(
        string userId, string? email, CancellationToken cancellationToken = default)
    {
        var mention = string.IsNullOrWhiteSpace(email) ? null : $"@{email}";
        return await _context.Comments.AsNoTracking()
            .Include(x => x.WorkItem).ThenInclude(x => x.Board).ThenInclude(x => x.Project)
            .Where(x => x.UserId != userId && mention != null && x.Content.Contains(mention))
            .OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SavedReport>> GetSavedReportsAsync(
        string userId, Guid? projectId, CancellationToken cancellationToken = default)
        => await _context.SavedReports.AsNoTracking()
            .Where(x => (x.OwnerId == userId || x.IsShared)
                && (!projectId.HasValue || x.ProjectId == projectId || x.ProjectId == null))
            .OrderByDescending(x => x.UpdatedAt).ToListAsync(cancellationToken);

    public Task<SavedReport?> GetSavedReportAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.SavedReports.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public void AddSavedReport(SavedReport report) => _context.SavedReports.Add(report);
    public void RemoveSavedReport(SavedReport report) => _context.SavedReports.Remove(report);
    public Task SaveAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
