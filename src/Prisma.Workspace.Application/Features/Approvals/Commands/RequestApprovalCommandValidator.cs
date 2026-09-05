using FluentValidation;

namespace Prisma.Workspace.Application.Features.Approvals.Commands;

public class RequestApprovalCommandValidator : AbstractValidator<RequestApprovalCommand>
{
    public RequestApprovalCommandValidator()
    {
        RuleFor(c => c.WorkItemId).NotEmpty();
        RuleFor(c => c.ApproverId).NotEmpty().WithMessage("Aprovador obrigatório.");
    }
}
