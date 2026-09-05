using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Detran.Kanban.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório de StageHistory usando EF Core.
/// </summary>
public class StageHistoryRepository : IStageHistoryRepository
{
    private readonly AppDbContext _context;

    public StageHistoryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<StageHistory>> GetByBoardIdAsync(Guid boardId, CancellationToken cancellationToken = default)
    {
        return await _context.StageHistories
            .AsNoTracking()
            .Include(h => h.Stage)
            .Where(h => h.WorkItem.BoardId == boardId)
            .ToListAsync(cancellationToken);
    }
}
