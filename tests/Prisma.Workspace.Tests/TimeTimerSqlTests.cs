using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Repositories;

namespace Prisma.Workspace.Tests;

public class TimeTimerSqlTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParallelStartsAcrossIndependentContextsLeaveOneOpen(bool differentTasks)
    {
        var fixture = await Fixture.CreateAsync();
        for (var round = 0; round < 3; round++)
        {
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var starts = Enumerable.Range(0, 2).Select(async index =>
            {
                await using var db = fixture.Open();
                await ready.Task;
                return await new TimeEntryRepository(db).StartTimerAsync(
                    index == 1 && differentTasks ? fixture.Second.Id : fixture.First.Id, fixture.UserId, "Paralelo");
            }).ToArray();
            ready.SetResult();
            var responses = await Task.WhenAll(starts);
            await using var read = fixture.Open();
            var rows = await read.TimeEntries.Where(e => e.UserId == fixture.UserId).ToListAsync();
            var active = Assert.Single(rows, e => e.EndedAt == null);
            Assert.Contains(active.Id, responses.Select(e => e.Id));
            if (differentTasks)
            {
                Assert.Equal(2, responses.Select(e => e.Id).Distinct().Count());
                var previous = Assert.Single(rows, e => responses.Any(response => response.Id == e.Id) && e.Id != active.Id);
                Assert.NotNull(previous.EndedAt);
                Assert.True(previous.EndedAt <= active.StartedAt);
            }
            else Assert.Equal(responses[0].Id, responses[1].Id);
            await new TimeEntryRepository(read).StopTimerAsync(fixture.UserId, active.WorkItemId, null);
        }
    }

    [Fact]
    public async Task StartRetainsSameTaskAndClosesAllLegacyDuplicatesWithAudit()
    {
        var fixture = await Fixture.CreateAsync();
        var old = await fixture.SeedAsync(fixture.First.Id, fixture.UserId, "Anterior");
        var otherTask = await fixture.SeedAsync(fixture.Second.Id, fixture.UserId, "Outra tarefa");
        var retained = await fixture.SeedAsync(fixture.First.Id, fixture.UserId, "Preservada");
        await using (var db = fixture.Open())
        {
            var result = await new TimeEntryRepository(db).StartTimerAsync(fixture.First.Id, fixture.UserId, "Não substituir");
            Assert.Equal(retained.Id, result.Id);
            Assert.Equal("Preservada", result.Note);
        }
        await using var read = fixture.Open();
        var rows = await read.TimeEntries.ToListAsync();
        Assert.Equal(retained.Id, Assert.Single(rows, e => e.EndedAt == null).Id);
        Assert.All(rows.Where(e => e.Id != retained.Id), entry => Assert.NotNull(entry.EndedAt));
        var audit = await read.AuditLogs.Where(a => a.EntityType == nameof(TimeEntry) && a.Action == "updated").ToListAsync();
        Assert.Equal(new[] { old.Id.ToString(), otherTask.Id.ToString() }.Order(), audit.Select(a => a.EntityId).Order());
        Assert.All(audit, entry =>
        {
            Assert.Equal(fixture.UserId, entry.UserId);
            Assert.Equal(fixture.OrganizationId, entry.OrganizationId);
            Assert.Equal(JsonValueKind.Null, JsonDocument.Parse(entry.PreviousValuesJson!).RootElement.GetProperty("EndedAt").ValueKind);
            Assert.Equal(JsonValueKind.String, JsonDocument.Parse(entry.NewValuesJson!).RootElement.GetProperty("EndedAt").ValueKind);
        });
    }

    [Fact]
    public async Task StartOnAnotherTaskClosesEveryLegacyTimerAndPreservesOtherUsers()
    {
        var fixture = await Fixture.CreateAsync();
        await fixture.SeedAsync(fixture.First.Id, fixture.UserId, "Primeiro");
        await fixture.SeedAsync(fixture.First.Id, fixture.UserId, "Duplicado");
        var colleague = await fixture.SeedAsync(fixture.First.Id, "other-" + fixture.UserId, "Colega");
        await using var db = fixture.Open();
        var result = await new TimeEntryRepository(db).StartTimerAsync(fixture.Second.Id, fixture.UserId, "Novo");
        db.ChangeTracker.Clear();
        var rows = await db.TimeEntries.Where(e => e.UserId == fixture.UserId).ToListAsync();
        Assert.Equal(3, rows.Count);
        Assert.Equal(result.Id, Assert.Single(rows, e => e.EndedAt == null).Id);
        Assert.Equal(fixture.Second.Id, result.WorkItemId);
        Assert.Null((await db.TimeEntries.SingleAsync(e => e.Id == colleague.Id)).EndedAt);
    }

    [Fact]
    public async Task StopRepairsLegacyTimersWithoutChangingAnotherTenantOrUser()
    {
        var fixture = await Fixture.CreateAsync();
        var otherTenant = await Fixture.CreateAsync(fixture.UserId);
        await fixture.SeedAsync(fixture.First.Id, fixture.UserId, "Oculto");
        var current = await fixture.SeedAsync(fixture.Second.Id, fixture.UserId, "Atual");
        var colleague = await fixture.SeedAsync(fixture.First.Id, "other-" + fixture.UserId, "Colega");
        var foreign = await otherTenant.SeedAsync(otherTenant.First.Id, fixture.UserId, "Outro tenant");
        await using (var db = fixture.Open())
        {
            var stopped = await new TimeEntryRepository(db).StopTimerAsync(fixture.UserId, current.WorkItemId, "Encerrado");
            Assert.Equal(current.Id, stopped.Id);
            Assert.Equal("Encerrado", stopped.Note);
            db.ChangeTracker.Clear();
            Assert.Empty(await db.TimeEntries.Where(e => e.UserId == fixture.UserId && e.EndedAt == null).ToListAsync());
            Assert.Null((await db.TimeEntries.SingleAsync(e => e.Id == colleague.Id)).EndedAt);
            Assert.All(await db.AuditLogs.ToListAsync(), entry => Assert.Equal(fixture.OrganizationId, entry.OrganizationId));
        }
        await using var other = otherTenant.Open();
        Assert.Null((await other.TimeEntries.SingleAsync(e => e.Id == foreign.Id)).EndedAt);
        Assert.Empty(await other.AuditLogs.Where(a => a.Action == "updated").ToListAsync());
    }

    [Fact]
    public async Task WrongCardCannotStopTheRunningTimer()
    {
        var fixture = await Fixture.CreateAsync();
        var current = await fixture.SeedAsync(fixture.First.Id, fixture.UserId, "Atual");
        await using var db = fixture.Open();
        var error = await Assert.ThrowsAsync<DomainException>(() => new TimeEntryRepository(db)
            .StopTimerAsync(fixture.UserId, fixture.Second.Id, null));
        Assert.Equal("O timer em andamento pertence a outro card.", error.Message);
        db.ChangeTracker.Clear();
        Assert.Null((await db.TimeEntries.SingleAsync(e => e.Id == current.Id)).EndedAt);
        Assert.Empty(await db.AuditLogs.Where(a => a.Action == "updated").ToListAsync());
    }

    [Fact]
    public async Task FailedInsertRollsBackLegacyClosuresAndAudit()
    {
        var fixture = await Fixture.CreateAsync();
        await fixture.SeedAsync(fixture.First.Id, fixture.UserId, "Anterior");
        await fixture.SeedAsync(fixture.First.Id, fixture.UserId, "Duplicado");
        await using var db = fixture.Open();
        await Assert.ThrowsAsync<DbUpdateException>(() => new TimeEntryRepository(db)
            .StartTimerAsync(Guid.NewGuid(), fixture.UserId, "FK inexistente"));
        db.ChangeTracker.Clear();
        var rows = await db.TimeEntries.ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, entry => Assert.Null(entry.EndedAt));
        Assert.Empty(await db.AuditLogs.Where(a => a.Action == "updated").ToListAsync());
    }

    private sealed class Fixture
    {
        private readonly string connection;
        public Guid OrganizationId { get; } = Guid.NewGuid();
        public string UserId { get; }
        public WorkItem First { get; private set; } = null!;
        public WorkItem Second { get; private set; } = null!;
        private Fixture(string value, string? userId) { connection = value; UserId = userId ?? "timer-" + Guid.NewGuid().ToString("N"); }
        public AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options,
            new Tenant(OrganizationId), new Audit(UserId));

        public static async Task<Fixture> CreateAsync(string? userId = null)
        {
            var value = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            Assert.False(string.IsNullOrWhiteSpace(value), "Configure o banco E2E exclusivo.");
            Assert.StartsWith("Prisma_BoardColumns_", new SqlConnectionStringBuilder(value).InitialCatalog);
            var fixture = new Fixture(value!, userId);
            await using var db = fixture.Open();
            var project = Project.Criar("T" + Guid.NewGuid().ToString("N")[..8], "Timer SQL", fixture.UserId, WorkNature.Project, WorkType.Development);
            project.OrganizationId = fixture.OrganizationId;
            var board = new Board { Id = Guid.NewGuid(), ProjectId = project.Id, OrganizationId = fixture.OrganizationId, Name = "Timer", OwnerId = fixture.UserId };
            var stage = new Stage { Id = Guid.NewGuid(), ProjectId = project.Id, BoardId = board.Id, Name = "Etapa", Category = StageCategory.InProgress };
            fixture.First = new WorkItem { Id = Guid.NewGuid(), BoardId = board.Id, StageId = stage.Id, Title = "Primeira" };
            fixture.Second = new WorkItem { Id = Guid.NewGuid(), BoardId = board.Id, StageId = stage.Id, Title = "Segunda" };
            db.AddRange(new Organization { Id = fixture.OrganizationId, Name = "Timer SQL", Slug = "timer-" + fixture.OrganizationId.ToString("N") },
                project, board, stage, fixture.First, fixture.Second);
            await db.SaveChangesAsync();
            return fixture;
        }

        public async Task<TimeEntry> SeedAsync(Guid itemId, string userId, string note)
        {
            await using var db = Open();
            var entry = TimeEntry.IniciarAgora(itemId, userId, note);
            db.TimeEntries.Add(entry);
            await db.SaveChangesAsync();
            return entry;
        }
    }
    private sealed class Tenant(Guid id) : IOrganizationContext { public Guid? OrganizationId => id; }
    private sealed class Audit(string userId) : IAuditContext
    {
        public string UserId => userId;
        public string Origin => "api";
        public string? IpAddress => null;
        public string CorrelationId => "timer-sql";
    }
}
