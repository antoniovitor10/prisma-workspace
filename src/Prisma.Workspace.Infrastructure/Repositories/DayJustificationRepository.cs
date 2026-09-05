using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório de justificativas de dia usando EF Core.
/// </summary>
public class DayJustificationRepository : IDayJustificationRepository
{
    private readonly AppDbContext _context;

    public DayJustificationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<DayJustification>> GetByUserAndDateAsync(
        string userId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        return await _context.DayJustifications
            .AsNoTracking()
            .Where(d => d.UserId == userId && d.Date == date)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<DayJustification?> GetByIdForUserAsync(
        Guid id,
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DayJustifications
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(DayJustification justification, CancellationToken cancellationToken = default)
    {
        await _context.DayJustifications.AddAsync(justification, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(DayJustification justification, CancellationToken cancellationToken = default)
    {
        _context.DayJustifications.Remove(justification);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
