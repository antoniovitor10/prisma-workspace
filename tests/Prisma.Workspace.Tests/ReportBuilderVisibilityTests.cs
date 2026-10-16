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

public class ReportBuilderVisibilityTests
{
    private sealed class Tenant(Guid id) : IOrganizationContext { public Guid? OrganizationId { get; set; } = id; }
    private sealed class Names : IUserDirectory
    {
        private static readonly string[] Ids = ["actor", "colleague", "outsider", "idle"];
        public Task<IReadOnlyList<UserSummary>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<UserSummary>>(Ids.Select(id => new UserSummary(id, null, null, id)).ToList());
        public Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(IEnumerable<string> ids, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(ids.ToDictionary(id => id, id => id));
        public Task<IReadOnlyList<UserSummary>> GetByIdsAsync(IEnumerable<string> ids, bool includeInactive = false, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UserSummary?> GetByIdAsync(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UserSummary?> GetByIdUnscopedAsync(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UserSummary?> GetByEmailAsync(string email, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly Tenant _tenant = new(Guid.NewGuid());
        public Guid OrganizationId => _tenant.OrganizationId!.Value;
        public AppDbContext Db { get; }
        public Team Managed { get; }
        public Project Shared { get; }
        public Project Hidden { get; }
        public ReportExecutor Executor { get; }
        public AnalyticsRepository Analytics { get; }
        public PermissionService Permissions { get; }
        public ProjectAccessService Access { get; }
        public ProjectRepository Projects { get; }
        public Fixture(OrganizationRole role)
        {
            Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"builder-visibility-{Guid.NewGuid()}").Options, _tenant);
            Db.OrganizationMembers.Add(OrganizationMember.Create(OrganizationId, "actor", role));
            Managed = new Team { Id=Guid.NewGuid(), OrganizationId=OrganizationId, Name="Gerenciada", LeaderId="actor" };
            var other = new Team { Id=Guid.NewGuid(), OrganizationId=OrganizationId, Name="Outra", LeaderId="outsider" };
            Managed.Members.Add(new TeamMember { TeamId=Managed.Id, UserId="idle" });
            Db.Teams.AddRange(Managed, other);
            Shared = Project("Compartilhado", "actor");
            Hidden = Project("Sem acesso", "outsider");
            var own = Item(Shared, Managed, "Tarefa própria", "actor");
            Entry(own, "actor", 9, 1);
            Entry(own, "colleague", 9, 2); // Mesma tarefa: horas embutidas também precisam do recorte.
            Entry(Item(Shared, other, "Tarefa de outra equipe", "outsider"), "outsider", 10, 3);
            Entry(Item(Hidden, Managed, "Projeto inacessível", "actor"), "actor", 14, 4);
            Db.SaveChanges(); Db.ChangeTracker.Clear();
            var organizationId = OrganizationId;
            _tenant.OrganizationId = Guid.NewGuid();
            Entry(Item(Project("Outro tenant", "foreign"), null, "Sigilosa", "foreign"), "foreign", 0, 20);
            Db.SaveChanges(); Db.ChangeTracker.Clear(); _tenant.OrganizationId = organizationId;
            Analytics = new(Db); Projects = new(Db); Permissions = new(Db, _tenant); Access = new(Db, Permissions);
            Executor = new(Analytics, new Names(), Projects, _tenant, new OrganizationRepository(Db),
                new TeamRepository(Db), Permissions, Access);
        }
        private Project Project(string name, string owner)
        {
            var p = new Project { Id=Guid.NewGuid(), OrganizationId=OrganizationId, Name=name, Key=Guid.NewGuid().ToString("N"), OwnerId=owner };
            Db.Projects.Add(p); return p;
        }
        private WorkItem Item(Project p, Team? team, string title, string responsible)
        {
            var board = new Board { Id=Guid.NewGuid(), OrganizationId=OrganizationId, ProjectId=p.Id, Project=p, Name=title, OwnerId=p.OwnerId };
            var item = new WorkItem { Id=Guid.NewGuid(), BoardId=board.Id, Board=board, Title=title,
                ResponsibleId=responsible, CreatedBy=responsible, CreatedAt=new(2026,1,12,12,0,0,TimeSpan.Zero), TeamId=team?.Id, Team=team };
            Db.WorkItems.Add(item); return item;
        }
        private void Entry(WorkItem item, string user, int hour, int duration)
        {
            var start = new DateTimeOffset(2026,1,12,hour,0,0,TimeSpan.FromHours(-3));
            var entry = TimeEntry.Manual(item.Id,user,start,start.AddHours(duration),$"Nota {user}");
            entry.WorkItem=item; Db.TimeEntries.Add(entry);
        }
        public Task<ReportExecutionDto> Run(ReportDataSource source, ReportDefinitionDto? definition = null, Guid? projectId = null)
            => Executor.RunAsync(projectId,source,ReportVisualization.Table,definition ?? Definition(source),"actor",default);
        public static ReportDefinitionDto Definition(ReportDataSource source)
            => new(source == ReportDataSource.TimeEntries ? ["userId","task","hours","note"]
                : source == ReportDataSource.Users ? ["userId","realizedHours"]
                : ["title","realizedHours"],[],[],null,null,false,new(2026,1,12),new(2026,1,12));
        public async Task Deny(PermissionScope scope, Guid? id = null)
        {
            Db.PermissionGrants.Add(new PermissionGrant { Id=Guid.NewGuid(), OrganizationId=OrganizationId,
                UserId="actor", Permission=scope == PermissionScope.Project ? PlatformPermission.View : PlatformPermission.ViewReport,
                Scope=scope, ScopeId=id, IsAllowed=false, GrantedBy="admin" });
            await Db.SaveChangesAsync(); Db.ChangeTracker.Clear();
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    [Theory]
    [InlineData(OrganizationRole.Administrator)]
    [InlineData(OrganizationRole.Manager)]
    public async Task AuthorizedUserWithoutEntriesRemainsVisibleWithZeroHours(OrganizationRole role)
    {
        await using var f = new Fixture(role);
        var users = await f.Run(ReportDataSource.Users, Fixture.Definition(ReportDataSource.Users) with { Columns=["userId","realizedHours","assignedTasks"] });
        var idle = Assert.Single(users.Rows,r => (string)r["userId"]! == "idle");
        Assert.Equal(0m,idle["realizedHours"]);
        Assert.Equal(0,idle["assignedTasks"]);
    }

    [Theory]
    [InlineData(ReportDataSource.Projects)]
    [InlineData(ReportDataSource.Teams)]
    [InlineData(ReportDataSource.Sprints)]
    public async Task OrganizationSourcesExcludeInaccessibleProjectsIncludingTeamCounts(ReportDataSource source)
    {
        await using var f = new Fixture(OrganizationRole.Viewer);
        var team = await f.Db.Teams.SingleAsync(t=>t.Id==f.Managed.Id);
        foreach (var project in await f.Db.Projects.ToListAsync())
        {
            f.Db.Add(new ProjectTeam { ProjectId=project.Id, TeamId=team.Id, Project=project, Team=team });
            f.Db.Sprints.Add(new Sprint { Id=Guid.NewGuid(), ProjectId=project.Id, Project=project,
                TeamId=team.Id, Team=team, Name=project.Name, StartDate=new(2026,1,12), EndDate=new(2026,1,13) });
        }
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var definition = new ReportDefinitionDto(source == ReportDataSource.Teams ? ["team","projectCount"] : ["project"],
            [],[],null,null,false,null,null);
        var row = Assert.Single((await f.Run(source,definition)).Rows);
        if (source == ReportDataSource.Teams) Assert.Equal(1,row["projectCount"]);
        else Assert.Equal(f.Shared.Name,row["project"]);
    }

    [Theory]
    [InlineData(OrganizationRole.Viewer, 1)]
    [InlineData(OrganizationRole.Client, 1)]
    [InlineData(OrganizationRole.ProductOwner, 1)]
    [InlineData(OrganizationRole.ScrumMaster, 1)]
    [InlineData(OrganizationRole.ProjectManager, 5)]
    [InlineData(OrganizationRole.Manager, 7)]
    [InlineData(OrganizationRole.Administrator, 10)]
    public async Task TimeRowsAndUserAggregatesRespectRoleProjectAndTenant(OrganizationRole role, int expectedHours)
    {
        await using var f = new Fixture(role);
        var entries = await f.Run(ReportDataSource.TimeEntries);
        Assert.Equal(expectedHours, entries.Rows.Sum(r => (decimal)r["hours"]!));
        Assert.DoesNotContain(entries.Rows,r => (string)r["userId"]! == "foreign");
        var users = await f.Run(ReportDataSource.Users);
        Assert.Equal(expectedHours, users.Rows.Sum(r => (decimal)r["realizedHours"]!));
        if (role is not (OrganizationRole.Administrator or OrganizationRole.Manager))
        {
            Assert.All(entries.Rows,r => Assert.Equal("actor",r["userId"]));
            Assert.Equal("actor", Assert.Single(users.Rows)["userId"]);
        }
        if (role == OrganizationRole.Manager)
            Assert.DoesNotContain(entries.Rows,r => (string)r["userId"]! == "outsider");
    }

    [Theory]
    [InlineData(OrganizationRole.Viewer)]
    [InlineData(OrganizationRole.Client)]
    [InlineData(OrganizationRole.ProductOwner)]
    public async Task SameTaskHoursAndMetricsCannotReintroduceColleague(OrganizationRole role)
    {
        await using var f = new Fixture(role);
        var tasks = await f.Run(ReportDataSource.WorkItems);
        Assert.Equal(1m,Assert.Single(tasks.Rows)["realizedHours"]);
        var definition = Fixture.Definition(ReportDataSource.TimeEntries) with {
            Columns=[], GroupBy="userId", Metrics=[new(ReportMetricOperation.Sum,"hours","Horas")] };
        var result = await f.Run(ReportDataSource.TimeEntries, definition);
        var row = Assert.Single(result.Rows);
        Assert.Equal("actor",row["userId"]);
        Assert.Equal(1m,row.Single(p=>p.Key.StartsWith("metric:")).Value);
        Assert.Equal(1,result.SourceRowCount);
        var filtered = await f.Run(ReportDataSource.TimeEntries, definition with { Filters=[new("userId","eq","colleague")] });
        Assert.Empty(filtered.Rows);
    }

    [Theory]
    [InlineData(OrganizationRole.Manager, 4)]
    [InlineData(OrganizationRole.Administrator, 4)]
    public async Task ExplicitProjectDenialRemovesHoursBeforeAggregation(OrganizationRole role, int expectedHours)
    {
        await using var f = new Fixture(role);
        await f.Deny(PermissionScope.Project,f.Shared.Id);
        Assert.Equal(expectedHours,(await f.Run(ReportDataSource.TimeEntries)).Rows.Sum(r=>(decimal)r["hours"]!));
    }

    [Theory]
    [InlineData(OrganizationRole.Manager, 0)]
    [InlineData(OrganizationRole.Administrator, 3)]
    public async Task ExplicitTeamDenialRemovesTimeRowsAndUserTotals(OrganizationRole role, int expectedHours)
    {
        await using var f = new Fixture(role);
        await f.Deny(PermissionScope.Team,f.Managed.Id);
        Assert.Equal(expectedHours,(await f.Run(ReportDataSource.TimeEntries)).Rows.Sum(r=>(decimal)r["hours"]!));
        Assert.Equal(expectedHours,(await f.Run(ReportDataSource.Users)).Rows.Sum(r=>(decimal)r["realizedHours"]!));
    }

    [Theory]
    [InlineData(OrganizationRole.Viewer)]
    [InlineData(OrganizationRole.Client)]
    [InlineData(OrganizationRole.ProductOwner)]
    [InlineData(OrganizationRole.Manager)]
    [InlineData(OrganizationRole.Administrator)]
    public async Task SavedSharedAndAdHocReportsApplyTheCurrentActorScope(OrganizationRole role)
    {
        await using var f = new Fixture(role);
        var report = SavedReport.Create(f.OrganizationId,null,"colleague","Compartilhado",
            ReportDataSource.TimeEntries,ReportVisualization.Table,
            System.Text.Json.JsonSerializer.Serialize(Fixture.Definition(ReportDataSource.TimeEntries),new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),true);
        f.Analytics.AddSavedReport(report); await f.Analytics.SaveAsync();
        var saved = await new RunSavedReportQueryHandler(f.Analytics,f.Executor,f.Permissions,f.Access)
            .Handle(new(report.Id,"actor"),default);
        var preview = await new RunAdHocReportQueryHandler(f.Executor,f.Permissions,f.Access,f.Projects)
            .Handle(new(null,ReportDataSource.TimeEntries,ReportVisualization.Table,Fixture.Definition(ReportDataSource.TimeEntries),"actor"),default);
        Assert.Equal((await f.Run(ReportDataSource.TimeEntries)).Rows.Select(r => r["userId"]),saved.Rows.Select(r => r["userId"]));
        Assert.Equal(saved.Rows.Select(r => r["hours"]),preview.Rows.Select(r => r["hours"]));
    }

    [Theory]
    [InlineData(OrganizationRole.ExternalRequester)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task InactiveExternalAndDeniedReportAreRejected(OrganizationRole role)
    {
        await using var f = new Fixture(role);
        if (role == OrganizationRole.Viewer) await f.Deny(PermissionScope.Report);
        await Assert.ThrowsAsync<AcessoNegadoException>(()=>f.Run(ReportDataSource.TimeEntries));
        var member = await f.Db.OrganizationMembers.SingleAsync(); member.IsActive=false;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<AcessoNegadoException>(()=>f.Run(ReportDataSource.Users));
    }
}
