using Prisma.Workspace.Application.Features.MeTime.Dtos;
using MediatR;

namespace Prisma.Workspace.Application.Features.MeTime.Queries;

/// <summary>Total por tarefa no recorte do dia civil de Brasília, inclusive períodos iniciados antes dele.</summary>
public record GetMyDailyByTaskQuery(string UserId, DateOnly Date) : IRequest<IReadOnlyList<TaskDayTimeDto>>;
