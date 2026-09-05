using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Application.Interfaces;

public interface IOrganizationWorkflowRepository
{
    Task<IReadOnlyList<OrganizationWorkflowTemplate>> GetTemplatesAsync(Guid organizationId, CancellationToken ct = default);
    Task<OrganizationWorkflowTemplate?> GetTemplateAsync(Guid organizationId, Guid templateId, bool tracking = false, CancellationToken ct = default);
    Task<OrganizationWorkflowTemplate?> GetDefaultAsync(CancellationToken ct = default);
    Task<OrganizationWorkflowTemplate?> GetCurrentTemplateAsync(Guid templateId, CancellationToken ct = default);
    Task<Project?> GetProjectGraphAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationWorkflowTemplate>> GetOtherDefaultsAsync(Guid organizationId, Guid? excludingId, CancellationToken ct = default);
    void Add(OrganizationWorkflowTemplate template);
    void RemoveTransitions(IEnumerable<OrganizationWorkflowTransition> transitions);
    void AddTransitions(IEnumerable<OrganizationWorkflowTransition> transitions);
    void RemoveProjectTransitions(IEnumerable<WorkflowTransition> transitions);
    void AddProjectTransitions(IEnumerable<WorkflowTransition> transitions);
    void AddProjectStatus(WorkflowStatus status);
    Task SaveAsync(CancellationToken ct = default);
}
