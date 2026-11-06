using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.MeTime.Queries;
using Prisma.Workspace.Application.Features.TimeEntries;
using Prisma.Workspace.Application.Features.TimeEntries.Queries;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Repositories;

namespace Prisma.Workspace.Tests;

public class BrasiliaTimeAggregationTests
{
    [Theory]
    [InlineData("2026-10-12T23:59:30-03:00", "2026-10-13T00:00:30-03:00", 30, 30)]
    [InlineData("2026-10-12T22:00:00-03:00", "2026-10-13T02:00:00-03:00", 7200, 7200)]
    [InlineData("2026-10-12T23:59:30.3000000-03:00", "2026-10-13T00:00:30.7000000-03:00", 29, 31)]
    public async Task DailyAndWeeklyIncludeEarlierStartAndSplitWithoutChangingEntry(string from, string to, int first, int second)
    {
        await using var f = await Fixture.CreateAsync(from, to);
        var daily = new GetMyDailyByTaskQueryHandler(f.Repository);
        var monday = await daily.Handle(new("actor", new(2026, 10, 12)), default);
        var tuesday = await daily.Handle(new("actor", new(2026, 10, 13)), default);
        Assert.Equal(first, Assert.Single(monday).Seconds);
        Assert.Equal(second, Assert.Single(tuesday).Seconds);
        Assert.Equal("Tarefa própria", Assert.Single(tuesday).Title);
        var weekly = await new GetMyWeeklyTimeQueryHandler(f.Repository).Handle(new("actor", new(2026, 10, 12)), default);
        Assert.Equal([first, second, 0, 0, 0, 0, 0], weekly.Days.Select(day => day.TotalSeconds));
        var stored = await f.Db.TimeEntries.SingleAsync(entry => entry.UserId == "actor");
        Assert.Equal(DateTimeOffset.Parse(from), stored.StartedAt);
        Assert.Equal(DateTimeOffset.Parse(to), stored.EndedAt);
        Assert.Equal(stored.DurationSeconds, weekly.Days.Sum(day => day.TotalSeconds));
    }

    [Fact]
    public async Task SundayToMondayIsClippedToEachWeekWithoutLosingTheSecondHalf()
    {
        await using var f = await Fixture.CreateAsync("2026-10-18T23:59:30-03:00", "2026-10-19T00:00:30-03:00");
        var weekly = new GetMyWeeklyTimeQueryHandler(f.Repository);
        var previous = await weekly.Handle(new("actor", new(2026, 10, 12)), default);
        var next = await weekly.Handle(new("actor", new(2026, 10, 19)), default);
        Assert.Equal(30, previous.Days[6].TotalSeconds);
        Assert.Equal(30, next.Days[0].TotalSeconds);
        Assert.Equal(60, previous.Days.Sum(day => day.TotalSeconds) + next.Days.Sum(day => day.TotalSeconds));
    }

    [Fact]
    public async Task IntervalsTouchingDayBoundariesDoNotEnterTheAdjacentDay()
    {
        await using var f = await Fixture.CreateAsync("2026-10-12T22:00:00-03:00", "2026-10-13T00:00:00-03:00");
        Assert.Empty(await new GetMyDailyByTaskQueryHandler(f.Repository).Handle(new("actor", new(2026, 10, 13)), default));
        Assert.Empty(await f.Repository.GetByUserInRangeAsync("actor", TimeTrackingDay.Start(new(2026, 10, 13)), TimeTrackingDay.Start(new(2026, 10, 14))));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public AppDbContext Db { get; private set; } = null!;
        public TimeEntryRepository Repository => new(Db);
        public static async Task<Fixture> CreateAsync(string from, string to)
        {
            var org = Guid.NewGuid();
            var f = new Fixture { Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new Tenant(org)) };
            var project = Project.Criar("QA", "Oficina", "actor", WorkNature.Project, WorkType.Development);
            project.OrganizationId = org;
            var board = new Board { Id = Guid.NewGuid(), OrganizationId = org, ProjectId = project.Id,
                Project = project, OwnerId = "actor", Name = "Quadro", CreatedAt = DateTimeOffset.UtcNow };
            var item = new WorkItem { Id = Guid.NewGuid(), BoardId = board.Id, Board = board, Title = "Tarefa própria",
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            var entry = TimeEntry.Manual(item.Id, "actor", DateTimeOffset.Parse(from), DateTimeOffset.Parse(to), null);
            entry.WorkItem = item;
            var other = TimeEntry.Manual(item.Id, "other", DateTimeOffset.Parse(from), DateTimeOffset.Parse(to), null);
            other.WorkItem = item;
            f.Db.AddRange(new Organization { Id = org, Name = "Oficina", Slug = org.ToString("N") }, project, board, item, entry, other);
            await f.Db.SaveChangesAsync();
            f.Db.ChangeTracker.Clear();
            return f;
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
    private sealed class Tenant(Guid id) : IOrganizationContext { public Guid? OrganizationId => id; }
}
