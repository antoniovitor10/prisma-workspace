using Prisma.Workspace.Application.Features.TimeEntries.Dtos;
using MediatR;

namespace Prisma.Workspace.Application.Features.TimeEntries.Queries;

/// <summary>
/// Total de segundos por tarefa, usuário OU quadro (exatamente um alvo).
/// </summary>
public record GetTimeTotalQuery(
    string ActorId,
    Guid? WorkItemId = null,
    string? UserId = null,
    Guid? BoardId = null)
    : IRequest<TimeTotalDto>;
