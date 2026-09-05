using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly AppDbContext _context;
    public ProjectRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<Project>> GetForUserAsync(
        string userId,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
        => await _context.Projects.AsNoTracking()
            .Include(x => x.Boards).Include(x => x.Teams).ThenInclude(x => x.Team)
            .Include(x => x.Tags).ThenInclude(x => x.Tag)
            .Include(x => x.CustomFields)
            .Where(x => (includeArchived || !x.IsArchived) && (x.OwnerId == userId
                || x.Members.Any(m => m.UserId == userId)
                || _context.OrganizationMembers.Any(m => m.UserId == userId && m.IsActive
                    && (m.Role == OrganizationRole.Administrator
                        || m.Role == OrganizationRole.Manager
                        || m.Role == OrganizationRole.ProjectManager))))
            .OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.Projects.Include(x => x.Boards).ThenInclude(x => x.Stages)
            .Include(x => x.WorkflowStatuses).Include(x => x.WorkflowTemplate)
            .Include(x => x.Teams).ThenInclude(x => x.Team)
            .Include(x => x.Tags).ThenInclude(x => x.Tag)
            .Include(x => x.CustomFields)
            .Include(x => x.Events)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Project?> GetByIdWithMembersAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.Projects.Include(x => x.Members).Include(x => x.Boards).ThenInclude(x => x.Stages)
            .Include(x => x.WorkflowStatuses).Include(x => x.WorkflowTemplate)
            .Include(x => x.Teams).ThenInclude(x => x.Team)
            .Include(x => x.Tags).ThenInclude(x => x.Tag)
            .Include(x => x.CustomFields)
            .Include(x => x.Events)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken = default)
        => _context.Projects.AnyAsync(x => x.Key == key, cancellationToken);

    public async Task AddAsync(Project project, CancellationToken cancellationToken = default)
    {
        await _context.Projects.AddAsync(project, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public void AddCustomField(ProjectCustomFieldDefinition field)
        => _context.ProjectCustomFields.Add(field);

    public void AddEvent(ProjectEvent projectEvent)
        => _context.ProjectEvents.Add(projectEvent);

    public Task SaveAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
}
