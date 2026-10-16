using Prisma.Workspace.Application.Services;
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
        public HoursReadAccessService Visibility { get; }
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
            Visibility = new(_tenant,new OrganizationRepository(Db),new TeamRepository(Db),Access);
            Executor = new(Analytics,new Names(),Projects,Permissions,Access,Visibility);
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
    [InlineData(OrganizationRole.TeamMember, 5)]
    [InlineData(OrganizationRole.Viewer, 5)]
    [InlineData(OrganizationRole.Client, 5)]
    [InlineData(OrganizationRole.Manager, 7)]
    [InlineData(OrganizationRole.Administrator, 10)]
    public async Task PersonalReadScopeAndQueryPredicateAgreeBeforeSerializing(OrganizationRole role, int hours)
    {
        await using var f = new Fixture(role);
        var scope = await f.Visibility.GetScopeAsync("actor", false);
        var all = await f.Db.TimeEntries.Include(e => e.WorkItem).ThenInclude(i => i.Board).ToListAsync();
        var filtered = all.Where(scope.CanRead).OrderBy(e => e.Id).ToList();
        var queried = await f.Db.TimeEntries.Where(scope.EntryPredicate()).OrderBy(e => e.Id).ToListAsync();
        Assert.Equal(filtered.Select(e => e.Id), queried.Select(e => e.Id));
        Assert.Equal(hours, filtered.Sum(e => (e.EndedAt!.Value - e.StartedAt).TotalHours));
        if (role is not (OrganizationRole.Administrator or OrganizationRole.Manager))
            Assert.All(filtered, e => Assert.Equal("actor", e.UserId));
    }

    [Theory]
    [InlineData(OrganizationRole.Viewer,1,1)]
    [InlineData(OrganizationRole.Client,1,1)]
    [InlineData(OrganizationRole.ProductOwner,1,1)]
    [InlineData(OrganizationRole.Manager,7,3)]
    [InlineData(OrganizationRole.Administrator,10,6)]
    public async Task PreparedAndProjectTimeUseSamePopulationAsBuilder(OrganizationRole role,int globalHours,int projectHours)
    {
        await using var f = new Fixture(role);
        var filters = new AnalyticsFiltersDto(null,null,null,null,null,new(2026,1,12),new(2026,1,12));
        var prepared = await new GetPreparedReportsQueryHandler(f.Analytics,new Names(),f.Permissions,f.Access,f.Visibility)
            .Handle(new(filters,"actor"),default);
        Assert.Equal(globalHours,prepared.Hours.Realized);
        var project = await new GetTimeReportQueryHandler(new ReportRepository(f.Db),f.Projects,f.Access,new Names(),f.Visibility)
            .Handle(new(f.Shared.Id,new(2026,1,12),new(2026,1,12),null,null,"actor"),default);
        Assert.Equal(projectHours,project.Project.RealizedHours);
        if(role is not (OrganizationRole.Administrator or OrganizationRole.Manager))
        {
            Assert.All(project.Entries,e=>Assert.Equal("actor",e.UserId));
            var shared = await f.Db.Projects.Include(p=>p.Members).SingleAsync(p=>p.Id==f.Shared.Id);
            shared.Members.Add(new ProjectMember { ProjectId=shared.Id,UserId="outsider",Role=ProjectRole.Member });
            await f.Db.SaveChangesAsync();
            var other = await new GetTimeReportQueryHandler(new ReportRepository(f.Db),f.Projects,f.Access,new Names(),f.Visibility)
                .Handle(new(f.Shared.Id,new(2026,1,12),new(2026,1,12),null,"outsider","actor"),default);
            Assert.Empty(other.Entries);
        }
    }

    [Fact]
    public async Task PersonalOwnerHoursRemainReadableOutsideManagedTeamButReportsDoNotIncludeThem()
    {
        await using var f = new Fixture(OrganizationRole.Manager);
        var item = await f.Db.WorkItems.SingleAsync(i=>i.Board.ProjectId==f.Hidden.Id);
        item.TeamId=null; item.Team=null; await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var personal = await f.Visibility.GetScopeAsync("actor",false);
        var report = await f.Visibility.GetScopeAsync("actor",true);
        var repo = new TimeEntryRepository(f.Db);
        Assert.Equal(5m,(await repo.GetVisibleEntriesAsync(personal,userId:"actor"))
            .Sum(e=>(decimal)(e.EndedAt!.Value-e.StartedAt).TotalHours));
        Assert.Equal(1m,(await repo.GetVisibleEntriesAsync(report,userId:"actor"))
            .Sum(e=>(decimal)(e.EndedAt!.Value-e.StartedAt).TotalHours));
    }

    [Fact]
    public async Task ExplicitReportDenialDoesNotGrantOthersThroughPersonalTotals()
    {
        await using var f = new Fixture(OrganizationRole.Administrator);
        await f.Deny(PermissionScope.Report);
        var repo = new TimeEntryRepository(f.Db);
        var personal = await f.Visibility.GetScopeAsync("actor",false);
        var report = await f.Visibility.GetScopeAsync("actor",true);
        var own = await repo.GetVisibleEntriesAsync(personal);
        Assert.NotEmpty(own); Assert.All(own,e=>Assert.Equal("actor",e.UserId));
        Assert.Empty(await repo.GetVisibleEntriesAsync(report));
    }

    [Fact]
    public async Task ItemReportDenialRemovesOtherHoursButPreservesPersonalHistory()
    {
        await using var f = new Fixture(OrganizationRole.Administrator);
        var item = await f.Db.WorkItems.SingleAsync(i => i.Title == "Tarefa própria");
        await f.Deny(PermissionScope.WorkItem, item.Id);
        var personal = await f.Visibility.GetScopeAsync("actor", false);
        var report = await f.Visibility.GetScopeAsync("actor", true);
        var repository = new TimeEntryRepository(f.Db);
        Assert.All(await repository.GetVisibleEntriesAsync(personal, workItemId: item.Id),
            entry => Assert.Equal("actor", entry.UserId));
        Assert.Empty(await repository.GetVisibleEntriesAsync(report, workItemId: item.Id));
        Assert.Equal(7m, (await f.Run(ReportDataSource.TimeEntries)).Rows.Sum(r => (decimal)r["hours"]!));
    }

    [Theory]
    [InlineData(PermissionScope.Project)]
    [InlineData(PermissionScope.Report)]
    public async Task ProjectReportDenialCannotBeBypassedByAnOrganizationQuery(PermissionScope deniedScope)
    {
        await using var f=new Fixture(OrganizationRole.Administrator);
        f.Db.PermissionGrants.Add(new PermissionGrant { Id=Guid.NewGuid(),OrganizationId=f.OrganizationId,
            UserId="actor",Permission=PlatformPermission.ViewReport,Scope=deniedScope,ScopeId=f.Shared.Id,
            IsAllowed=false,GrantedBy="admin" });
        await f.Db.SaveChangesAsync();
        Assert.Equal(4m,(await f.Run(ReportDataSource.TimeEntries)).Rows.Sum(r=>(decimal)r["hours"]!));
        await Assert.ThrowsAsync<AcessoNegadoException>(()=>f.Run(ReportDataSource.TimeEntries,projectId:f.Shared.Id));
    }

    [Fact]
    public async Task ASharedDefinitionDenialCannotBeBypassedThroughExecution()
    {
        await using var f=new Fixture(OrganizationRole.Administrator);
        var report=SavedReport.Create(f.OrganizationId,null,"colleague","Negado",ReportDataSource.TimeEntries,
            ReportVisualization.Table,System.Text.Json.JsonSerializer.Serialize(Fixture.Definition(ReportDataSource.TimeEntries),
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),true);
        f.Analytics.AddSavedReport(report);await f.Analytics.SaveAsync();
        await f.Deny(PermissionScope.Report,report.Id);
        await Assert.ThrowsAsync<AcessoNegadoException>(()=>new RunSavedReportQueryHandler(f.Analytics,f.Executor,
            f.Permissions,f.Access,f.Visibility).Handle(new(report.Id,"actor"),default));
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
    [InlineData(OrganizationRole.Viewer, 0)]
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
        var saved = await new RunSavedReportQueryHandler(f.Analytics,f.Executor,f.Permissions,f.Access,f.Visibility)
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
