using FluentValidation;

namespace Detran.Kanban.Application.Features.MeTime.Commands;

public class AddDayJustificationCommandValidator : AbstractValidator<AddDayJustificationCommand>
{
    public AddDayJustificationCommandValidator()
    {
        RuleFor(c => c.Reason).NotEmpty().WithMessage("Motivo obrigatório.");
        RuleFor(c => c.Hours).GreaterThan(0).WithMessage("Horas deve ser maior que zero.");
    }
}
