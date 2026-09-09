using Prisma.Workspace.Application.Features.Stages.Dtos;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;
using MediatR;

namespace Prisma.Workspace.Application.Features.Stages.Queries;

/// <summary>
/// Handler da query de listagem de Stages por projeto.
/// </summary>
public class GetStagesByProjectIdQueryHandler : IRequestHandler<GetStagesByProjectIdQuery, IReadOnlyList<StageDto>>
{
    private readonly IStageRepository _stageRepository;
    private readonly IProjectAccessService _access;

    public GetStagesByProjectIdQueryHandler(
        IStageRepository stageRepository,
        IProjectAccessService access)
    {
        _stageRepository = stageRepository;
        _access = access;
    }

    public async Task<IReadOnlyList<StageDto>> Handle(
        GetStagesByProjectIdQuery request,
        CancellationToken cancellationToken)
    {
        await _access.EnsureAtLeastAsync(
            request.ProjectId, request.ActorId, ProjectRole.Viewer, cancellationToken);
        var stages = await _stageRepository.GetByProjectIdAsync(request.ProjectId, cancellationToken);

        return stages.Select(s => new StageDto
        {
            Id = s.Id,
            ProjectId = s.ProjectId,
            Name = s.Name,
            Position = s.Position,
            WorkflowStatusId = s.WorkflowStatusId,
            Category = s.Category,
            StatusName = s.WorkflowStatus?.Name,
            StatusColor = s.WorkflowStatus?.Color,
            IsInitial = s.WorkflowStatus?.IsInitial ?? false,
            IsFinal = s.WorkflowStatus?.IsFinal ?? false,
            CreatedAt = s.CreatedAt
        }).ToList().AsReadOnly();
    }
}
