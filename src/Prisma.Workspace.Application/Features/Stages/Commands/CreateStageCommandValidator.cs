using FluentValidation;

namespace Prisma.Workspace.Application.Features.Stages.Commands;

/// <summary>
/// Validador para criação de etapas (Stage).
/// </summary>
public class CreateStageCommandValidator : AbstractValidator<CreateStageCommand>
{
    public CreateStageCommandValidator()
    {
        RuleFor(s => s.BoardId).NotEmpty().WithMessage("O quadro da coluna é obrigatório.");
        RuleFor(s => s.Category).IsInEnum();
        RuleFor(s => s.ProjectId)
            .NotEmpty().WithMessage("O identificador do projeto é obrigatório.");

        RuleFor(s => s.Name)
            .NotEmpty().WithMessage("O nome da etapa é obrigatório.")
            .MaximumLength(200).WithMessage("O nome da etapa deve ter no máximo 200 caracteres.");

        RuleFor(s => s.Position)
            .GreaterThanOrEqualTo(0).WithMessage("A posição da etapa deve ser um valor maior ou igual a zero.");
    }
}
