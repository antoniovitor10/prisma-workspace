using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using MediatR;

namespace Prisma.Workspace.Application.Features.Boards.Commands;

/// <summary>
/// Exclui um quadro do projeto com segurança:
/// — Proíbe excluir o último quadro.
/// — Realoca itens exclusivos ao quadro de destino antes de remover.
/// — Nunca deleta WorkItems. Colunas pertencem ao projeto (D83), não ao quadro.
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

        await _projectAccess.EnsureAtLeastAsync(
            board.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, cancellationToken);

        var boardsDoProject = await _boardRepository.GetByProjectIdAsync(board.ProjectId, cancellationToken);
        DomainException.Garantir(boardsDoProject.Count > 1,
            "Não é possível excluir o único quadro do projeto.");

        var itensExclusivos = await _workItemRepository.GetExclusiveToBoardAsync(request.BoardId, cancellationToken);

        if (itensExclusivos.Count > 0)
        {
            DomainException.Garantir(request.DestinationBoardId.HasValue,
                $"Existem {itensExclusivos.Count} item(ns) exclusivo(s) neste quadro. " +
                "Informe DestinationBoardId para realocá-los antes de excluir.");

            var destBoard = await _boardRepository.GetByIdAsync(request.DestinationBoardId!.Value, cancellationToken);
            DomainException.Garantir(destBoard is not null && destBoard.ProjectId == board.ProjectId,
                "O quadro de destino não existe ou pertence a outro projeto.");

            // Colunas são do projeto — Backlog do fluxo do projeto
            var stagesDoProjeto = await _stageRepository.GetByProjectIdAsync(destBoard!.ProjectId, cancellationToken);
            var backlogDestino = stagesDoProjeto.FirstOrDefault(s =>
                    string.Equals(s.Name.Trim(), "Backlog", StringComparison.OrdinalIgnoreCase))
                ?? stagesDoProjeto.FirstOrDefault(s => s.Category == StageCategory.Ready);
            DomainException.Garantir(backlogDestino is not null,
                $"O projeto do quadro '{destBoard.Name}' não possui uma etapa Backlog (Ready).");

            var now = DateTimeOffset.UtcNow;
            foreach (var item in itensExclusivos)
            {
                item.BoardId = destBoard.Id;
                item.StageId = backlogDestino!.Id;
                item.UpdatedAt = now;

                await _workItemRepository.UpdateAsync(item, cancellationToken);
            }
        }

        var project = await _projectRepository.GetByIdAsync(board.ProjectId, cancellationToken);
        if (project is not null && project.DefaultBoardId == request.BoardId)
        {
            project.DefaultBoardId = request.DestinationBoardId
                ?? boardsDoProject.FirstOrDefault(b => b.Id != request.BoardId)?.Id;
            await _projectRepository.SaveAsync(cancellationToken);
        }

        await _boardRepository.DeleteAsync(board, cancellationToken);
    }
}
