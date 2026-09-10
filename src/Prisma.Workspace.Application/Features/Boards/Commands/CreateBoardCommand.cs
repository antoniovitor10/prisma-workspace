using MediatR;

namespace Prisma.Workspace.Application.Features.Boards.Commands;

/// <summary>
/// Comando para criar um novo Board (visão salva do projeto — D83).
/// </summary>
public record CreateBoardCommand(string Name, string OwnerId, Guid ProjectId, Guid? TeamId = null) : IRequest<Guid>;
