using Prisma.Workspace.Application.Features.Boards.Dtos;
using MediatR;

namespace Prisma.Workspace.Application.Features.Boards.Queries;

/// <summary>
/// Query para calcular o Lead Time médio por etapa de um Board.
/// </summary>
public record GetBoardLeadTimeQuery(Guid BoardId, string ActorId)
    : IRequest<IReadOnlyList<StageLeadTimeDto>>;
