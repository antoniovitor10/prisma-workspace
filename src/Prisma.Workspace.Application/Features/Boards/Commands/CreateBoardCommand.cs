using MediatR;

namespace Prisma.Workspace.Application.Features.Boards.Commands;

/// <summary>
/// Comando para criar um novo Board.
/// </summary>
public record CreateBoardCommand(string Name, string OwnerId, Guid? ProjectId = null, Guid? TeamId = null) : IRequest<Guid>;
