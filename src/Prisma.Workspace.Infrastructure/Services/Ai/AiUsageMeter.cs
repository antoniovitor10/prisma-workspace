using System.Data;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.Ai;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Infrastructure.Persistence;

namespace Prisma.Workspace.Infrastructure.Services.Ai;

public sealed class AiUsageMeter(AppDbContext db) : IAiUsageMeter
{
    internal static (DateTimeOffset Start, DateTimeOffset End) Day(string zone, DateTimeOffset now)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(zone);
        var date = TimeZoneInfo.ConvertTime(now, tz).Date;
        return (new(TimeZoneInfo.ConvertTimeToUtc(date, tz), TimeSpan.Zero), new(TimeZoneInfo.ConvertTimeToUtc(date.AddDays(1), tz), TimeSpan.Zero));
    }
    public async Task<AiUsageRecord> BeginAsync(AiProviderConnection connection, string userId, Guid? organizationId, int reservation, CancellationToken ct)
    {
        var relational = db.Database.IsRelational();
        await using var transaction = relational ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (relational) await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r = sp_getapplock @Resource='PrismaAiUsage', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r < 0 THROW 51000, 'AI usage lock unavailable', 1;", ct);
        var settings = await db.AiInstallationSettings.SingleOrDefaultAsync(ct) ?? new();
        var org = organizationId.HasValue ? await db.OrganizationAiSettings.SingleOrDefaultAsync(x => x.OrganizationId == organizationId, ct) : null;
        var now = DateTimeOffset.UtcNow; var day = Day(settings.TimeZone, now);
        var q = db.AiUsageRecords.IgnoreQueryFilters().Where(x => x.CreatedAt >= day.Start && x.CreatedAt < day.End);
        var rows = await q.Select(x => new { x.OrganizationId, x.UserId, x.InputTokens, x.OutputTokens, x.ReservedTokens, x.Outcome, x.CreatedAt }).ToListAsync(ct);
        var totals = new[] { rows.Sum(r => r.InputTokens + r.OutputTokens + (r.Outcome == "pending" && r.CreatedAt > now.AddMinutes(-10) ? r.ReservedTokens : 0)),
            rows.Where(r => r.OrganizationId == organizationId).Sum(r => r.InputTokens + r.OutputTokens + (r.Outcome == "pending" && r.CreatedAt > now.AddMinutes(-10) ? r.ReservedTokens : 0)),
            rows.Where(r => r.UserId == userId && r.OrganizationId == organizationId).Sum(r => r.InputTokens + r.OutputTokens + (r.Outcome == "pending" && r.CreatedAt > now.AddMinutes(-10) ? r.ReservedTokens : 0)) };
        var limits = new[] { settings.DailyTokenLimit, organizationId is null ? 0 : org?.DailyTokenLimit ?? settings.OrganizationTokenLimit,
            organizationId is null ? 0 : org?.UserTokenLimit ?? settings.UserTokenLimit };
        string[] labels = ["instalação", "organização", "usuário"];
        for (var i = 0; i < 3; i++) if (limits[i] > 0 && totals[i] >= limits[i])
            throw new AiException(429, $"Limite diário de {labels[i]} atingido. Renova em {day.End:O}.", day.End);
        var record = new AiUsageRecord { OrganizationId = organizationId, UserId = userId, ConnectionId = connection.Id,
            Model = connection.Model, ReservedTokens = reservation };
        db.AiUsageRecords.Add(record); await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return record;
    }
    public async Task FinishAsync(AiUsageRecord record, AiProviderConnection connection, AiProviderResult? result, int durationMs, string outcome, CancellationToken ct)
    {
        record.InputTokens = result?.InputTokens ?? 0; record.OutputTokens = result?.OutputTokens ?? 0;
        record.EstimatedCost = (connection.InputPrice.HasValue || connection.OutputPrice.HasValue)
            && (record.InputTokens == 0 || connection.InputPrice.HasValue)
            && (record.OutputTokens == 0 || connection.OutputPrice.HasValue)
            ? (record.InputTokens * (connection.InputPrice ?? 0) + record.OutputTokens * (connection.OutputPrice ?? 0)) / 1_000_000m : null;
        record.ReservedTokens = 0; record.DurationMs = durationMs; record.Outcome = outcome;
        await db.SaveChangesAsync(ct);
    }
}
