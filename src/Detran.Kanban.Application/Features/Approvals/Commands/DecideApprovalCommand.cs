using MediatR;

namespace Detran.Kanban.Application.Features.Approvals.Commands;

/// <summary>
/// Decide uma aprovação pendente do usuário logado.
/// Aprovar = true aprova; false rejeita.
/// </summary>
public record DecideApprovalCommand(
    Guid ApprovalId,
    string ApproverId,
    bool Aprovar,
    string? Note) : IRequest;
