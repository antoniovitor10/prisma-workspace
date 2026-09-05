using MediatR;

namespace Detran.Kanban.Application.Features.Approvals.Commands;

/// <summary>Solicita aprovação de uma tarefa a um aprovador.</summary>
public record RequestApprovalCommand(
    Guid WorkItemId,
    string RequesterId,
    string ApproverId) : IRequest<Guid>;
