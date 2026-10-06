using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.Ai;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Infrastructure.Persistence;

namespace Prisma.Workspace.Infrastructure.Services.Ai;

public sealed class AiWorkspaceReadRepository(AppDbContext db) : IAiWorkspaceReadRepository
{
    public Task<bool> ProjectAvailableAsync(Guid id, CancellationToken ct)
        => db.Projects.AnyAsync(x => x.Id == id && !x.IsArchived, ct);
    public Task<Guid?> BoardProjectAsync(Guid id, CancellationToken ct)
        => db.Boards.Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct);
    public Task<AiWorkItemScope?> WorkItemScopeAsync(Guid id, CancellationToken ct)
        => db.WorkItems.Where(x => x.Id == id && !x.IsArchived && !x.Board.Project.IsArchived)
            .Select(x => new AiWorkItemScope(x.BoardId, x.Board.ProjectId)).SingleOrDefaultAsync(ct);
    public Task<WorkItem?> WorkItemDetailAsync(Guid id, CancellationToken ct)
        => db.WorkItems.AsNoTracking().Include(x => x.Board).Include(x => x.ChecklistItems)
            .Include(x => x.Comments).Include(x => x.Attachments).SingleOrDefaultAsync(x => x.Id == id && !x.IsArchived && !x.Board.Project.IsArchived, ct);
    public async Task<IReadOnlyList<AiSourceCandidate>> ChildrenAsync(Guid id, CancellationToken ct)
        => await db.WorkItems.Where(x => x.ParentId == id && !x.IsArchived && !x.Board.Project.IsArchived)
            .Select(x => new AiSourceCandidate(x.Id, x.Title, x.Board.ProjectId, x.Number)).Take(100).ToListAsync(ct);
    public async Task<IReadOnlyList<AiWorkItemCandidate>> CandidatesAsync(AiWorkItemFilters f, CancellationToken ct)
    {
        var q = db.WorkItems.AsNoTracking().Where(x => !x.IsArchived && !x.Board.Project.IsArchived);
        if (f.ProjectId is { } project) q = q.Where(x => x.Board.ProjectId == project);
        if (f.StageId is { } stage) q = q.Where(x => x.StageId == stage);
        if (f.BoardId is { } board) q = q.Where(x => x.BoardId == board);
        if (f.ResponsibleId is { } responsible) q = q.Where(x => x.ResponsibleId == responsible || x.Assignees.Any(y => y.UserId == responsible));
        if (f.Priority is { } priority) q = q.Where(x => (int)x.Priority == priority);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (f.Overdue) q = q.Where(x => x.DueDate < today && x.CompletedAt == null);
        if (f.DueFrom is { } from) q = q.Where(x => x.DueDate >= from);
        if (f.DueTo is { } to) q = q.Where(x => x.DueDate <= to);
        if (f.UpdatedSince is { } since) q = q.Where(x => x.UpdatedAt >= since);
        if (f.MyUserId is { } user) q = q.Where(x => x.ResponsibleId == user || x.Assignees.Any(y => y.UserId == user));
        return await q.OrderByDescending(x => x.UpdatedAt).Select(x => new AiWorkItemCandidate(x.Id, x.Number, x.Title, x.Board.ProjectId,
            x.StageId, x.Stage == null ? "Sem coluna" : x.Stage.Name, x.DueDate, x.CompletedAt, x.ResponsibleId, x.Priority, x.UpdatedAt)).Take(500).ToListAsync(ct);
    }
    public async Task<object?> ActiveSprintAsync(Guid projectId, DateOnly today, CancellationToken ct)
        => await db.Sprints.Where(x => x.ProjectId == projectId && x.StartDate <= today && x.EndDate >= today)
            .Select(x => new { x.Id, x.Name }).FirstOrDefaultAsync(ct);
    public async Task<object> RecentActivityAsync(Guid[] allowedIds, DateTimeOffset since, CancellationToken ct)
    {
        var events = await db.TaskEvents.Where(x => allowedIds.Contains(x.WorkItemId) && x.CreatedAt >= since)
            .OrderByDescending(x => x.CreatedAt).Select(x => new { x.WorkItemId, x.Kind, x.CreatedAt, x.Payload }).Take(50).ToListAsync(ct);
        var history = await db.StageHistories.Where(x => allowedIds.Contains(x.WorkItemId) && x.EnteredAt >= since)
            .OrderByDescending(x => x.EnteredAt).Select(x => new { x.WorkItemId, x.StageId, x.EnteredAt, x.LeftAt }).Take(50).ToListAsync(ct);
        var entityIds = allowedIds.Select(x => x.ToString()).ToArray();
        var audit = await db.AuditLogs.Where(x => x.OccurredAt >= since && entityIds.Contains(x.EntityId) && x.EntityType == "WorkItem")
            .OrderByDescending(x => x.OccurredAt).Select(x => new { x.EntityId, x.Action, x.OccurredAt }).Take(50).ToListAsync(ct);
        return new { events, history, audit };
    }
    public Task<AiSourceCandidate?> TaskSourceAsync(long number, CancellationToken ct)
        => db.WorkItems.Where(x => x.Number == number && !x.IsArchived && !x.Board.Project.IsArchived)
            .Select(x => new AiSourceCandidate(x.Id, x.Title, x.Board.ProjectId, x.Number)).SingleOrDefaultAsync(ct);
    public Task<AiSourceCandidate?> ProjectSourceAsync(string key, CancellationToken ct)
        => db.Projects.Where(x => x.Key == key && !x.IsArchived)
            .Select(x => new AiSourceCandidate(x.Id, x.Name, x.Id, 0)).FirstOrDefaultAsync(ct);
}
