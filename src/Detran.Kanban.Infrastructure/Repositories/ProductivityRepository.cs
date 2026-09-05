using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Detran.Kanban.Infrastructure.Repositories;

public class ProductivityRepository : IProductivityRepository
{
    private readonly AppDbContext _context;
    public ProductivityRepository(AppDbContext context) => _context = context;
    public Task<Board?> GetBoardAsync(Guid boardId, CancellationToken ct = default)
        => _context.Boards.AsNoTracking().FirstOrDefaultAsync(x => x.Id == boardId, ct);
    public async Task<IReadOnlyList<SavedFilter>> GetFiltersAsync(Guid boardId, string userId, CancellationToken ct = default)
        => await _context.SavedFilters.AsNoTracking().Where(x => x.BoardId == boardId && x.UserId == userId)
            .OrderBy(x => x.Name).ToListAsync(ct);
    public async Task<IReadOnlyList<AutomationRule>> GetRulesAsync(Guid boardId, CancellationToken ct = default)
        => await _context.AutomationRules.AsNoTracking().Include(x => x.TriggerStage)
            .Where(x => x.BoardId == boardId).OrderBy(x => x.CreatedAt).ToListAsync(ct);
    public Task<AutomationRule?> GetRuleAsync(Guid id, CancellationToken ct = default)
        => _context.AutomationRules.Include(x => x.Board).FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<Stage?> GetStageAsync(Guid id, CancellationToken ct = default)
        => _context.Stages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<Sprint?> GetSprintAsync(Guid id, CancellationToken ct = default)
        => _context.Sprints.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> TagExistsAsync(Guid id, CancellationToken ct = default)
        => _context.Tags.AsNoTracking().AnyAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<WorkItem>> GetItemsAsync(Guid boardId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => await _context.WorkItems.Include(x => x.Assignees).Include(x => x.WorkItemTags)
            .Include(x => x.StageHistories).Include(x => x.Followers).Include(x => x.Board)
            .Where(x => x.BoardId == boardId && ids.Contains(x.Id)).ToListAsync(ct);
    public async Task AddFilterAsync(SavedFilter filter, CancellationToken ct = default) { _context.SavedFilters.Add(filter); await _context.SaveChangesAsync(ct); }
    public async Task DeleteFilterAsync(Guid boardId, Guid id, string userId, CancellationToken ct = default)
    { var filter = await _context.SavedFilters.FirstOrDefaultAsync(x => x.Id == id && x.BoardId == boardId && x.UserId == userId, ct); if (filter is not null) { _context.Remove(filter); await _context.SaveChangesAsync(ct); } }
    public async Task AddRuleAsync(AutomationRule rule, CancellationToken ct = default) { _context.AutomationRules.Add(rule); await _context.SaveChangesAsync(ct); }
    public async Task DeleteRuleAsync(AutomationRule rule, CancellationToken ct = default) { _context.Remove(rule); await _context.SaveChangesAsync(ct); }
    public void AddTaskEvents(IEnumerable<TaskEvent> events) => _context.TaskEvents.AddRange(events);
    public Task SaveAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
