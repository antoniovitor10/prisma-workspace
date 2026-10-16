using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Exceptions;

namespace Prisma.Workspace.Application.Features.TimeEntries;

public static class TimeEntryPeriod
{
    public static async Task ValidateAsync(ITimeEntryRepository entries, string userId,
        DateTimeOffset start, DateTimeOffset end, string? note,
        TimeEntry? original = null, CancellationToken ct = default)
    {
        DomainException.Garantir(end > start, "O fim deve ser maior que o início.");
        DomainException.Garantir(end <= DateTimeOffset.UtcNow, "O lançamento não pode estar no futuro.");
        DomainException.Garantir(note is null || note.Length <= 2000, "A observação deve ter até 2000 caracteres.");
        // Um subconjunto nunca aumenta conflitos já existentes.
        var preservesOrShrinks = original is not null && start >= original.StartedAt && end <= original.EndedAt;
        if (!preservesOrShrinks)
        {
            var firstDay = TimeTrackingDay.Date(start);
            var lastDay = TimeTrackingDay.Date(end.AddTicks(-1));
            var periods = await entries.GetUserPeriodsForValidationAsync(userId,
                TimeTrackingDay.Start(firstDay), TimeTrackingDay.Start(lastDay.AddDays(1)), ct);
            var now = DateTimeOffset.UtcNow;
            foreach (var period in periods.Where(p => p.Id != original?.Id))
            {
                var periodEnd = period.EndedAt ?? now;
                var before = original is null ? 0 : OverlapTicks(original.StartedAt, original.EndedAt!.Value, period.StartedAt, periodEnd);
                var after = OverlapTicks(start, end, period.StartedAt, periodEnd);
                DomainException.Garantir(after <= before, "O período se sobrepõe a outro lançamento seu.");
            }
            for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
            {
                var from = TimeTrackingDay.Start(date);
                var to = TimeTrackingDay.Start(date.AddDays(1));
                var seconds = TimeTrackingDay.SecondsInRange(start, end, from, to);
                var others = periods.Where(p => p.Id != original?.Id)
                    .Sum(p => (long)TimeTrackingDay.SecondsInRange(p.StartedAt, p.EndedAt ?? now, from, to));
                var previous = original is null ? 0 : TimeTrackingDay.SecondsInRange(original.StartedAt, original.EndedAt!.Value, from, to);
                DomainException.Garantir(seconds + others <= 24 * 3600 || seconds <= previous,
                    "O total de horas no dia de Brasília não pode ultrapassar 24 horas.");
            }
        }
    }

    private static long OverlapTicks(DateTimeOffset start, DateTimeOffset end,
        DateTimeOffset otherStart, DateTimeOffset otherEnd)
    {
        var from = start > otherStart ? start : otherStart;
        var to = end < otherEnd ? end : otherEnd;
        return Math.Max(0, (to - from).Ticks);
    }
}
