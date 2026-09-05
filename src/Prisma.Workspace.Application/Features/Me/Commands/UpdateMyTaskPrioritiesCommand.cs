using MediatR;

namespace Prisma.Workspace.Application.Features.Me.Commands;

/// <summary>Reordena a fila pessoal: recebe os IDs na nova ordem.</summary>
public record UpdateMyTaskPrioritiesCommand(string UserId, IReadOnlyList<Guid> WorkItemIds) : IRequest;
