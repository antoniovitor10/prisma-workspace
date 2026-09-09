using Prisma.Workspace.Application.Features.Boards.Dtos;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;
using MediatR;

namespace Prisma.Workspace.Application.Features.Boards.Queries;

/// <summary>
/// Handler do cálculo de Lead Time por etapa do fluxo do projeto (visão filtrada pelo quadro).
/// </summary>
public class GetBoardLeadTimeQueryHandler : IRequestHandler<GetBoardLeadTimeQuery, IReadOnlyList<StageLeadTimeDto>>
{
    private readonly IBoardRepository _boardRepository;
    private readonly IStageRepository _stageRepository;
    private readonly IStageHistoryRepository _stageHistoryRepository;
    private readonly IBoardAccessService _access;

    public GetBoardLeadTimeQueryHandler(
        IBoardRepository boardRepository,
        IStageRepository stageRepository,
        IStageHistoryRepository stageHistoryRepository,
        IBoardAccessService access)
    {
        _boardRepository = boardRepository;
        _stageRepository = stageRepository;
        _stageHistoryRepository = stageHistoryRepository;
        _access = access;
    }

    public async Task<IReadOnlyList<StageLeadTimeDto>> Handle(
        GetBoardLeadTimeQuery request,
        CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(request.BoardId, request.ActorId,
            PlatformPermission.View, ProjectRole.Viewer, cancellationToken);

        var board = await _boardRepository.GetByIdAsync(request.BoardId, cancellationToken)
            ?? throw new ArgumentException("O quadro especificado não existe.");

        var stages = await _stageRepository.GetByProjectIdAsync(board.ProjectId, cancellationToken);
        var histories = await _stageHistoryRepository.GetByBoardIdAsync(request.BoardId, cancellationToken);

        var leadTimes = new List<StageLeadTimeDto>();

        foreach (var stage in stages)
        {
            var stageHistories = histories
                .Where(h => h.StageId == stage.Id && h.LeftAt.HasValue)
                .ToList();

            double avgSecs = 0;
            if (stageHistories.Count > 0)
            {
                avgSecs = stageHistories.Average(h => (h.LeftAt!.Value - h.EnteredAt).TotalSeconds);
            }

            leadTimes.Add(new StageLeadTimeDto
            {
                StageId = stage.Id,
                StageName = stage.Name,
                AverageSeconds = Math.Round(avgSecs, 2),
                AverageHours = Math.Round(avgSecs / 3600.0, 2),
                AverageDays = Math.Round(avgSecs / 86400.0, 2),
                ItemsCount = stageHistories.Count
            });
        }

        return leadTimes.AsReadOnly();
    }
}
