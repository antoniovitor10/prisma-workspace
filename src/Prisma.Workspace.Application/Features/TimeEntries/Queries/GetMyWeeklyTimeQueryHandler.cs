using Prisma.Workspace.Application.Features.TimeEntries.Dtos;
using Prisma.Workspace.Application.Interfaces;
using MediatR;

namespace Prisma.Workspace.Application.Features.TimeEntries.Queries;

/// <summary>
/// Agrupa os lançamentos da semana em baldes por dia no fuso local.
/// Cronômetro aberto conta o decorrido até agora.
/// </summary>
public class GetMyWeeklyTimeQueryHandler : IRequestHandler<GetMyWeeklyTimeQuery, WeeklyTimeDto>
{
    // O produto é usado só no Brasil; o fuso fica fixo até precisarmos de outro.


    private readonly ITimeEntryRepository _timeEntries;

    public GetMyWeeklyTimeQueryHandler(ITimeEntryRepository timeEntries)
    {
        _timeEntries = timeEntries;
    }

    public async Task<WeeklyTimeDto> Handle(GetMyWeeklyTimeQuery request, CancellationToken cancellationToken)
    {
        var startDate = request.WeekStart?.ToDateTime(TimeOnly.MinValue).Date
            ?? InicioDaSemanaCorrente();

        var startLocal = TimeTrackingDay.Start(DateOnly.FromDateTime(startDate));
        var endLocal = TimeTrackingDay.Start(DateOnly.FromDateTime(startDate).AddDays(7));

        var entries = await _timeEntries.GetByUserInRangeAsync(
            request.UserId, startLocal.ToUniversalTime(), endLocal.ToUniversalTime(), cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var buckets = new Dictionary<DateTime, int>();
        for (var i = 0; i < 7; i++) buckets[startDate.AddDays(i)] = 0;

        foreach (var entry in entries)
        {
            var dayKey = TimeTrackingDay.Date(entry.StartedAt).ToDateTime(TimeOnly.MinValue);
            var endedAt = entry.EndedAt ?? now;
            var secs = Math.Max(0, (int)(endedAt - entry.StartedAt).TotalSeconds);
            if (buckets.ContainsKey(dayKey)) buckets[dayKey] += secs;
        }

        return new WeeklyTimeDto
        {
            WeekStart = startDate.ToString("yyyy-MM-dd"),
            Days = buckets
                .OrderBy(kv => kv.Key)
                .Select(kv => new DailyTimeDto
                {
                    Date = kv.Key.ToString("yyyy-MM-dd"),
                    TotalSeconds = kv.Value,
                    TotalHours = Math.Round(kv.Value / 3600m, 2)
                })
                .ToList()
        };
    }

    private static DateTime InicioDaSemanaCorrente()
    {
        var todayLocal = TimeTrackingDay.Date(DateTimeOffset.UtcNow).ToDateTime(TimeOnly.MinValue);
        var diff = ((int)todayLocal.DayOfWeek + 6) % 7; // segunda-feira = 0
        return todayLocal.AddDays(-diff);
    }
}
