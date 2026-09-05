using Detran.Kanban.Application.Features.MeTime.Dtos;
using MediatR;

namespace Detran.Kanban.Application.Features.MeTime.Queries;

/// <summary>Total por tarefa nos lançamentos que começaram no dia (UTC).</summary>
public record GetMyDailyByTaskQuery(string UserId, DateOnly Date) : IRequest<IReadOnlyList<TaskDayTimeDto>>;
