using FluentValidation;

namespace Prisma.Workspace.Application.Features.Boards.Commands;

/// <summary>
/// Validador do comando CreateBoard.
/// </summary>
public class CreateBoardCommandValidator : AbstractValidator<CreateBoardCommand>
{
    public CreateBoardCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome do quadro é obrigatório.")
            .MaximumLength(200).WithMessage("O nome do quadro deve ter no máximo 200 caracteres.");

        RuleFor(x => x.OwnerId)
            .NotEmpty().WithMessage("O proprietário do quadro é obrigatório.");

        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("O projeto do quadro é obrigatório.");
    }
}
