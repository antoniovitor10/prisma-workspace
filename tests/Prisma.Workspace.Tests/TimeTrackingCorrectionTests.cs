using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Features.MeTime.Queries;
using Prisma.Workspace.Application.Features.TimeEntries.Commands;
using Prisma.Workspace.Application.Features.TimeEntries.Queries;
using Prisma.Workspace.Application.Features.TimeEntries;
using Prisma.Workspace.Application.Features.WorkItems.Queries;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Repositories;

namespace Prisma.Workspace.Tests;

public class TimeTrackingCorrectionTests
{
    private sealed class Tenant(Guid id) : IOrganizationContext { public Guid? OrganizationId => id; }
    private sealed class Audit : IAuditContext
    {
        public string UserId => "owner";
        public string Origin => "api";
        public string? IpAddress => null;
        public string CorrelationId => "time-correction";
    }
    private sealed class Access : IWorkItemAccessService
    {
        public bool Deny { get; set; }
        public Task EnsureAsync(Guid id, string user, PlatformPermission permission,
            ProjectRole minimumRole = ProjectRole.Viewer, CancellationToken cancellationToken = default)
        {
            Assert.Equal(PlatformPermission.Edit, permission);
            Assert.Equal(ProjectRole.Member, minimumRole);
            if (Deny) throw new NaoEncontradoException("Tarefa");
            return Task.CompletedTask;
        }
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public AppDbContext Db { get; }
        public TimeEntryRepository Repo { get; }
        public Access Access { get; } = new();
        public WorkItem Item { get; }
        public DateTimeOffset Start { get; } = new(2026, 1, 12, 12, 0, 0, TimeSpan.Zero);
        public Fixture()
        {
            var org = Guid.NewGuid();
            Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"time-fix-{Guid.NewGuid()}").Options, new Tenant(org), new Audit());
            Item = new WorkItem { Id = Guid.NewGuid(), Title = "Horas", Board = new Board {
                Id = Guid.NewGuid(), Name = "Quadro", OrganizationId = org, OwnerId = "owner" }, CreatedBy = "owner" };
            Item.BoardId = Item.Board.Id;
            Db.WorkItems.Add(Item);
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
            Repo = new TimeEntryRepository(Db);
        }
        public async Task<TimeEntry> Entry(bool running = false, string user = "owner")
        {
            var entry = running ? TimeEntry.IniciarAgora(Item.Id, user, "original")
                : TimeEntry.Manual(Item.Id, user, Start, Start.AddHours(1), "original");
            await Repo.AddAsync(entry);
            Db.ChangeTracker.Clear();
            return entry;
        }
        public Task<Prisma.Workspace.Application.Features.TimeEntries.Dtos.TimeEntryDto> Update(TimeEntry e,
            DateTimeOffset? start = null, DateTimeOffset? end = null, string user = "owner")
            => new UpdateTimeEntryCommandHandler(Repo, Access).Handle(new(e.Id, user,
                start ?? Start, end ?? Start.AddMinutes(30), "corrigido"), default);
        public Task Delete(TimeEntry e, string user = "owner") => new DeleteTimeEntryCommandHandler(Repo, Access).Handle(new(e.Id, user), default);
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    [Fact]
    public async Task OwnerEditsWithBeforeAfterAuditThenDeletesWithPreviousSnapshot()
    {
        await using var f = new Fixture();
        var entry = await f.Entry();
        var dto = await f.Update(entry, start:f.Start.AddMinutes(5), end:f.Start.AddMinutes(35));
        Assert.Equal(1800, dto.DurationSeconds);
        Assert.Equal("corrigido", dto.Note);
        Assert.True(dto.IsManual);
        var audit = Assert.Single(await f.Db.AuditLogs.Where(a => a.EntityId == entry.Id.ToString() && a.Action == "updated").ToListAsync());
        Assert.Equal("owner", audit.UserId);
        Assert.Equal("time-correction", audit.CorrelationId);
        Assert.Equal("original", JsonDocument.Parse(audit.PreviousValuesJson!).RootElement.GetProperty("Note").GetString());
        Assert.Equal("corrigido", JsonDocument.Parse(audit.NewValuesJson!).RootElement.GetProperty("Note").GetString());
        Assert.Equal(f.Start.AddHours(1), JsonDocument.Parse(audit.PreviousValuesJson!).RootElement.GetProperty("EndedAt").GetDateTimeOffset());
        Assert.Equal(f.Start.AddMinutes(35), JsonDocument.Parse(audit.NewValuesJson!).RootElement.GetProperty("EndedAt").GetDateTimeOffset());
        Assert.Equal(f.Start, JsonDocument.Parse(audit.PreviousValuesJson!).RootElement.GetProperty("StartedAt").GetDateTimeOffset());
        Assert.Equal(f.Start.AddMinutes(5), JsonDocument.Parse(audit.NewValuesJson!).RootElement.GetProperty("StartedAt").GetDateTimeOffset());
        f.Db.ChangeTracker.Clear();
        await f.Delete(entry);
        Assert.Empty(await f.Db.TimeEntries.ToListAsync());
        audit = Assert.Single(await f.Db.AuditLogs.Where(a => a.EntityId == entry.Id.ToString() && a.Action == "deleted").ToListAsync());
        Assert.Equal("owner", audit.UserId);
        Assert.Null(audit.NewValuesJson);
        Assert.Equal("corrigido", JsonDocument.Parse(audit.PreviousValuesJson!).RootElement.GetProperty("Note").GetString());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task NonOwnerCannotEditOrDelete(bool delete)
    {
        await using var f = new Fixture();var e = await f.Entry();
        if (delete) await Assert.ThrowsAsync<AcessoNegadoException>(() => f.Delete(e, "other"));
        else await Assert.ThrowsAsync<AcessoNegadoException>(() => f.Update(e, user:"other"));
        Assert.Single(await f.Db.TimeEntries.ToListAsync());
        Assert.Empty(await f.Db.AuditLogs.Where(a => a.Action != "created").ToListAsync());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RunningTimerCannotBeEditedOrDeleted(bool delete)
    {
        await using var f = new Fixture();var e = await f.Entry(running:true);
        if (delete) await Assert.ThrowsAsync<DomainException>(() => f.Delete(e));
        else await Assert.ThrowsAsync<DomainException>(() => f.Update(e));
        Assert.Null((await f.Repo.GetByIdAsync(e.Id))!.EndedAt);
    }

    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(1)]
    public async Task InvalidIntervalOrFutureIsRejected(int kind)
    {
        await using var f = new Fixture();var e = await f.Entry();
        var start = kind == 1 ? DateTimeOffset.UtcNow.AddHours(1) : f.Start;
        var end = kind == 1 ? start.AddHours(1) : start.AddSeconds(kind);
        await Assert.ThrowsAsync<DomainException>(() => f.Update(e, start, end));
        f.Db.ChangeTracker.Clear();
        Assert.Equal(3600, (await f.Repo.GetByIdAsync(e.Id))!.DurationSeconds);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task OverlapWithClosedOrOpenEntryIsRejected(bool running)
    {
        await using var f = new Fixture();var e = await f.Entry();
        var other = running ? TimeEntry.IniciarAgora(f.Item.Id,"owner",null)
            : TimeEntry.Manual(f.Item.Id,"owner",f.Start.AddHours(2),f.Start.AddHours(3),null);
        await f.Repo.AddAsync(other);f.Db.ChangeTracker.Clear();
        var start = running ? other.StartedAt.AddMinutes(-5) : f.Start.AddMinutes(150);
        var end = running ? DateTimeOffset.UtcNow : f.Start.AddHours(4);
        await Assert.ThrowsAsync<DomainException>(() => f.Update(e,start,end));
    }

    [Fact]
    public async Task AdjacentIntervalsAreAllowedAndOwnIntervalIsIgnored()
    {
        await using var f = new Fixture();var e = await f.Entry();
        await f.Repo.AddAsync(TimeEntry.Manual(f.Item.Id,"owner",f.Start.AddHours(1),f.Start.AddHours(2),null));
        f.Db.ChangeTracker.Clear();
        Assert.Equal(3600,(await f.Update(e,end:f.Start.AddHours(1))).DurationSeconds);
    }

    [Fact]
    public async Task TaskAccessIsRequiredForBothMutations()
    {
        await using var f = new Fixture();var e = await f.Entry();f.Access.Deny=true;
        await Assert.ThrowsAsync<NaoEncontradoException>(() => f.Update(e));
        await Assert.ThrowsAsync<NaoEncontradoException>(() => f.Delete(e));
    }

    [Fact]
    public async Task DailyAndWeeklySplitAtSaoPauloMidnightWithoutSplittingStoredEntry()
    {
        await using var f = new Fixture();
        var date = new DateOnly(2026,1,12);
        var before = new DateTimeOffset(2026,1,12,22,0,0,TimeSpan.FromHours(-3));
        var entry = TimeEntry.Manual(f.Item.Id,"owner",before,before.AddHours(4),null);
        await f.Repo.AddAsync(entry);
        f.Db.ChangeTracker.Clear();
        var daily = new GetMyDailyByTaskQueryHandler(f.Repo);
        Assert.Equal(7200,Assert.Single(await daily.Handle(new("owner",date),default)).Seconds);
        Assert.Equal(7200,Assert.Single(await daily.Handle(new("owner",date.AddDays(1)),default)).Seconds);
        var weekly = await new GetMyWeeklyTimeQueryHandler(f.Repo).Handle(new("owner",date),default);
        Assert.Equal(7200,weekly.Days[0].TotalSeconds);Assert.Equal(7200,weekly.Days[1].TotalSeconds);
        Assert.Equal(14400, weekly.Days.Sum(d => d.TotalSeconds));
        var rows = new GetMyDailyEntriesQueryHandler(f.Repo);
        Assert.Equal(entry.Id, Assert.Single(await rows.Handle(new("owner", date), default)).Entry.Id);
        Assert.Equal(entry.Id, Assert.Single(await rows.Handle(new("owner", date.AddDays(1)), default)).Entry.Id);
        Assert.Single(await f.Db.TimeEntries.ToListAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(600)]
    public void DayClippingConservesSecondsIncludingFractionalBoundary(int milliseconds)
    {
        var date = new DateOnly(2026,1,12);
        var start = TimeTrackingDay.Start(date.AddDays(1)).AddMilliseconds(-1600 + milliseconds);
        var end = start.AddMilliseconds(2600);
        var first = TimeTrackingDay.SecondsInRange(start, end, TimeTrackingDay.Start(date), TimeTrackingDay.Start(date.AddDays(1)));
        var second = TimeTrackingDay.SecondsInRange(start, end, TimeTrackingDay.Start(date.AddDays(1)), TimeTrackingDay.Start(date.AddDays(2)));
        Assert.Equal((int)(end - start).TotalSeconds, first + second);
    }

    [Fact]
    public async Task AWeekIncludesOnlyItsShareOfEntryStartedInPreviousWeek()
    {
        await using var f = new Fixture();
        var monday = new DateOnly(2026,1,12);
        var start = TimeTrackingDay.Start(monday).AddHours(-2);
        await f.Repo.AddAsync(TimeEntry.Manual(f.Item.Id,"owner",start,start.AddHours(4),null));
        var week = await new GetMyWeeklyTimeQueryHandler(f.Repo).Handle(new("owner",monday),default);
        Assert.Equal(7200,week.Days[0].TotalSeconds);
        Assert.Equal(7200,week.Days.Sum(d=>d.TotalSeconds));
    }

    [Fact]
    public async Task CreationAndEditingUseSamePeriodRulesAndLegacyOverlapCanShrink()
    {
        await using var f = new Fixture();
        var first = await f.Entry();
        await Assert.ThrowsAsync<DomainException>(() => TimeEntryPeriod.ValidateAsync(f.Repo,"owner",f.Start.AddMinutes(30),f.Start.AddMinutes(90),null));
        await Assert.ThrowsAsync<DomainException>(() => TimeEntryPeriod.ValidateAsync(f.Repo,"owner",DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddHours(1),null));
        await TimeEntryPeriod.ValidateAsync(f.Repo,"owner",f.Start.AddHours(1),f.Start.AddHours(2),null);
        await f.Repo.AddAsync(TimeEntry.Manual(f.Item.Id,"owner",f.Start.AddHours(1),f.Start.AddHours(2),null));
        Assert.Equal(1800,(await f.Update(first)).DurationSeconds);
        await f.Update(first,end:f.Start.AddHours(1));
        // Dados legados podem conter sobreposição; manter horários/editar nota e reduzir continua possível.
        var legacy = TimeEntry.Manual(f.Item.Id,"owner",f.Start,f.Start.AddHours(1),null);
        await f.Repo.AddAsync(legacy);f.Db.ChangeTracker.Clear();
        Assert.Equal(3600,(await f.Update(first,end:f.Start.AddHours(1))).DurationSeconds);
        Assert.Equal(1800,(await f.Update(first,end:f.Start.AddMinutes(30))).DurationSeconds);
        await Assert.ThrowsAsync<DomainException>(() => f.Update(first,end:f.Start.AddHours(2)));
    }

    [Fact]
    public async Task DailyLimitUsesOnlyTwoHoursFromPreviousNightAndAllowsAdjacentTwentyTwoHours()
    {
        await using var f = new Fixture();
        var start = new DateTimeOffset(2026,1,12,22,0,0,TimeSpan.FromHours(-3));
        await f.Repo.AddAsync(TimeEntry.Manual(f.Item.Id,"owner",start,start.AddHours(4),null));
        await TimeEntryPeriod.ValidateAsync(f.Repo,"owner",start.AddHours(4),start.AddHours(26),null);
        // Legado que já supera 24h não pode receber tempo adicional, mesmo numa lacuna livre.
        var day = TimeTrackingDay.Start(new(2026,1,15));
        await f.Repo.AddAsync(TimeEntry.Manual(f.Item.Id,"owner",day.AddHours(1),day.AddHours(24),null));
        await f.Repo.AddAsync(TimeEntry.Manual(f.Item.Id,"owner",day.AddHours(1),day.AddHours(24),null));
        var error = await Assert.ThrowsAsync<DomainException>(() =>
            TimeEntryPeriod.ValidateAsync(f.Repo,"owner",day,day.AddHours(1),null));
        Assert.Contains("24 horas",error.Message);
    }

    [Fact]
    public async Task LegacyIntervalCanMoveToReduceOverlapButCannotCreateANewConflict()
    {
        await using var f = new Fixture();
        var entry = await f.Entry();
        await f.Repo.AddAsync(TimeEntry.Manual(f.Item.Id,"owner",f.Start,f.Start.AddHours(1),null));
        f.Db.ChangeTracker.Clear();
        Assert.Equal(3600,(await f.Update(entry,start:f.Start.AddMinutes(30),end:f.Start.AddMinutes(90))).DurationSeconds);
        await f.Repo.AddAsync(TimeEntry.Manual(f.Item.Id,"owner",f.Start.AddMinutes(105),f.Start.AddHours(2),null));
        f.Db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<DomainException>(() => f.Update(entry,start:f.Start.AddMinutes(45),end:f.Start.AddHours(2)));
    }

    [Fact]
    public void CardSnapshotIncludesOpenIntervalExactlyOnce()
    {
        var item = new WorkItem { Id=Guid.NewGuid() };
        var timer = TimeEntry.IniciarAgora(item.Id,"owner",null);
        typeof(TimeEntry).GetProperty(nameof(TimeEntry.StartedAt))!.SetValue(timer,DateTimeOffset.UtcNow.AddHours(-18));
        item.TimeEntries.Add(timer);
        var card = Assert.Single(WorkItemCardProjection.ToCards([item],"owner"));
        Assert.Equal((int)(card.TimeCalculatedAt-timer.StartedAt).TotalSeconds,card.UserTimeSeconds);
        Assert.Equal(card.UserTimeSeconds,card.TotalTimeSeconds);
    }
}
