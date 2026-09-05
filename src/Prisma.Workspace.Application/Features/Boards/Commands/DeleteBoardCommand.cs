using FluentValidation;
using MediatR;

namespace Prisma.Workspace.Application.Features.Boards.Commands;

/// <summary>
/// Exclui um quadro do projeto.
/// Itens com placements exclusivos neste quadro são realocados para DestinationBoardId.
/// </summary>
public record DeleteBoardCommand(
    Guid BoardId,
    string ActorId,
    /// <summary>
    /// Quadro de destino obrigatório quando há itens exclusivos.
    /// </summary>
    Guid? DestinationBoardId = null) : IRequest;

public class DeleteBoardCommandValidator : AbstractValidator<DeleteBoardCommand>
{
    public DeleteBoardCommandValidator()
    {
        RuleFor(x => x.BoardId).NotEmpty().WithMessage("Id do quadro é obrigatório.");
        RuleFor(x => x.ActorId).NotEmpty().WithMessage("Ator é obrigatório.");
    }
}
