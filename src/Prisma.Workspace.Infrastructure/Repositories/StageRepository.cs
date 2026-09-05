using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório de Stage usando EF Core.
/// </summary>
public class StageRepository : IStageRepository
{
    private readonly AppDbContext _context;

    public StageRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Stage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Stages
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Stage>> GetByBoardIdAsync(Guid boardId, CancellationToken cancellationToken = default)
    {
        return await _context.Stages
            .Include(s => s.WorkflowStatus)
            .AsNoTracking()
            .Where(s => s.BoardId == boardId)
            .OrderBy(s => s.Position)
            .ToListAsync(cancellationToken);
    }

    public async Task<Stage> AddAsync(Stage stage, CancellationToken cancellationToken = default)
    {
        await _context.Stages.AddAsync(stage, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return stage;
    }

    public async Task UpdateAsync(Stage stage, CancellationToken cancellationToken = default)
    {
        _context.Stages.Update(stage);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Stage stage, CancellationToken cancellationToken = default)
    {
        _context.Stages.Remove(stage);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
