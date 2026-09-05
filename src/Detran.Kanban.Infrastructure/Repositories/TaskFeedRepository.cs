using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Detran.Kanban.Infrastructure.Repositories;

/// <summary>
/// Implementacao EF Core do feed da tarefa (comentarios + eventos de sistema).
/// </summary>
public class TaskFeedRepository : ITaskFeedRepository
{
    private readonly AppDbContext _context;

    public TaskFeedRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Comment>> GetCommentsAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        return await _context.Comments
            .AsNoTracking()
            .Where(c => c.WorkItemId == workItemId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Comment> AddCommentAsync(Comment comment, CancellationToken cancellationToken = default)
    {
        await _context.Comments.AddAsync(comment, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return comment;
    }

    public async Task<IReadOnlyList<TaskEvent>> GetEventsAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        return await _context.TaskEvents
            .AsNoTracking()
            .Where(e => e.WorkItemId == workItemId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StageHistory>> GetStageHistoriesAsync(
        Guid workItemId,
        CancellationToken cancellationToken = default)
    {
        return await _context.StageHistories
            .AsNoTracking()
            .Include(history => history.Stage)
            .Where(history => history.WorkItemId == workItemId)
            .OrderBy(history => history.EnteredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddEventAsync(TaskEvent taskEvent, CancellationToken cancellationToken = default)
    {
        await _context.TaskEvents.AddAsync(taskEvent, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
