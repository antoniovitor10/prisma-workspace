using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using MediatR;
using System.Text.Json;

namespace Prisma.Workspace.Application.Features.Approvals.Commands;

/// <summary>
/// Aprova ou rejeita uma solicitação pendente. A regra "só decide quem é o
/// aprovador" está na busca; a regra "só decide uma vez" está na entidade.
/// </summary>
public class DecideApprovalCommandHandler : IRequestHandler<DecideApprovalCommand>
{
    private readonly IApprovalRepository _approvals;
    private readonly ITaskFeedRepository _feed;
    private readonly IWorkItemAccessService _access;

    public DecideApprovalCommandHandler(
        IApprovalRepository approvals,
        ITaskFeedRepository feed,
        IWorkItemAccessService access)
    {
        _approvals = approvals;
        _feed = feed;
        _access = access;
    }

    public async Task Handle(DecideApprovalCommand request, CancellationToken cancellationToken)
    {
        var approval = await _approvals.GetByIdForApproverAsync(request.ApprovalId, request.ApproverId, cancellationToken)
            ?? throw new NaoEncontradoException("Aprovação");

        await _access.EnsureAsync(approval.WorkItemId, request.ApproverId,
            PlatformPermission.Edit, ProjectRole.Viewer, cancellationToken);

        if (request.Aprovar) approval.Aprovar(request.Note);
        else approval.Rejeitar(request.Note);

        await _feed.AddEventAsync(TaskEvent.Registrar(
            approval.WorkItemId,
            request.ApproverId,
            request.Aprovar ? "approval_approved" : "approval_rejected",
            approval.Note is null ? null : JsonSerializer.Serialize(new { note = approval.Note })), cancellationToken);

        await _approvals.SaveAsync(cancellationToken);
    }
}
