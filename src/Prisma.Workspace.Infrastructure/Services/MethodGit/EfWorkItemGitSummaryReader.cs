using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.WorkItems.Dtos;
using Prisma.Workspace.Application.Features.WorkItems.Queries;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Infrastructure.Persistence;

namespace Prisma.Workspace.Infrastructure.Services.MethodGit;

/// <summary>Uma consulta por quadro para os selos de Git dos cartões; só vínculos de integrações da organização atual.</summary>
public sealed class EfWorkItemGitSummaryReader(AppDbContext db, IOrganizationContext organization) : IWorkItemGitSummaryReader
{
    public async Task<IReadOnlyDictionary<Guid, WorkItemGitSummaryDto>> GetAsync(IReadOnlyCollection<Guid> workItemIds,
        CancellationToken cancellationToken = default)
    {
        if (workItemIds.Count == 0 || organization.OrganizationId is not { } tenant) return new Dictionary<Guid, WorkItemGitSummaryDto>();
        var ids = workItemIds.Distinct().ToArray();
        var rows = await db.Set<TaskGitLink>().AsNoTracking()
            .Where(x => ids.Contains(x.WorkItemId) && x.Integration.OrganizationId == tenant)
            .Select(x => new WorkItemGitLinkRow(x.WorkItemId, x.IntegrationId, x.Integration.RepositoryFullName, x.BranchName, x.BranchState,
                x.CreatedAtUtc,
                x.Observation == null ? null : x.Observation.PullRequestNumber,
                x.Observation == null ? null : x.Observation.Url,
                x.Observation == null ? null : x.Observation.HeadBranch,
                x.Observation == null ? null : x.Observation.State,
                x.Observation != null && x.Observation.IsDraft,
                // A observação só guarda MergeSha de PR mergeado (UpsertObservationAsync).
                x.Observation != null && x.Observation.State == "closed" && x.Observation.MergeSha != null,
                x.Observation == null ? 0 : x.Observation.ApprovalsCount,
                x.Observation != null && x.Observation.ChangesRequested,
                x.Observation == null ? null : x.Observation.ProviderUpdatedAtUtc,
                x.Id, x.Integration.Mode, x.Integration.DefaultBranch,
                db.Set<MethodTaskState>().Where(state => state.WorkItemId == x.WorkItemId).Select(state => state.MethodTaskId).FirstOrDefault()))
            .ToListAsync(cancellationToken);
        return WorkItemGitSummary.Summarize(rows);
    }
}
