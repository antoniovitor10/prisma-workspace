using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Detran.Kanban.Infrastructure.Repositories;

public sealed class OrganizationWorkflowRepository : IOrganizationWorkflowRepository
{
    private readonly AppDbContext _context;
    public OrganizationWorkflowRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<OrganizationWorkflowTemplate>> GetTemplatesAsync(
        Guid organizationId, CancellationToken ct = default)
        => await _context.OrganizationWorkflowTemplates.AsNoTracking()
            .Include(x => x.Statuses).Include(x => x.Transitions)
            .Where(x => x.OrganizationId == organizationId && x.IsActive)
            .OrderByDescending(x => x.IsDefault).ThenBy(x => x.Name).ToListAsync(ct);

    public async Task<OrganizationWorkflowTemplate?> GetTemplateAsync(
        Guid organizationId, Guid templateId, bool tracking = false, CancellationToken ct = default)
    {
        var query = _context.OrganizationWorkflowTemplates
            .Include(x => x.Statuses)
            .Include(x => x.Transitions)
            .Include(x => x.Projects.Where(p => p.WorkflowInheritanceMode == WorkflowInheritanceMode.Inherited))
                .ThenInclude(x => x.WorkflowStatuses).ThenInclude(x => x.OutgoingTransitions)
            .Where(x => x.OrganizationId == organizationId && x.Id == templateId);
        if (!tracking) query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(ct);
    }

    public Task<OrganizationWorkflowTemplate?> GetDefaultAsync(CancellationToken ct = default)
        => _context.OrganizationWorkflowTemplates.AsNoTracking()
            .Include(x => x.Statuses).Include(x => x.Transitions)
            .FirstOrDefaultAsync(x => x.IsDefault && x.IsActive, ct);

    public Task<OrganizationWorkflowTemplate?> GetCurrentTemplateAsync(Guid templateId, CancellationToken ct = default)
        => _context.OrganizationWorkflowTemplates.AsNoTracking()
            .Include(x => x.Statuses).Include(x => x.Transitions)
            .FirstOrDefaultAsync(x => x.Id == templateId && x.IsActive, ct);

    public Task<Project?> GetProjectGraphAsync(Guid projectId, CancellationToken ct = default)
        => _context.Projects
            .Include(x => x.WorkflowTemplate).ThenInclude(x => x!.Statuses)
            .Include(x => x.WorkflowTemplate).ThenInclude(x => x!.Transitions)
            .Include(x => x.WorkflowStatuses).ThenInclude(x => x.OutgoingTransitions)
            .FirstOrDefaultAsync(x => x.Id == projectId, ct);

    public async Task<IReadOnlyList<OrganizationWorkflowTemplate>> GetOtherDefaultsAsync(
        Guid organizationId, Guid? excludingId, CancellationToken ct = default)
        => await _context.OrganizationWorkflowTemplates
            .Where(x => x.OrganizationId == organizationId && x.IsDefault
                && (!excludingId.HasValue || x.Id != excludingId.Value)).ToListAsync(ct);

    public void Add(OrganizationWorkflowTemplate template) => _context.OrganizationWorkflowTemplates.Add(template);
    public void RemoveTransitions(IEnumerable<OrganizationWorkflowTransition> transitions)
        => _context.OrganizationWorkflowTransitions.RemoveRange(transitions);
    public void AddTransitions(IEnumerable<OrganizationWorkflowTransition> transitions)
        => _context.OrganizationWorkflowTransitions.AddRange(transitions);
    public void RemoveProjectTransitions(IEnumerable<WorkflowTransition> transitions)
        => _context.WorkflowTransitions.RemoveRange(transitions);
    public void AddProjectTransitions(IEnumerable<WorkflowTransition> transitions)
        => _context.WorkflowTransitions.AddRange(transitions);
    public void AddProjectStatus(WorkflowStatus status) => _context.WorkflowStatuses.Add(status);
    public Task SaveAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
