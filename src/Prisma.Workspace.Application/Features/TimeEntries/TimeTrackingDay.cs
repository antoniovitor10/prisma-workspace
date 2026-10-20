namespace Prisma.Workspace.Application.Features.TimeEntries;

public static class TimeTrackingDay
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    public static DateTimeOffset Start(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local)).ToUniversalTime();
    }
    public static DateOnly Date(DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);

    /// <summary>Segundos do intervalo que pertencem ao recorte, sem duplicar frações nas fronteiras.</summary>
    public static int SecondsInRange(DateTimeOffset startedAt, DateTimeOffset endedAt,
        DateTimeOffset from, DateTimeOffset to)
    {
        var start = startedAt > from ? startedAt : from;
        var end = endedAt < to ? endedAt : to;
        if (end <= start) return 0;
        return checked((int)(((end - startedAt).Ticks / TimeSpan.TicksPerSecond)
            - ((start - startedAt).Ticks / TimeSpan.TicksPerSecond)));
    }
}
