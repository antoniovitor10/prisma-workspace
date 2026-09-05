using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Detran.Kanban.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório de detalhes da tarefa usando EF Core.
/// </summary>
public class WorkItemDetailsRepository : IWorkItemDetailsRepository
{
    private readonly AppDbContext _context;

    public WorkItemDetailsRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<WorkItem?> GetWithTagsAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        return await _context.WorkItems
            .Include(w => w.WorkItemTags)
            .FirstOrDefaultAsync(w => w.Id == workItemId, cancellationToken);
    }

    public async Task<WorkItem?> GetAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        return await _context.WorkItems
            .FirstOrDefaultAsync(w => w.Id == workItemId, cancellationToken);
    }

    public Task<bool> WorkItemExistsAsync(Guid workItemId, CancellationToken cancellationToken = default)
        => _context.WorkItems.AnyAsync(w => w.Id == workItemId, cancellationToken);

    public async Task<IReadOnlyList<ChecklistItem>> GetChecklistAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        return await _context.ChecklistItems
            .AsNoTracking()
            .Where(c => c.WorkItemId == workItemId)
            .OrderBy(c => c.Position)
            .ToListAsync(cancellationToken);
    }

    public async Task<ChecklistItem?> GetChecklistItemAsync(Guid workItemId, Guid itemId, CancellationToken cancellationToken = default)
    {
        return await _context.ChecklistItems
            .FirstOrDefaultAsync(c => c.Id == itemId && c.WorkItemId == workItemId, cancellationToken);
    }

    public async Task<double> GetMaxChecklistPositionAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        return await _context.ChecklistItems
            .Where(c => c.WorkItemId == workItemId)
            .Select(c => (double?)c.Position)
            .MaxAsync(cancellationToken) ?? 0;
    }

    public async Task AddChecklistItemAsync(ChecklistItem item, CancellationToken cancellationToken = default)
    {
        await _context.ChecklistItems.AddAsync(item, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteChecklistItemAsync(ChecklistItem item, CancellationToken cancellationToken = default)
    {
        _context.ChecklistItems.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
