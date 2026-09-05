using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Domain.Exceptions;
using MediatR;

namespace Detran.Kanban.Application.Features.Boards.Commands;

/// <summary>
/// Exclui um quadro do projeto com segurança:
/// — Proíbe excluir o último quadro.
/// — Realoca itens exclusivos ao quadro de destino antes de remover.
/// — Nunca deleta WorkItems.
/// </summary>
public class DeleteBoardCommandHandler : IRequestHandler<DeleteBoardCommand>
{
    private readonly IBoardRepository _boardRepository;
    private readonly IWorkItemRepository _workItemRepository;
    private readonly IStageRepository _stageRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectAccessService _projectAccess;

    public DeleteBoardCommandHandler(
        IBoardRepository boardRepository,
        IWorkItemRepository workItemRepository,
        IStageRepository stageRepository,
        IProjectRepository projectRepository,
        IProjectAccessService projectAccess)
    {
        _boardRepository = boardRepository;
        _workItemRepository = workItemRepository;
        _stageRepository = stageRepository;
        _projectRepository = projectRepository;
        _projectAccess = projectAccess;
    }

    public async Task Handle(DeleteBoardCommand request, CancellationToken cancellationToken)
    {
        var board = await _boardRepository.GetByIdAsync(request.BoardId, cancellationToken);
        if (board is null)
            throw new ArgumentException("O quadro especificado não existe.");

        if (!board.ProjectId.HasValue)
            throw new DomainException("Somente quadros vinculados a projetos podem ser excluídos por este endpoint.");

        // Verifica permissão: somente ProjectAdmin
        await _projectAccess.EnsureAtLeastAsync(
            board.ProjectId.Value, request.ActorId, ProjectRole.ProjectAdmin, cancellationToken);

        // Não pode excluir o único quadro do projeto
        var boardsDoProject = await _boardRepository.GetByProjectIdAsync(board.ProjectId.Value, cancellationToken);
        DomainException.Garantir(boardsDoProject.Count > 1,
            "Não é possível excluir o único quadro do projeto.");

        // Itens exclusivos deste quadro (home = este, sem outros placements)
        var itensExclusivos = await _workItemRepository.GetExclusiveToBoardAsync(request.BoardId, cancellationToken);

        if (itensExclusivos.Count > 0)
        {
            DomainException.Garantir(request.DestinationBoardId.HasValue,
                $"Existem {itensExclusivos.Count} item(ns) exclusivo(s) neste quadro. " +
                "Informe DestinationBoardId para realocá-los antes de excluir.");

            var destBoard = await _boardRepository.GetByIdAsync(request.DestinationBoardId!.Value, cancellationToken);
            DomainException.Garantir(destBoard is not null && destBoard.ProjectId == board.ProjectId,
                "O quadro de destino não existe ou pertence a outro projeto.");

            // Localiza Backlog no destino
            var stagesDestino = await _stageRepository.GetByBoardIdAsync(destBoard!.Id, cancellationToken);
            var backlogDestino = stagesDestino.FirstOrDefault(s =>
                    string.Equals(s.Name.Trim(), "Backlog", StringComparison.OrdinalIgnoreCase))
                ?? stagesDestino.FirstOrDefault(s => s.Category == StageCategory.Ready);
            DomainException.Garantir(backlogDestino is not null,
                $"O quadro de destino '{destBoard.Name}' não possui uma etapa Backlog (Ready).");

            var now = DateTimeOffset.UtcNow;
            foreach (var item in itensExclusivos)
            {
                // Muda home para o destino
                item.BoardId = destBoard.Id;
                item.StageId = backlogDestino!.Id;
                item.UpdatedAt = now;

                // Remove placement antigo deste board (se existia)
                var plOld = item.BoardPlacements.FirstOrDefault(p => p.BoardId == request.BoardId);
                if (plOld is not null)
                    item.BoardPlacements.Remove(plOld);

                // Cria/atualiza placement no destino
                var plDest = item.BoardPlacements.FirstOrDefault(p => p.BoardId == destBoard.Id);
                if (plDest is null)
                {
                    item.BoardPlacements.Add(new WorkItemBoardPlacement
                    {
                        Id = Guid.NewGuid(),
                        WorkItemId = item.Id,
                        BoardId = destBoard.Id,
                        StageId = backlogDestino.Id,
                        Position = item.Position,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
                else
                {
                    plDest.StageId = backlogDestino.Id;
                    plDest.UpdatedAt = now;
                }

                await _workItemRepository.UpdateAsync(item, cancellationToken);
            }
        }

        // Remove placements de itens não-exclusivos que apontavam para este board
        // (esses itens têm outros placements, continuam visíveis em seus outros quadros)
        // EF Cascade ou ClientSetNull cuidarão dos registros restantes ao deletar o board.

        // Atualiza DefaultBoardId do projeto se necessário
        var project = await _projectRepository.GetByIdAsync(board.ProjectId.Value, cancellationToken);
        if (project is not null && project.DefaultBoardId == request.BoardId)
        {
            project.DefaultBoardId = request.DestinationBoardId
                ?? boardsDoProject.FirstOrDefault(b => b.Id != request.BoardId)?.Id;
            await _projectRepository.SaveAsync(cancellationToken);
        }

        await _boardRepository.DeleteAsync(board, cancellationToken);
    }
}
