using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Services;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Repositories;

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
        // Lead time conta os mesmos cards que o quadro mostra (regra única de visibilidade).
        var cards = _context.WorkItems
            .Where(w => w.BoardId == boardId)
            .Where(WorkItemBoardVisibility.IsBoardCardExpression)
            .Select(w => w.Id);

        return await _context.StageHistories
            .AsNoTracking()
            .Include(h => h.Stage)
            .Where(h => h.WorkItem.BoardId == boardId && cards.Contains(h.WorkItemId))
            .ToListAsync(cancellationToken);
    }
}
