using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Detran.Kanban.Infrastructure.Repositories;

public class SlaRepository : ISlaRepository
{
    private readonly AppDbContext _context;
    public SlaRepository(AppDbContext context) => _context = context;

    public Task<ProjectSlaPolicy?> GetByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => _context.ProjectSlaPolicies
            .FirstOrDefaultAsync(x => x.ProjectId == projectId, cancellationToken);

    public void Add(ProjectSlaPolicy policy) => _context.ProjectSlaPolicies.Add(policy);

    public Task SaveAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
