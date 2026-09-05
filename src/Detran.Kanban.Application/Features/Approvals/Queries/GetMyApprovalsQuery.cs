using Detran.Kanban.Application.Features.Approvals.Dtos;
using MediatR;

namespace Detran.Kanban.Application.Features.Approvals.Queries;

/// <summary>Aprovações onde o usuário é o aprovador (pendentes primeiro).</summary>
public record GetMyApprovalsQuery(string ApproverId) : IRequest<IReadOnlyList<ApprovalDto>>;
