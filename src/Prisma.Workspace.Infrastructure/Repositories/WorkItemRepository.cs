using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório de WorkItem usando EF Core.
/// </summary>
public class WorkItemRepository : IWorkItemRepository
{
    private readonly AppDbContext _context;

    public WorkItemRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<WorkItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.WorkItems
            .Include(w => w.Board).ThenInclude(w => w.Project)
            .Include(w => w.Assignees)
            .Include(w => w.Attachments)
            .Include(w => w.TimeEntries)
            .Include(w => w.SubItems)
            .Include(w => w.StageHistories)
            .Include(w => w.Followers)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<WorkItem?> GetForMoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.WorkItems
            .Include(w => w.Board).ThenInclude(w => w.Project)
            .Include(w => w.Assignees)
            .Include(w => w.Followers)
            .Include(w => w.StageHistories)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkItem>> GetByBoardIdAsync(Guid boardId, CancellationToken cancellationToken = default)
    {
        return await _context.WorkItems
            .AsNoTracking()
            .Include(w => w.Assignees)
            .Include(w => w.Attachments)
            .Include(w => w.SubItems)
            .Include(w => w.TimeEntries)
            .Include(w => w.TaskType)
            .Include(w => w.WorkflowStatus)
            .Include(w => w.Team)
            .Include(w => w.WorkItemTags).ThenInclude(wt => wt.Tag)
            .Include(w => w.ChecklistItems)
            .Include(w => w.CustomFieldValues)
            .Include(w => w.OutgoingLinks).ThenInclude(x => x.TargetWorkItem)
            .Include(w => w.IncomingLinks).ThenInclude(x => x.SourceWorkItem)
            .Where(w => w.BoardId == boardId && w.ParentId == null && !w.IsArchived)
            .OrderBy(w => w.Position)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkItem>> GetSubItemsAsync(Guid parentId, CancellationToken cancellationToken = default)
    {
        return await _context.WorkItems
            .AsNoTracking()
            .Include(w => w.Assignees)
            .Include(w => w.Attachments)
            .Include(w => w.SubItems)
            .Include(w => w.TimeEntries)
            .Where(w => w.ParentId == parentId && !w.IsArchived)
            .OrderBy(w => w.Position)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkItemAssignee>> GetAssigneesAsync(
        Guid workItemId,
        CancellationToken cancellationToken = default)
    {
        return await _context.WorkItemAssignees
            .AsNoTracking()
            .Where(a => a.WorkItemId == workItemId)
            .OrderBy(a => a.AssignedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkItem> AddAsync(WorkItem workItem, CancellationToken cancellationToken = default)
    {
        await _context.WorkItems.AddAsync(workItem, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return workItem;
    }

    public async Task UpdateAsync(WorkItem workItem, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(workItem).State == EntityState.Detached)
        {
            _context.WorkItems.Attach(workItem);
            _context.Entry(workItem).State = EntityState.Modified;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateWithEventAsync(
        WorkItem workItem,
        TaskEvent taskEvent,
        CancellationToken cancellationToken = default)
    {
        if (_context.Entry(workItem).State == EntityState.Detached)
        {
            _context.WorkItems.Attach(workItem);
            _context.Entry(workItem).State = EntityState.Modified;
        }

        await _context.TaskEvents.AddAsync(taskEvent, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(WorkItem workItem, CancellationToken cancellationToken = default)
    {
        _context.WorkItems.Remove(workItem);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddAssigneeAsync(
        Guid workItemId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var workItemExists = await _context.WorkItems
            .AnyAsync(w => w.Id == workItemId, cancellationToken);

        if (!workItemExists)
        {
            throw new ArgumentException("O item de trabalho especificado nao existe.");
        }

        var alreadyAssigned = await _context.WorkItemAssignees
            .AnyAsync(a => a.WorkItemId == workItemId && a.UserId == userId, cancellationToken);

        if (alreadyAssigned)
        {
            return;
        }

        await _context.WorkItemAssignees.AddAsync(new WorkItemAssignee
        {
            WorkItemId = workItemId,
            UserId = userId,
            AssignedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkItem>> GetAssignedToUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.WorkItems
            .AsNoTracking()
            .Include(w => w.Board)
            .Include(w => w.Stage)
            .Include(w => w.Assignees)
            .Include(w => w.TimeEntries)
            .Where(w => w.ParentId == null && !w.IsArchived
                && (w.ResponsibleId == userId || w.Assignees.Any(a => a.UserId == userId)))
            .ToListAsync(cancellationToken);
    }

    public async Task UpdatePersonalPrioritiesAsync(string userId, IReadOnlyList<Guid> orderedWorkItemIds, CancellationToken cancellationToken = default)
    {
        var ids = orderedWorkItemIds.ToList();
        var rows = await _context.WorkItemAssignees
            .Where(a => a.UserId == userId && ids.Contains(a.WorkItemId))
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            row.PersonalPriority = ids.IndexOf(row.WorkItemId) + 1;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkItem>> GetExclusiveToBoardAsync(Guid boardId, CancellationToken cancellationToken = default)
    {
        // A tarefa pertence a um único quadro (D83), então todo item não arquivado
        // do quadro é exclusivo dele.
        return await _context.WorkItems
            .Where(w => !w.IsArchived && w.BoardId == boardId)
            .ToListAsync(cancellationToken);
    }

    public async Task RemoveAssigneeAsync(
        Guid workItemId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var assignee = await _context.WorkItemAssignees
            .FirstOrDefaultAsync(a => a.WorkItemId == workItemId && a.UserId == userId, cancellationToken);

        if (assignee is null)
        {
            return;
        }

        _context.WorkItemAssignees.Remove(assignee);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
