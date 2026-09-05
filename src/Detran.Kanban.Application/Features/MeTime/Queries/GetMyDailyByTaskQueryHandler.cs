using Detran.Kanban.Application.Features.MeTime.Dtos;
using Detran.Kanban.Application.Interfaces;
using MediatR;

namespace Detran.Kanban.Application.Features.MeTime.Queries;

public class GetMyDailyByTaskQueryHandler
    : IRequestHandler<GetMyDailyByTaskQuery, IReadOnlyList<TaskDayTimeDto>>
{
    private readonly ITimeEntryRepository _timeEntries;

    public GetMyDailyByTaskQueryHandler(ITimeEntryRepository timeEntries)
    {
        _timeEntries = timeEntries;
    }

    public async Task<IReadOnlyList<TaskDayTimeDto>> Handle(
        GetMyDailyByTaskQuery request,
        CancellationToken cancellationToken)
    {
        var start = new DateTimeOffset(request.Date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end = start.AddDays(1);

        var entries = await _timeEntries.GetByUserInRangeWithWorkItemAsync(
            request.UserId, start, end, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        return entries
            .GroupBy(e => new { e.WorkItemId, Title = e.WorkItem != null ? e.WorkItem.Title : "(tarefa)" })
            .Select(g => new TaskDayTimeDto
            {
                WorkItemId = g.Key.WorkItemId,
                Title = g.Key.Title,
                Seconds = g.Sum(e => Math.Max(0, (int)((e.EndedAt ?? now) - e.StartedAt).TotalSeconds))
            })
            .OrderByDescending(x => x.Seconds)
            .ToList();
    }
}
