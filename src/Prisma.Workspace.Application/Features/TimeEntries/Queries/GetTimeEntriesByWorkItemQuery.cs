using Prisma.Workspace.Application.Features.TimeEntries.Dtos;
using MediatR;

namespace Prisma.Workspace.Application.Features.TimeEntries.Queries;

/// <summary>Lançamentos de tempo de uma tarefa.</summary>
public record GetTimeEntriesByWorkItemQuery(Guid WorkItemId, string ActorId)
    : IRequest<IReadOnlyList<TimeEntryDto>>;
