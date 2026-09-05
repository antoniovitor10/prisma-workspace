using Prisma.Workspace.Application.Features.Boards.Dtos;
using MediatR;

namespace Prisma.Workspace.Application.Features.Boards.Queries;

/// <summary>
/// Query para buscar um Board por Id.
/// </summary>
public record GetBoardByIdQuery(Guid Id, string ActorId) : IRequest<BoardDto?>;
