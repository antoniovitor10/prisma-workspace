using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Features.Reports;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Identity;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Repositories;

namespace Prisma.Workspace.Tests;

public class OrganizationHoursVisibilityTests
{
    private sealed class Tenant(Guid id) : IOrganizationContext { public Guid? OrganizationId { get; set; } = id; }
    private sealed class Names : IUserDirectory
    {
        public Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(IEnumerable<string> ids, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(ids.ToDictionary(id => id, id => $"Nome {id}"));
        public Task<IReadOnlyList<UserSummary>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<UserSummary>>([new("actor",null,null,"Nome actor")]);
        public Task<IReadOnlyList<UserSummary>> GetByIdsAsync(IEnumerable<string> ids, bool includeInactive = false, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UserSummary?> GetByIdAsync(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UserSummary?> GetByIdUnscopedAsync(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UserSummary?> GetByEmailAsync(string email, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid OrgId { get; } = Guid.NewGuid();
        public AppDbContext Db { get; }
        public Team Managed { get; }
        public Team Other { get; }
        public GetOrganizationHoursReportQueryHandler Handler { get; }
        public Fixture(OrganizationRole role)
        {
            var tenant = new Tenant(OrgId);
            Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"hours-report-{Guid.NewGuid()}").Options, tenant);
            Db.OrganizationMembers.Add(OrganizationMember.Create(OrgId, "actor", role));
            Managed = new Team { Id = Guid.NewGuid(), OrganizationId = OrgId, Name = "Gerenciada", LeaderId = "actor" };
            Other = new Team { Id = Guid.NewGuid(), OrganizationId = OrgId, Name = "Outra", LeaderId = "elsewhere" };
            Db.Teams.AddRange(Managed, Other);
            AddEntry("actor", Managed, OrgId);
            AddEntry("colleague", Managed, OrgId);
            AddEntry("outsider", Other, OrgId);
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
            tenant.OrganizationId = Guid.NewGuid();
            AddEntry("foreign", null, tenant.OrganizationId.Value);
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
            tenant.OrganizationId = OrgId;
            Handler = new(new ReportRepository(Db), new Names(), new OrganizationRepository(Db), tenant,
                new TeamRepository(Db), new PermissionService(Db, tenant));
        }

        private void AddEntry(string user, Team? team, Guid orgId)
        {
            var project = new Project { Id = Guid.NewGuid(), OrganizationId = orgId, Name = $"Projeto {user}", Key = user, OwnerId = user };
            var board = new Board { Id = Guid.NewGuid(), OrganizationId = orgId, Name = "Quadro", ProjectId = project.Id, Project = project, OwnerId = user };
            var item = new WorkItem { Id = Guid.NewGuid(), BoardId = board.Id, Board = board, Title = user, CreatedBy = user,
                TeamId = team?.Id, Team = team };
            var start = new DateTimeOffset(2026, 1, 12, 12, 0, 0, TimeSpan.Zero);
            var entry = TimeEntry.Manual(item.Id, user, start, start.AddHours(1), null);
            entry.WorkItem = item;
            Db.TimeEntries.Add(entry);
        }
        public Task<OrganizationHoursReportDto> Report(string? user = null, Guid? team = null)
            => Handler.Handle(new(new(2026, 1, 12), new(2026, 1, 12), user, team, "actor"), default);
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    [Theory]
    [InlineData(OrganizationRole.TeamMember)]
    [InlineData(OrganizationRole.Developer)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task CommonUserOnlySeesOwnEntriesBeforeAggregation(OrganizationRole role)
    {
        await using var f = new Fixture(role);
        var result = await f.Report();
        Assert.Equal("actor", Assert.Single(result.ByResponsible).UserId);
        Assert.Equal(1m, result.TotalHours);
        Assert.Equal("Projeto actor", Assert.Single(result.ByResponsible[0].Projects).ProjectName);
        Assert.Empty((await f.Report("colleague")).ByResponsible);
        Assert.Equal(0m, (await f.Report("colleague")).TotalHours);
        Assert.Empty((await f.Report(team: f.Other.Id)).ByResponsible);
    }

    [Fact]
    public async Task AdministratorSeesOrganizationButNotOtherTenant()
    {
        await using var f = new Fixture(OrganizationRole.Administrator);
        var result = await f.Report();
        Assert.Equal(new[] { "actor", "colleague", "outsider" }, result.ByResponsible.Select(r => r.UserId).Order());
        Assert.Equal(3m, result.TotalHours);
        Assert.Equal("outsider", Assert.Single((await f.Report("outsider")).ByResponsible).UserId);
    }

    [Fact]
    public async Task ManagerOnlySeesManagedTeamsEvenWhenOtherTeamOrUserIsRequested()
    {
        await using var f = new Fixture(OrganizationRole.Manager);
        var result = await f.Report();
        Assert.Equal(new[] { "actor", "colleague" }, result.ByResponsible.Select(r => r.UserId).Order());
        Assert.Equal(2m, result.TotalHours);
        Assert.Empty((await f.Report(team: f.Other.Id)).ByResponsible);
        Assert.Empty((await f.Report("outsider")).ByResponsible);
    }

    [Fact]
    public async Task InactiveAndExternalMembersCannotReadHours()
    {
        await using var f = new Fixture(OrganizationRole.ExternalRequester);
        await Assert.ThrowsAsync<AcessoNegadoException>(() => f.Report());
        var member = await f.Db.OrganizationMembers.SingleAsync();
        member.IsActive = false;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<AcessoNegadoException>(() => f.Report());
    }

    [Fact]
    public async Task OrganizationReportClipsBothDaysInBrasiliaAndConservesTotal()
    {
        await using var f = new Fixture(OrganizationRole.TeamMember);
        var entry = await f.Db.TimeEntries.SingleAsync(e => e.UserId == "actor");
        var start = new DateTimeOffset(2026,1,12,22,0,0,TimeSpan.FromHours(-3));
        entry.Correct(start,start.AddHours(4),null);
        await f.Db.SaveChangesAsync();
        Assert.Equal(2m,(await f.Report()).TotalHours);
        var next = await f.Handler.Handle(new(new(2026,1,13),new(2026,1,13),null,null,"actor"),default);
        Assert.Equal(2m,next.TotalHours);
        var both = await f.Handler.Handle(new(new(2026,1,12),new(2026,1,13),null,null,"actor"),default);
        Assert.Equal(4m,both.TotalHours);
        var executor = new ReportExecutor(new AnalyticsRepository(f.Db), new Names(), new ProjectRepository(f.Db));
        foreach (var date in new[] { new DateOnly(2026,1,12), new DateOnly(2026,1,13) })
        {
            var definition = new ReportDefinitionDto(["userId","hours"], [], [new("userId","eq","actor")],
                null, null, false, date, date);
            var report = await executor.RunAsync(null, ReportDataSource.TimeEntries, ReportVisualization.Table, definition, default);
            Assert.Equal(2m, Assert.Single(report.Rows)["hours"]);
        }
    }

    [Theory]
    [InlineData(OrganizationRole.Manager)]
    [InlineData(OrganizationRole.Administrator)]
    public async Task ExplicitTeamDenialAppliesBeforeAggregation(OrganizationRole role)
    {
        await using var f = new Fixture(role);
        f.Db.PermissionGrants.Add(new PermissionGrant { Id=Guid.NewGuid(), OrganizationId=f.OrgId,
            UserId="actor", Permission=PlatformPermission.ViewReport, Scope=PermissionScope.Team,
            ScopeId=f.Managed.Id, IsAllowed=false, GrantedBy="admin" });
        await f.Db.SaveChangesAsync();
        Assert.DoesNotContain((await f.Report()).ByResponsible,r=>r.UserId is "actor" or "colleague");
    }
}
