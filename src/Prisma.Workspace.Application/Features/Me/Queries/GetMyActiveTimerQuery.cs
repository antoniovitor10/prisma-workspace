using Prisma.Workspace.Application.Features.Me.Dtos;
using MediatR;

namespace Prisma.Workspace.Application.Features.Me.Queries;

/// <summary>Cronômetro ativo do usuário com o título da tarefa (nulo se não houver).</summary>
public record GetMyActiveTimerQuery(string UserId) : IRequest<ActiveTimerDto?>;
