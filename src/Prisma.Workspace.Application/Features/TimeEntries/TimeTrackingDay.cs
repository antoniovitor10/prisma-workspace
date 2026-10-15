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
}
