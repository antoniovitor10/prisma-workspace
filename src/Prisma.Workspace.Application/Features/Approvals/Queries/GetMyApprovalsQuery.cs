using Prisma.Workspace.Application.Features.Approvals.Dtos;
using MediatR;

namespace Prisma.Workspace.Application.Features.Approvals.Queries;

/// <summary>Aprovações onde o usuário é o aprovador (pendentes primeiro).</summary>
public record GetMyApprovalsQuery(string ApproverId) : IRequest<IReadOnlyList<ApprovalDto>>;
