using Prisma.Workspace.Application.Features.Stages.Dtos;
using Prisma.Workspace.Application.Interfaces;
using MediatR;

namespace Prisma.Workspace.Application.Features.Stages.Queries;

/// <summary>
/// Query para buscar todas as etapas (Stages) do fluxo de um projeto.
/// </summary>
public record GetStagesByProjectIdQuery(Guid ProjectId, string ActorId)
    : IRequest<IReadOnlyList<StageDto>>;


public record GetStagesByBoardIdQuery(Guid BoardId, string ActorId) : IRequest<IReadOnlyList<StageDto>>;

public sealed class GetStagesByBoardIdQueryHandler : IRequestHandler<GetStagesByBoardIdQuery, IReadOnlyList<StageDto>>
{
    private readonly IStageRepository _stages; private readonly IBoardAccessService _access;
    public GetStagesByBoardIdQueryHandler(IStageRepository stages, IBoardAccessService access) => (_stages,_access)=(stages,access);
    public async Task<IReadOnlyList<StageDto>> Handle(GetStagesByBoardIdQuery request, CancellationToken ct)
    {
        await _access.EnsureAsync(request.BoardId, request.ActorId, Prisma.Workspace.Domain.Enums.PlatformPermission.View, Prisma.Workspace.Domain.Enums.ProjectRole.Viewer, ct);
        return (await _stages.GetByBoardIdAsync(request.BoardId,ct)).Select(s=>new StageDto { Id=s.Id, ProjectId=s.ProjectId, BoardId=s.BoardId, Name=s.Name, Position=s.Position, WorkflowStatusId=s.WorkflowStatusId, Category=s.Category, StatusName=s.WorkflowStatus?.Name, StatusColor=s.WorkflowStatus?.Color, IsInitial=s.WorkflowStatus?.IsInitial??false, IsFinal=s.WorkflowStatus?.IsFinal??false, CreatedAt=s.CreatedAt }).ToList();
    }
}
