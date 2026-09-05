using Detran.Kanban.Application.Features.Stages.Dtos;
using MediatR;

namespace Detran.Kanban.Application.Features.Stages.Queries;

/// <summary>
/// Query para buscar todas as etapas (Stages) de um Quadro (Board).
/// </summary>
public record GetStagesByBoardIdQuery(Guid BoardId, string ActorId)
    : IRequest<IReadOnlyList<StageDto>>;
