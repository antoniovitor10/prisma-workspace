using Detran.Kanban.Application.Features.Boards.Dtos;
using MediatR;

namespace Detran.Kanban.Application.Features.Boards.Queries;

/// <summary>
/// Query para calcular o Lead Time médio por etapa de um Board.
/// </summary>
public record GetBoardLeadTimeQuery(Guid BoardId, string ActorId)
    : IRequest<IReadOnlyList<StageLeadTimeDto>>;
