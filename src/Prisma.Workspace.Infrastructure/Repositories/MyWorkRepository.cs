using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Repositories;

public sealed class MyWorkRepository : IMyWorkRepository
{
    private readonly AppDbContext _context;
    public MyWorkRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<WorkItem>> GetTasksAsync(string userId, CancellationToken ct = default)
        => await _context.WorkItems.AsNoTracking().AsSplitQuery()
            .Include(x => x.Board).ThenInclude(x => x.Project)
            .Include(x => x.Stage)
            .Include(x => x.WorkflowStatus)
            .Include(x => x.Team)
            .Include(x => x.TaskType)
            .Include(x => x.Assignees)
            .Include(x => x.Followers)
            .Include(x => x.ExternalRequest)
            .Include(x => x.WorkItemTags).ThenInclude(x => x.Tag)
            .Include(x => x.TimeEntries)
            .Include(x => x.OutgoingLinks).ThenInclude(x => x.TargetWorkItem)
            .Include(x => x.IncomingLinks).ThenInclude(x => x.SourceWorkItem)
            .Where(x => !x.IsArchived && x.ParentId == null
                && (x.ResponsibleId == userId
                    || x.Assignees.Any(a => a.UserId == userId)
                    || x.CreatedBy == userId
                    || x.RequesterId == userId
                    || x.Followers.Any(f => f.UserId == userId)))
            .OrderBy(x => x.DueDate == null).ThenBy(x => x.DueDate)
            .ThenByDescending(x => x.Priority)
            .Take(500)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Project>> GetActiveProjectsAsync(
        string userId, CancellationToken ct = default)
        => await _context.Projects.AsNoTracking().AsSplitQuery()
            .Include(x => x.Members)
            .Where(x => !x.IsArchived && x.Status == ProjectStatus.Active
                && (x.OwnerId == userId || x.Members.Any(m => m.UserId == userId)))
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Sprint>> GetSprintsAsync(
        string userId, CancellationToken ct = default)
        => await _context.Sprints.AsNoTracking().AsSplitQuery()
            .Include(x => x.Project)
            .Include(x => x.WorkItems)
            .Where(x => !x.Project.IsArchived && x.Project.Status == ProjectStatus.Active
                && (x.Project.OwnerId == userId || x.Project.Members.Any(m => m.UserId == userId)))
            .OrderBy(x => x.EndDate)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Comment>> GetRecentCommentsAsync(
        string userId, string? userEmail, int limit, CancellationToken ct = default)
    {
        var mention = string.IsNullOrWhiteSpace(userEmail) ? null : $"@{userEmail}";
        return await _context.Comments.AsNoTracking()
            .Include(x => x.WorkItem).ThenInclude(x => x.Board).ThenInclude(x => x.Project)
            .Where(x => x.UserId != userId && (
                (mention != null && x.Content.Contains(mention))
                || x.WorkItem.ResponsibleId == userId
                || x.WorkItem.CreatedBy == userId
                || x.WorkItem.Assignees.Any(a => a.UserId == userId)
                || x.WorkItem.Followers.Any(f => f.UserId == userId)))
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }
}
