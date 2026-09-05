using Prisma.Workspace.Application.Features.Boards.Dtos;
using MediatR;

namespace Prisma.Workspace.Application.Features.Boards.Queries;

/// <summary>
/// Query para listar todos os Boards.
/// </summary>
public record GetAllBoardsQuery(string ActorId) : IRequest<IReadOnlyList<BoardDto>>;
