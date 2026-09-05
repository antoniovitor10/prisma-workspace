using FluentValidation;

namespace Detran.Kanban.Application.Features.TimeEntries.Commands;

public class CreateManualTimeEntryCommandValidator : AbstractValidator<CreateManualTimeEntryCommand>
{
    public CreateManualTimeEntryCommandValidator()
    {
        RuleFor(c => c.WorkItemId).NotEmpty();
        RuleFor(c => c.EndedAt)
            .GreaterThan(c => c.StartedAt)
            .WithMessage("O fim deve ser maior que o início.");
    }
}
