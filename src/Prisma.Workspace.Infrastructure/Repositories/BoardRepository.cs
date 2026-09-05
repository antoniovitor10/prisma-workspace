using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório de Board usando EF Core.
/// </summary>
public class BoardRepository : IBoardRepository
{
    private readonly AppDbContext _context;

    public BoardRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Board?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Boards
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Board>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Boards
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Board>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return await _context.Boards
            .AsNoTracking()
            .Include(b => b.Stages)
            .Where(b => b.ProjectId == projectId)
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Board> AddAsync(Board board, CancellationToken cancellationToken = default)
    {
        await _context.Boards.AddAsync(board, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return board;
    }

    public async Task UpdateAsync(Board board, CancellationToken cancellationToken = default)
    {
        _context.Boards.Update(board);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Board board, CancellationToken cancellationToken = default)
    {
        _context.Boards.Remove(board);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
