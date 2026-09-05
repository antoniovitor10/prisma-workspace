using Detran.Kanban.Application.Features.Approvals.Dtos;
using MediatR;

namespace Detran.Kanban.Application.Features.Approvals.Queries;

/// <summary>Aprovações de uma tarefa (mais recente primeiro).</summary>
public record GetApprovalsByWorkItemQuery(Guid WorkItemId, string ActorId)
    : IRequest<IReadOnlyList<ApprovalDto>>;
