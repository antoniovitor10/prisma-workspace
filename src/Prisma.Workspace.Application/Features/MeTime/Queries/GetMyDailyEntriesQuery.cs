using MediatR;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Application.Features.TimeEntries;
using Prisma.Workspace.Application.Features.TimeEntries.Dtos;

namespace Prisma.Workspace.Application.Features.MeTime.Queries;

public sealed record DayTimeEntryDto(TimeEntryDto Entry, string Title);
public sealed record GetMyDailyEntriesQuery(string UserId, DateOnly Date) : IRequest<IReadOnlyList<DayTimeEntryDto>>;
public sealed class GetMyDailyEntriesQueryHandler(ITimeEntryRepository entries)
    : IRequestHandler<GetMyDailyEntriesQuery, IReadOnlyList<DayTimeEntryDto>>
{
    public async Task<IReadOnlyList<DayTimeEntryDto>> Handle(GetMyDailyEntriesQuery request, CancellationToken ct)
    {
        var rows = await entries.GetByUserInRangeWithWorkItemAsync(request.UserId,
            TimeTrackingDay.Start(request.Date), TimeTrackingDay.Start(request.Date.AddDays(1)), ct);
        return rows.OrderBy(e => e.StartedAt)
            .Select(e => new DayTimeEntryDto(e.ToDto(), e.WorkItem?.Title ?? "(tarefa)")).ToList();
    }
}

public sealed record TimeAdjustmentTaskDto(Guid Id, long Number, string Title, string BoardName, bool Assigned);
public sealed record SearchTimeAdjustmentTasksQuery(string UserId, string Query) : IRequest<IReadOnlyList<TimeAdjustmentTaskDto>>;
public sealed class SearchTimeAdjustmentTasksQueryHandler(ITimeEntryRepository entries)
    : IRequestHandler<SearchTimeAdjustmentTasksQuery, IReadOnlyList<TimeAdjustmentTaskDto>>
{
    public Task<IReadOnlyList<TimeAdjustmentTaskDto>> Handle(SearchTimeAdjustmentTasksQuery request, CancellationToken ct)
        => entries.SearchAdjustmentTasksAsync(request.UserId, request.Query[..Math.Min(100, request.Query.Length)], ct);
}
