using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.Backlog;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Queries;
using Prisma.Workspace.Infrastructure.Repositories;

namespace Prisma.Workspace.Tests;

/// <summary>
/// T-0022 (CA-4): todas as consultas de quadro, lista e métrica usam a regra única de
/// visibilidade. Cenário do CA num projeto sem método: História de topo com Tarefa filha,
/// Tarefa de topo com Subtarefa, Bug de topo, Épico e Funcionalidade sob o Épico.
///
/// Roda sempre no provedor em memória. Com <c>ConnectionStrings__DefaultConnection</c> apontando
/// para um banco <c>Prisma_BoardColumns_*</c> descartável, roda também no SQL Server, o que prova
/// que a regra é traduzida para SQL (sem avaliação no cliente).
/// </summary>
public class WorkItemBoardQueriesTests
{
    private const string InMemory = "InMemory";
    private const string SqlServer = "SqlServer";

    public static TheoryData<string> Providers()
    {
        var data = new TheoryData<string> { InMemory };
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")))
            data.Add(SqlServer);
        return data;
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Board_shows_story_child_task_top_task_and_bug_only(string provider)
    {
        await using var s = await Scenario.CreateAsync(provider);

        var cards = await new WorkItemRepository(s.Context).GetByBoardIdAsync(s.Board.Id);

        Assert.Equal(s.ExpectedCards, Ids(cards));
        var topTask = Assert.Single(cards, x => x.Id == s.TopTask.Id);
        Assert.Equal(s.Subtask.Id, Assert.Single(topTask.SubItems).Id);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Project_items_use_the_same_rule(string provider)
    {
        await using var s = await Scenario.CreateAsync(provider);

        var cards = await new WorkItemRepository(s.Context).GetByProjectIdAsync(s.Project.Id);

        Assert.Equal(s.ExpectedCards, Ids(cards));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Assigned_items_are_board_cards(string provider)
    {
        await using var s = await Scenario.CreateAsync(provider);

        var items = await new WorkItemRepository(s.Context).GetAssignedToUserAsync(s.UserId);

        Assert.Equal(Set(s.StoryChildTask.Id), Ids(items));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task My_tasks_include_story_child_and_keep_planning_items(string provider)
    {
        await using var s = await Scenario.CreateAsync(provider);

        var items = await new MyWorkRepository(s.Context).GetTasksAsync(s.UserId);

        // A Subtarefa atribuída continua fora, como antes (está dentro do card da Tarefa).
        Assert.Equal(Set(s.Story.Id, s.StoryChildTask.Id, s.Epic.Id), Ids(items));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Board_metrics_count_visible_cards(string provider)
    {
        await using var s = await Scenario.CreateAsync(provider);

        var metrics = await new BoardMetricsQueries(s.Context).GetAsync(s.Board.Id);

        Assert.Equal(2, metrics.TasksByStage.Single(x => x.Stage == s.Todo.Name).Count);
        Assert.Equal(1, metrics.TasksByStage.Single(x => x.Stage == s.Done.Name).Count);
        // Vazão: a Tarefa filha entregue conta; a Subtarefa entregue não é card.
        Assert.Equal(1, metrics.DeliveredThisWeek);
        Assert.Equal(3, metrics.TasksByType.Sum(x => x.Count));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Lead_time_histories_come_from_visible_cards(string provider)
    {
        await using var s = await Scenario.CreateAsync(provider);

        var histories = await new StageHistoryRepository(s.Context).GetByBoardIdAsync(s.Board.Id);

        Assert.Equal(s.ExpectedCards, histories.Select(x => x.WorkItemId).ToHashSet());
        Assert.Contains(histories, x => x.WorkItemId == s.StoryChildTask.Id && x.LeftAt != null);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Company_gallery_counts_cards(string provider)
    {
        await using var s = await Scenario.CreateAsync(provider);

        var summary = Assert.Single(await new CompanyQueries(s.Context).GetProjectsAsync(), x => x.Id == s.Board.Id);

        Assert.Equal(3, summary.TasksTotal);
        Assert.Equal(1, summary.TasksDone);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Sprint_board_flag_comes_from_the_backlog(string provider)
    {
        await using var s = await Scenario.CreateAsync(provider);
        var handler = new GetProjectBacklogQueryHandler(new BacklogRepository(s.Context), new AllowAllAccess());

        var backlog = await handler.Handle(new GetProjectBacklogQuery(s.Project.Id, s.UserId), default);

        // O backlog continua listando tudo; só a marca de card muda.
        Assert.Equal(s.All, backlog.Select(x => x.Id).ToHashSet());
        Assert.Equal(s.ExpectedCards, backlog.Where(x => x.IsBoardCard).Select(x => x.Id).ToHashSet());
    }

    private static HashSet<Guid> Ids(IEnumerable<WorkItem> items) => items.Select(x => x.Id).ToHashSet();
    private static HashSet<Guid> Set(params Guid[] ids) => ids.ToHashSet();

    private sealed class Scenario : IAsyncDisposable
    {
        private static readonly SemaphoreSlim MigrationLock = new(1, 1);
        private static bool _migrated;

        public Guid OrganizationId { get; } = Guid.NewGuid();
        public string UserId { get; } = "t0022-" + Guid.NewGuid().ToString("N");
        public AppDbContext Context { get; private set; } = null!;
        public Project Project { get; private set; } = null!;
        public Board Board { get; private set; } = null!;
        public Stage Todo { get; private set; } = null!;
        public Stage Done { get; private set; } = null!;
        public WorkItem Epic { get; private set; } = null!;
        public WorkItem Feature { get; private set; } = null!;
        public WorkItem Story { get; private set; } = null!;
        public WorkItem StoryChildTask { get; private set; } = null!;
        public WorkItem TopTask { get; private set; } = null!;
        public WorkItem Subtask { get; private set; } = null!;
        public WorkItem Bug { get; private set; } = null!;

        public HashSet<Guid> ExpectedCards => [StoryChildTask.Id, TopTask.Id, Bug.Id];
        public HashSet<Guid> All => [Epic.Id, Feature.Id, Story.Id, StoryChildTask.Id, TopTask.Id, Subtask.Id, Bug.Id];

        public static async Task<Scenario> CreateAsync(string provider)
        {
            var scenario = new Scenario();
            scenario.Context = await CreateContextAsync(provider, scenario.OrganizationId);
            await scenario.SeedAsync();
            scenario.Context.ChangeTracker.Clear();
            return scenario;
        }

        private static async Task<AppDbContext> CreateContextAsync(string provider, Guid organizationId)
        {
            var tenant = new OrganizationContextFake(organizationId);
            if (provider == InMemory)
                return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenant);

            var value = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            Assert.False(string.IsNullOrWhiteSpace(value), "Configure o banco de teste exclusivo.");
            // Nunca roda fora de um banco descartável próprio.
            Assert.StartsWith("Prisma_BoardColumns_", new SqlConnectionStringBuilder(value).InitialCatalog);
            var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(value).Options, tenant);
            await MigrationLock.WaitAsync();
            try
            {
                if (!_migrated) { await context.Database.MigrateAsync(); _migrated = true; }
            }
            finally { MigrationLock.Release(); }
            return context;
        }

        private async Task SeedAsync()
        {
            var now = DateTimeOffset.UtcNow;
            Project = Project.Criar("T" + Guid.NewGuid().ToString("N")[..8], "Quadro sem método", "owner",
                WorkNature.Project, WorkType.Development);
            Project.OrganizationId = OrganizationId;
            Board = new Board { Id = Guid.NewGuid(), ProjectId = Project.Id, OrganizationId = OrganizationId,
                Name = "Quadro T-0022 " + Guid.NewGuid().ToString("N")[..6], OwnerId = "owner", CreatedAt = now };
            Todo = new Stage { Id = Guid.NewGuid(), ProjectId = Project.Id, BoardId = Board.Id, Name = "A fazer",
                Position = 1000, Category = StageCategory.Backlog, CreatedAt = now };
            Done = new Stage { Id = Guid.NewGuid(), ProjectId = Project.Id, BoardId = Board.Id, Name = "Concluído",
                Position = 2000, Category = StageCategory.Done, CreatedAt = now };

            Epic = Item("Épico", WorkItemKind.Epic, Todo, null, now);
            Feature = Item("Funcionalidade", WorkItemKind.Feature, Todo, Epic, now);
            Story = Item("História", WorkItemKind.UserStory, Todo, null, now);
            StoryChildTask = Item("Tarefa da História", WorkItemKind.Task, Done, Story, now);
            TopTask = Item("Tarefa de topo", WorkItemKind.Task, Todo, null, now);
            Subtask = Item("Subtarefa", WorkItemKind.Subtask, Done, TopTask, now);
            Bug = Item("Bug de topo", WorkItemKind.Bug, Todo, null, now);

            // A História e a Tarefa filha passaram pela primeira coluna: lead time só conta a Tarefa.
            foreach (var moved in new[] { Story, StoryChildTask })
                moved.StageHistories.Add(new StageHistory { Id = Guid.NewGuid(), WorkItemId = moved.Id, StageId = Todo.Id,
                    EnteredAt = now.AddHours(-5), LeftAt = now.AddHours(-1) });

            // Ligados à pessoa: História, Tarefa filha e Subtarefa como responsável; Épico como participante.
            Story.ResponsibleId = UserId;
            StoryChildTask.ResponsibleId = UserId;
            Subtask.ResponsibleId = UserId;
            Epic.Assignees.Add(new WorkItemAssignee { WorkItemId = Epic.Id, UserId = UserId, AssignedAt = now });

            Context.AddRange(new Organization { Id = OrganizationId, Name = "T-0022", Slug = "t0022-" + OrganizationId.ToString("N"),
                CreatedAt = now, UpdatedAt = now }, Project, Board, Todo, Done);
            Context.WorkItems.AddRange(Epic, Feature, Story, StoryChildTask, TopTask, Subtask, Bug);
            await Context.SaveChangesAsync();
        }

        private WorkItem Item(string title, WorkItemKind kind, Stage stage, WorkItem? parent, DateTimeOffset now)
        {
            var item = new WorkItem { Id = Guid.NewGuid(), BoardId = Board.Id, StageId = stage.Id, ParentId = parent?.Id,
                Kind = kind, Title = title, CreatedAt = now.AddDays(-2), UpdatedAt = now, Position = 1000,
                ResponsibleId = "owner", CreatedBy = "owner" };
            item.StageHistories.Add(new StageHistory { Id = Guid.NewGuid(), WorkItemId = item.Id, StageId = stage.Id,
                EnteredAt = now.AddMinutes(-30) });
            return item;
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed class OrganizationContextFake(Guid organizationId) : IOrganizationContext
    {
        public Guid? OrganizationId => organizationId;
    }

    private sealed class AllowAllAccess : IProjectAccessService
    {
        public Task<ProjectRole?> GetRoleAsync(Guid projectId, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectRole?>(ProjectRole.ProjectAdmin);
        public Task EnsureAtLeastAsync(Guid projectId, string userId, ProjectRole minimumRole, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
