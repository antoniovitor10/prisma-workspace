using FluentValidation;

namespace Prisma.Workspace.Application.Features.Stages.Commands;

public class UpdateStageCommandValidator : AbstractValidator<UpdateStageCommand>
{
    public UpdateStageCommandValidator()
    {
        RuleFor(x => x.StageId)
            .NotEmpty().WithMessage("O identificador da etapa é obrigatório.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome da etapa é obrigatório.")
            .MaximumLength(100).WithMessage("O nome da etapa deve ter no máximo 100 caracteres.");
    }
}
