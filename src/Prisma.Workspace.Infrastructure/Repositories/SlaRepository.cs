using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Repositories;

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
