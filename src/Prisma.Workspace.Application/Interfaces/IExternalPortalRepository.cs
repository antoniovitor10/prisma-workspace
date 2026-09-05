using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Application.Interfaces;

public interface IExternalPortalRepository
{
    Task<ExternalPortal?> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<ExternalPortal?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<bool> SlugExistsAsync(string slug, Guid? excludingPortalId = null, CancellationToken cancellationToken = default);
    Task<ExternalForm?> GetFormAsync(Guid projectId, Guid formId, CancellationToken cancellationToken = default);
    Task<ExternalForm?> GetPublicFormAsync(string portalSlug, string formSlug,
        CancellationToken cancellationToken = default);
    Task<bool> FormSlugExistsAsync(Guid portalId, string slug, Guid? excludingFormId = null,
        CancellationToken cancellationToken = default);
    void AddPortal(ExternalPortal portal);
    void AddForm(ExternalForm form);
    Task<decimal> GetNextBacklogRankAsync(Guid boardId, CancellationToken cancellationToken = default);
    Task<ExternalRequest> CreateRequestAsync(WorkItem workItem, ExternalRequest externalRequest,
        CancellationToken cancellationToken = default);
    Task<ExternalRequest?> GetRequestByProtocolAsync(string protocol, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExternalRequest>> GetRequestsAsync(CancellationToken cancellationToken = default);
    Task<ExternalPortalInvitation?> GetInvitationAsync(Guid portalId, string email, string tokenHash,
        CancellationToken cancellationToken = default);
    Task<ExternalPortalVerification?> GetVerificationAsync(Guid portalId, string email, string codeHash,
        CancellationToken cancellationToken = default);
    void AddInvitation(ExternalPortalInvitation invitation);
    void AddVerification(ExternalPortalVerification verification);
    void AddMessage(ExternalRequestMessage message);
    void AddAttachment(Attachment attachment);
    void AddTriageEvent(ExternalRequestTriageEvent triageEvent);
    void AddTaskEvent(TaskEvent taskEvent);
    Task<WorkItem?> GetWorkItemAsync(Guid workItemId, CancellationToken cancellationToken = default);
    Task<WorkItem?> GetWorkItemByNumberAsync(long number, CancellationToken cancellationToken = default);
    Task<bool> WorkItemLinkExistsAsync(Guid sourceWorkItemId, Guid targetWorkItemId,
        Prisma.Workspace.Domain.Enums.WorkItemLinkType type, CancellationToken cancellationToken = default);
    void AddWorkItemLink(WorkItemLink link);
    Task<Attachment?> GetVisibleAttachmentAsync(Guid externalRequestId, Guid attachmentId,
        CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}

public interface IPortalEmailSender
{
    bool CanExposeLocalCode { get; }
    Task<bool> SendVerificationCodeAsync(string email, string portalName, string code,
        CancellationToken cancellationToken = default);
    Task<bool> SendRequestConfirmationAsync(
        string email, string portalName, string protocol, string accessKey,
        string trackingPath, string? confirmationMessage,
        CancellationToken cancellationToken = default);
    Task<bool> SendInformationRequestedAsync(
        string email, string portalName, string protocol, string message, string trackingPath,
        CancellationToken cancellationToken = default);
    Task<bool> SendPublicReplyAsync(
        string email, string portalName, string protocol, string message, string trackingPath,
        CancellationToken cancellationToken = default);
}
