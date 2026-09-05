using FluentValidation;

namespace Detran.Kanban.Application.Features.Approvals.Commands;

public class RequestApprovalCommandValidator : AbstractValidator<RequestApprovalCommand>
{
    public RequestApprovalCommandValidator()
    {
        RuleFor(c => c.WorkItemId).NotEmpty();
        RuleFor(c => c.ApproverId).NotEmpty().WithMessage("Aprovador obrigatório.");
    }
}
