using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Repositories;

public class ExternalPortalRepository : IExternalPortalRepository
{
    private readonly AppDbContext _context;
    public ExternalPortalRepository(AppDbContext context) => _context = context;

    public Task<ExternalPortal?> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => _context.ExternalPortals.Include(x => x.Project)
            .Include(x => x.Board)
            .Include(x => x.Project).ThenInclude(x => x.Stages).Include(x => x.Forms)
            .FirstOrDefaultAsync(x => x.ProjectId == projectId, cancellationToken);

    public Task<ExternalPortal?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
        => _context.ExternalPortals.IgnoreQueryFilters().AsSplitQuery()
            .Include(x => x.Board)
            .Include(x => x.Project).ThenInclude(x => x.WorkflowStatuses)
            .Include(x => x.Project).ThenInclude(x => x.Stages)
            .Include(x => x.Forms)
            .FirstOrDefaultAsync(x => x.PublicSlug == slug && x.IsEnabled
                && !x.Project.IsArchived
                && _context.Organizations.IgnoreQueryFilters().Any(organization =>
                    organization.Id == x.OrganizationId && organization.IsActive), cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, Guid? excludingPortalId = null,
        CancellationToken cancellationToken = default)
        => _context.ExternalPortals.IgnoreQueryFilters().AnyAsync(
            x => x.PublicSlug == slug && (!excludingPortalId.HasValue || x.Id != excludingPortalId.Value),
            cancellationToken);

    public Task<ExternalForm?> GetFormAsync(
        Guid projectId, Guid formId, CancellationToken cancellationToken = default)
        => _context.ExternalForms.AsSplitQuery()
            .Include(x => x.ExternalPortal).ThenInclude(x => x.Board)
            .Include(x => x.ExternalPortal).ThenInclude(x => x.Project)
            .Include(x => x.ExternalPortal).ThenInclude(x => x.Project).ThenInclude(x => x.Stages)
            .FirstOrDefaultAsync(x => x.Id == formId && x.ExternalPortal.ProjectId == projectId,
                cancellationToken);

    public Task<ExternalForm?> GetPublicFormAsync(
        string portalSlug, string formSlug, CancellationToken cancellationToken = default)
        => _context.ExternalForms.IgnoreQueryFilters().AsSplitQuery()
            .Include(x => x.ExternalPortal).ThenInclude(x => x.Board)
            .Include(x => x.ExternalPortal).ThenInclude(x => x.Project).ThenInclude(x => x.WorkflowStatuses)
            .Include(x => x.ExternalPortal).ThenInclude(x => x.Project).ThenInclude(x => x.Teams)
            .Include(x => x.ExternalPortal).ThenInclude(x => x.Project).ThenInclude(x => x.Members)
            .Include(x => x.ExternalPortal).ThenInclude(x => x.Project).ThenInclude(x => x.CustomFields)
            .Include(x => x.ExternalPortal).ThenInclude(x => x.Project).ThenInclude(x => x.Stages)
            .FirstOrDefaultAsync(x => x.PublicSlug == formSlug && x.IsEnabled
                && x.ExternalPortal.PublicSlug == portalSlug && x.ExternalPortal.IsEnabled
                && !x.ExternalPortal.Project.IsArchived
                && _context.Organizations.IgnoreQueryFilters().Any(organization =>
                    organization.Id == x.ExternalPortal.OrganizationId && organization.IsActive),
                cancellationToken);

    public Task<bool> FormSlugExistsAsync(
        Guid portalId, string slug, Guid? excludingFormId = null,
        CancellationToken cancellationToken = default)
        => _context.ExternalForms.AnyAsync(x => x.ExternalPortalId == portalId && x.PublicSlug == slug
            && (!excludingFormId.HasValue || x.Id != excludingFormId.Value), cancellationToken);

    public void AddPortal(ExternalPortal portal) => _context.ExternalPortals.Add(portal);
    public void AddForm(ExternalForm form) => _context.ExternalForms.Add(form);

    public async Task<decimal> GetNextBacklogRankAsync(Guid boardId, CancellationToken cancellationToken = default)
    {
        var maximum = await _context.WorkItems.IgnoreQueryFilters()
            .Where(x => x.BoardId == boardId)
            .Select(x => (decimal?)x.BacklogRank)
            .MaxAsync(cancellationToken) ?? 0;
        return maximum + 1000m;
    }

    public async Task<ExternalRequest> CreateRequestAsync(
        WorkItem workItem,
        ExternalRequest externalRequest,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.WorkItems.AddAsync(workItem, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        externalRequest.WorkItemId = workItem.Id;
        externalRequest.Protocol = $"{DateTimeOffset.UtcNow:yyyy}-{workItem.Number:D6}";
        await _context.ExternalRequests.AddAsync(externalRequest, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return externalRequest;
    }

    public Task<ExternalRequest?> GetRequestByProtocolAsync(string protocol, CancellationToken cancellationToken = default)
        => RequestQuery(ignoreFilters: true)
            .FirstOrDefaultAsync(x => x.Protocol == protocol, cancellationToken);

    public async Task<IReadOnlyList<ExternalRequest>> GetRequestsAsync(CancellationToken cancellationToken = default)
        => await RequestQuery(ignoreFilters: false)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);

    public Task<ExternalPortalInvitation?> GetInvitationAsync(
        Guid portalId, string email, string tokenHash, CancellationToken cancellationToken = default)
        => _context.ExternalPortalInvitations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.ExternalPortalId == portalId && x.Email == email
                && x.TokenHash == tokenHash && x.UsedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow,
                cancellationToken);

    public Task<ExternalPortalVerification?> GetVerificationAsync(
        Guid portalId, string email, string codeHash, CancellationToken cancellationToken = default)
        => _context.ExternalPortalVerifications.IgnoreQueryFilters()
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.ExternalPortalId == portalId && x.Email == email
                && x.CodeHash == codeHash && x.UsedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow,
                cancellationToken);

    public void AddInvitation(ExternalPortalInvitation invitation)
        => _context.ExternalPortalInvitations.Add(invitation);

    public void AddVerification(ExternalPortalVerification verification)
        => _context.ExternalPortalVerifications.Add(verification);

    public void AddMessage(ExternalRequestMessage message)
        => _context.ExternalRequestMessages.Add(message);

    public void AddAttachment(Attachment attachment) => _context.Attachments.Add(attachment);
    public void AddTriageEvent(ExternalRequestTriageEvent triageEvent)
        => _context.ExternalRequestTriageEvents.Add(triageEvent);
    public void AddTaskEvent(TaskEvent taskEvent) => _context.TaskEvents.Add(taskEvent);

    public Task<WorkItem?> GetWorkItemAsync(Guid workItemId, CancellationToken cancellationToken = default)
        => _context.WorkItems.AsSplitQuery()
            .Include(x => x.Board).ThenInclude(x => x.Project).ThenInclude(x => x!.WorkflowStatuses)
            .Include(x => x.Board).ThenInclude(x => x.Project).ThenInclude(x => x!.Stages)
            .FirstOrDefaultAsync(x => x.Id == workItemId, cancellationToken);

    public Task<WorkItem?> GetWorkItemByNumberAsync(long number, CancellationToken cancellationToken = default)
        => _context.WorkItems.AsSplitQuery()
            .Include(x => x.Board).ThenInclude(x => x.Project)
            .FirstOrDefaultAsync(x => x.Number == number, cancellationToken);

    public Task<bool> WorkItemLinkExistsAsync(
        Guid sourceWorkItemId, Guid targetWorkItemId,
        Prisma.Workspace.Domain.Enums.WorkItemLinkType type,
        CancellationToken cancellationToken = default)
        => _context.WorkItemLinks.AnyAsync(x => x.SourceWorkItemId == sourceWorkItemId
            && x.TargetWorkItemId == targetWorkItemId && x.Type == type, cancellationToken);

    public void AddWorkItemLink(WorkItemLink link) => _context.WorkItemLinks.Add(link);

    public Task<Attachment?> GetVisibleAttachmentAsync(
        Guid externalRequestId, Guid attachmentId, CancellationToken cancellationToken = default)
        => _context.Attachments.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == attachmentId && x.IsExternalVisible
                && x.WorkItem.ExternalRequest != null
                && x.WorkItem.ExternalRequest.Id == externalRequestId,
                cancellationToken);

    public Task SaveAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    private IQueryable<ExternalRequest> RequestQuery(bool ignoreFilters)
    {
        IQueryable<ExternalRequest> query = _context.ExternalRequests;
        if (ignoreFilters) query = query.IgnoreQueryFilters();
        return query.AsSplitQuery()
            .Include(x => x.ExternalPortal).ThenInclude(x => x.Project)
            .Include(x => x.ExternalForm)
            .Include(x => x.WorkItem).ThenInclude(x => x.Board).ThenInclude(x => x.Project).ThenInclude(x => x!.WorkflowStatuses)
            .Include(x => x.WorkItem).ThenInclude(x => x.Board).ThenInclude(x => x.Project).ThenInclude(x => x!.Stages)
            .Include(x => x.WorkItem).ThenInclude(x => x.Stage)
            .Include(x => x.WorkItem).ThenInclude(x => x.WorkflowStatus)
            .Include(x => x.WorkItem).ThenInclude(x => x.Attachments)
            .Include(x => x.WorkItem).ThenInclude(x => x.Assignees)
            .Include(x => x.WorkItem).ThenInclude(x => x.Followers)
            .Include(x => x.Messages)
            .Include(x => x.TriageEvents);
    }
}
