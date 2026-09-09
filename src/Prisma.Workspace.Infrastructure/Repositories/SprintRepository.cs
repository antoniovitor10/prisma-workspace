using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Repositories;

public class SprintRepository : ISprintRepository
{
    private readonly AppDbContext _context;
    public SprintRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<Sprint>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await _context.Sprints.AsNoTracking().AsSplitQuery().Include(x => x.Team).Include(x => x.Capacities)
            .Include(x => x.WorkItems).Include(x => x.ItemSnapshots).Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.StartDate).ToListAsync(cancellationToken);

    public Task<Sprint?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.Sprints.Include(x => x.Project).Include(x => x.Team).ThenInclude(x => x.Members)
            .Include(x => x.Capacities).Include(x => x.WorkItems).Include(x => x.ItemSnapshots)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task RemoveWithUnlinkAsync(Sprint sprint, CancellationToken cancellationToken = default)
    {
        // Desvincular e excluir precisam ser atômicos: falha em qualquer etapa não pode
        // deixar tarefa órfã apontando para sprint inexistente.
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        foreach (var item in sprint.WorkItems) item.SprintId = null;
        await _context.SaveChangesAsync(cancellationToken);

        _context.Sprints.Remove(sprint);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AddAsync(Sprint sprint, CancellationToken cancellationToken = default)
    {
        await _context.Sprints.AddAsync(sprint, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
}
