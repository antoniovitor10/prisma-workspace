using Detran.Kanban.Application.Features.Boards.Dtos;
using MediatR;

namespace Detran.Kanban.Application.Features.Boards.Queries;

/// <summary>
/// Query para buscar um Board por Id.
/// </summary>
public record GetBoardByIdQuery(Guid Id, string ActorId) : IRequest<BoardDto?>;
