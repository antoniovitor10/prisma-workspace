using Detran.Kanban.Application.Features.Stages.Dtos;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Enums;
using MediatR;

namespace Detran.Kanban.Application.Features.Stages.Queries;

/// <summary>
/// Handler da query de listagem de Stages por Board.
/// </summary>
public class GetStagesByBoardIdQueryHandler : IRequestHandler<GetStagesByBoardIdQuery, IReadOnlyList<StageDto>>
{
    private readonly IStageRepository _stageRepository;
    private readonly IBoardAccessService _access;

    public GetStagesByBoardIdQueryHandler(
        IStageRepository stageRepository,
        IBoardAccessService access)
    {
        _stageRepository = stageRepository;
        _access = access;
    }

    public async Task<IReadOnlyList<StageDto>> Handle(
        GetStagesByBoardIdQuery request,
        CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(request.BoardId, request.ActorId,
            PlatformPermission.View, ProjectRole.Viewer, cancellationToken);
        var stages = await _stageRepository.GetByBoardIdAsync(request.BoardId, cancellationToken);

        return stages.Select(s => new StageDto
        {
            Id = s.Id,
            BoardId = s.BoardId,
            Name = s.Name,
            Position = s.Position,
            WipLimit = s.WipLimit,
            WorkflowStatusId = s.WorkflowStatusId,
            StatusName = s.WorkflowStatus?.Name,
            StatusColor = s.WorkflowStatus?.Color,
            IsInitial = s.WorkflowStatus?.IsInitial ?? false,
            IsFinal = s.WorkflowStatus?.IsFinal ?? false,
            CreatedAt = s.CreatedAt
        }).ToList().AsReadOnly();
    }
}
