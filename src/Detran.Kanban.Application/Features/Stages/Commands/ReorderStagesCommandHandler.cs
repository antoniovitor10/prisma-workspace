using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Domain.Exceptions;
using MediatR;

namespace Detran.Kanban.Application.Features.Stages.Commands;

/// <summary>
/// Reordena etapas de um quadro. Aceita lista parcial ou completa de IDs.
/// </summary>
public class ReorderStagesCommandHandler : IRequestHandler<ReorderStagesCommand>
{
    private readonly IStageRepository _stageRepository;
    private readonly IBoardRepository _boardRepository;
    private readonly IProjectAccessService? _access;

    public ReorderStagesCommandHandler(
        IStageRepository stageRepository,
        IBoardRepository boardRepository,
        IProjectAccessService? access = null)
    {
        _stageRepository = stageRepository;
        _boardRepository = boardRepository;
        _access = access;
    }

    public async Task Handle(ReorderStagesCommand request, CancellationToken cancellationToken)
    {
        DomainException.Garantir(request.OrderedStageIds.Count > 0,
            "A lista de etapas não pode ser vazia.");

        var board = await _boardRepository.GetByIdAsync(request.BoardId, cancellationToken);
        if (board is null)
            throw new ArgumentException("O quadro especificado não existe.");

        if (board.ProjectId.HasValue && _access is not null)
            await _access.EnsureAtLeastAsync(
                board.ProjectId.Value, request.ActorId, ProjectRole.ProjectAdmin, cancellationToken);

        var stagesDoBoard = await _stageRepository.GetByBoardIdAsync(request.BoardId, cancellationToken);
        var idsDoBoardSet = stagesDoBoard.Select(s => s.Id).ToHashSet();

        // Todos os IDs devem pertencer ao quadro
        foreach (var sid in request.OrderedStageIds)
            DomainException.Garantir(idsDoBoardSet.Contains(sid),
                $"A etapa {sid} não pertence ao quadro {request.BoardId}.");

        for (int i = 0; i < request.OrderedStageIds.Count; i++)
        {
            var stage = stagesDoBoard.First(s => s.Id == request.OrderedStageIds[i]);
            stage.Position = (i + 1) * 100.0;
            await _stageRepository.UpdateAsync(stage, cancellationToken);
        }
    }
}
