using Detran.Kanban.Application.Features.TimeEntries.Dtos;
using MediatR;

namespace Detran.Kanban.Application.Features.TimeEntries.Queries;

/// <summary>Lançamentos de tempo de uma tarefa.</summary>
public record GetTimeEntriesByWorkItemQuery(Guid WorkItemId, string ActorId)
    : IRequest<IReadOnlyList<TimeEntryDto>>;
