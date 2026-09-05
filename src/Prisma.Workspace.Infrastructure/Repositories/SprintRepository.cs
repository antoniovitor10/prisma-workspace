using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
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

    public Task<bool> HasActiveAsync(Guid projectId, Guid? excludingId = null, CancellationToken cancellationToken = default)
        => _context.Sprints.AnyAsync(x => x.ProjectId == projectId && x.Status == SprintStatus.Active
            && (!excludingId.HasValue || x.Id != excludingId.Value), cancellationToken);

    public async Task AddAsync(Sprint sprint, CancellationToken cancellationToken = default)
    {
        await _context.Sprints.AddAsync(sprint, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
}
