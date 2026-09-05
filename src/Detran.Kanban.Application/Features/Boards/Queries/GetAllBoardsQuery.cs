using Detran.Kanban.Application.Features.Boards.Dtos;
using MediatR;

namespace Detran.Kanban.Application.Features.Boards.Queries;

/// <summary>
/// Query para listar todos os Boards.
/// </summary>
public record GetAllBoardsQuery(string ActorId) : IRequest<IReadOnlyList<BoardDto>>;
