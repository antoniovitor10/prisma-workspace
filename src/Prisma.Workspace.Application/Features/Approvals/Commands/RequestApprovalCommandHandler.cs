using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Domain.Enums;
using MediatR;
using System.Text.Json;

namespace Prisma.Workspace.Application.Features.Approvals.Commands;

/// <summary>
/// Cria a solicitação de aprovação e registra o evento no feed da tarefa.
/// Regra: uma tarefa só pode ter UMA aprovação pendente por vez.
/// </summary>
public class RequestApprovalCommandHandler : IRequestHandler<RequestApprovalCommand, Guid>
{
    private readonly IApprovalRepository _approvals;
    private readonly IWorkItemRepository _workItems;
    private readonly ITaskFeedRepository _feed;
    private readonly IWorkItemAccessService _access;
    private readonly IUserDirectory _users;

    public RequestApprovalCommandHandler(
        IApprovalRepository approvals,
        IWorkItemRepository workItems,
        ITaskFeedRepository feed,
        IWorkItemAccessService access,
        IUserDirectory users)
    {
        _approvals = approvals;
        _workItems = workItems;
        _feed = feed;
        _access = access;
        _users = users;
    }

    public async Task<Guid> Handle(RequestApprovalCommand request, CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(request.WorkItemId, request.RequesterId,
            PlatformPermission.Edit, ProjectRole.Member, cancellationToken);
        var workItem = await _workItems.GetByIdAsync(request.WorkItemId, cancellationToken)
            ?? throw new NaoEncontradoException("Tarefa");

        DomainException.Garantir(await _users.GetByIdAsync(request.ApproverId, cancellationToken) is not null,
            "O aprovador deve ser um membro ativo da organização.");
        await _access.EnsureAsync(request.WorkItemId, request.ApproverId,
            PlatformPermission.View, ProjectRole.Viewer, cancellationToken);

        DomainException.Garantir(
            !await _approvals.HasPendingAsync(request.WorkItemId, cancellationToken),
            "Já existe uma aprovação pendente para esta tarefa.");

        var approval = Approval.Solicitar(workItem.Id, request.RequesterId, request.ApproverId);
        await _approvals.AddAsync(approval, cancellationToken);

        await _feed.AddEventAsync(TaskEvent.Registrar(
            workItem.Id,
            request.RequesterId,
            "approval_requested",
            JsonSerializer.Serialize(new { approverId = request.ApproverId })), cancellationToken);

        return approval.Id;
    }
}
