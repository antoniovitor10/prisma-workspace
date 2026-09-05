using Prisma.Workspace.Application.Features.MeTime.Dtos;
using MediatR;

namespace Prisma.Workspace.Application.Features.MeTime.Queries;

/// <summary>Total por tarefa nos lançamentos que começaram no dia (UTC).</summary>
public record GetMyDailyByTaskQuery(string UserId, DateOnly Date) : IRequest<IReadOnlyList<TaskDayTimeDto>>;
