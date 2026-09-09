using Prisma.Workspace.Application.Features.WorkItems.Commands;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;

namespace Prisma.Workspace.Tests;

/// <summary>
/// Criação e movimentação de WorkItem no modelo de quadro único por tarefa (D83).
/// </summary>
public class WorkItemBoardTests
{
    // ── helpers ────────────────────────────────────────────────────────────

    private static Board CriarBoard(Guid? projectId = null)
        => new() { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Quadro", CreatedAt = DateTimeOffset.UtcNow };

    private static Stage CriarBacklog(Guid boardId)
        => new() { Id = Guid.NewGuid(), BoardId = boardId, Name = "Backlog", Category = StageCategory.Ready, Position = 100, CreatedAt = DateTimeOffset.UtcNow };

    private static Stage CriarStage(Guid boardId, string nome, StageCategory cat, Guid? statusId = null)
        => new() { Id = Guid.NewGuid(), BoardId = boardId, Name = nome, Category = cat, WorkflowStatusId = statusId, Position = 200, CreatedAt = DateTimeOffset.UtcNow };

    // ── CreateWorkItemCommandHandler ────────────────────────────────────────

    [Fact]
    public async Task CreateWorkItem_SemBacklogNoBoard_LancaArgumentException()
    {
        var board = CriarBoard();
        // Nenhuma stage Backlog — só InProgress
        var stage = CriarStage(board.Id, "Em andamento", StageCategory.InProgress);

        var repo = new FakeWorkItemRepo();
        var handler = new CreateWorkItemCommandHandler(
            repo,
            new FakeBoardRepo(board),
            new FakeStageRepo(stage),
            new FakeProjectRepo(),
            new FakeTeamRepo(),
            new FakeUserDirectory());

        var command = new CreateWorkItemCommand(
            BoardId: board.Id, StageId: null, ParentId: null,
            Title: "Sem Backlog", Subtitle: null, Description: null,
            Priority: Priority.Low, EstimatedHours: null, DueDate: null,
            Position: 0, CreatedBy: "u1");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(command, CancellationToken.None));

        Assert.Null(repo.AddedItem);
        Assert.Equal(0, repo.UpdateCount);
    }

    [Fact]
    public async Task CreateWorkItem_SemBoardIds_UsaDefaultBoardDoProject()
    {
        var projectId = Guid.NewGuid();
        var board = CriarBoard(projectId);
        var project = new Project { Id = projectId, DefaultBoardId = board.Id, Name = "Projeto", Key = "PRJ", OwnerId = "u1" };
        var repo = new FakeWorkItemRepo();
        var handler = new CreateWorkItemCommandHandler(
            repo,
            new FakeBoardRepo(board),
            new FakeStageRepo(CriarBacklog(board.Id)),
            new FakeProjectRepo(project),
            new FakeTeamRepo(),
            new FakeUserDirectory());

        var command = new CreateWorkItemCommand(
            Guid.Empty, null, null, "Default board", null, null, Priority.Medium,
            null, null, 100, "u1", ProjectId: projectId);

        await handler.Handle(command, CancellationToken.None);

        var item = Assert.IsType<WorkItem>(repo.AddedItem);
        Assert.Equal(board.Id, item.BoardId);
    }

    // ── MoveWorkItemCommandHandler ────────────────────────────────────────

    [Fact]
    public async Task MoveWorkItem_AtualizaEtapaEPosicaoDoItem()
    {
        var board = CriarBoard();
        var source = CriarStage(board.Id, "A fazer", StageCategory.Ready);
        var destination = CriarStage(board.Id, "Em andamento", StageCategory.InProgress);
        var item = new WorkItem
        {
            Id = Guid.NewGuid(), BoardId = board.Id, Board = board, StageId = source.Id,
            Title = "Single board", Position = 100, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        var repo = new FakeMoveWorkItemRepo(item);
        var handler = new MoveWorkItemCommandHandler(repo, new FakeStageRepo(source, destination), new FakeTaskFeedRepoMove());

        await handler.Handle(new MoveWorkItemCommand(item.Id, destination.Id, 250, "actor", "Actor"), CancellationToken.None);

        Assert.Equal(destination.Id, item.StageId);
        Assert.Equal(250, item.Position);
        Assert.Equal(1, repo.UpdateCount);
    }

    // ── Fakes ──────────────────────────────────────────────────────────────

    private sealed class FakeWorkItemRepo : IWorkItemRepository
    {
        public WorkItem? AddedItem { get; private set; }
        public int UpdateCount { get; private set; }

        public Task<WorkItem> AddAsync(WorkItem workItem, CancellationToken ct = default)
        {
            AddedItem = workItem;
            return Task.FromResult(workItem);
        }

        public Task<WorkItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<WorkItem?>(null);
        public Task<WorkItem?> GetForMoveAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<WorkItem?>(null);
        public Task<IReadOnlyList<WorkItem>> GetByBoardIdAsync(Guid boardId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WorkItem>>(new List<WorkItem>());
        public Task<IReadOnlyList<WorkItem>> GetSubItemsAsync(Guid parentId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WorkItem>>(new List<WorkItem>());
        public Task<IReadOnlyList<WorkItemAssignee>> GetAssigneesAsync(Guid workItemId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WorkItemAssignee>>(new List<WorkItemAssignee>());
        public Task UpdateAsync(WorkItem workItem, CancellationToken ct = default)
        {
            UpdateCount++;
            return Task.CompletedTask;
        }
        public Task UpdateWithEventAsync(WorkItem workItem, TaskEvent taskEvent, CancellationToken ct = default)
        {
            UpdateCount++;
            return Task.CompletedTask;
        }
        public Task DeleteAsync(WorkItem workItem, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddAssigneeAsync(Guid workItemId, string userId, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAssigneeAsync(Guid workItemId, string userId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<WorkItem>> GetAssignedToUserAsync(string userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WorkItem>>(new List<WorkItem>());
        public Task UpdatePersonalPrioritiesAsync(string userId, IReadOnlyList<Guid> ids, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<WorkItem>> GetExclusiveToBoardAsync(Guid boardId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WorkItem>>(new List<WorkItem>());
    }

    private sealed class FakeMoveWorkItemRepo : IWorkItemRepository
    {
        private readonly WorkItem _item;
        public FakeMoveWorkItemRepo(WorkItem item) => _item = item;
        public int UpdateCount { get; private set; }

        public Task<WorkItem?> GetForMoveAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(id == _item.Id ? _item : null);
        public Task<WorkItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(id == _item.Id ? _item : null);
        public Task UpdateAsync(WorkItem w, CancellationToken ct = default)
        {
            UpdateCount++;
            return Task.CompletedTask;
        }
        public Task UpdateWithEventAsync(WorkItem w, TaskEvent e, CancellationToken ct = default)
        {
            UpdateCount++;
            return Task.CompletedTask;
        }
        public Task<WorkItem> AddAsync(WorkItem w, CancellationToken ct = default) => Task.FromResult(w);
        public Task<IReadOnlyList<WorkItem>> GetByBoardIdAsync(Guid boardId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<WorkItem>>(new List<WorkItem>());
        public Task<IReadOnlyList<WorkItem>> GetSubItemsAsync(Guid parentId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<WorkItem>>(new List<WorkItem>());
        public Task<IReadOnlyList<WorkItemAssignee>> GetAssigneesAsync(Guid workItemId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<WorkItemAssignee>>(new List<WorkItemAssignee>());
        public Task DeleteAsync(WorkItem w, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddAssigneeAsync(Guid workItemId, string userId, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAssigneeAsync(Guid workItemId, string userId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<WorkItem>> GetAssignedToUserAsync(string userId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<WorkItem>>(new List<WorkItem>());
        public Task UpdatePersonalPrioritiesAsync(string userId, IReadOnlyList<Guid> ids, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<WorkItem>> GetExclusiveToBoardAsync(Guid boardId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<WorkItem>>(new List<WorkItem>());
    }

    private sealed class FakeBoardRepo : IBoardRepository
    {
        private readonly Dictionary<Guid, Board> _boards;
        public FakeBoardRepo(params Board[] boards)
            => _boards = boards.ToDictionary(b => b.Id);

        public Task<Board?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_boards.TryGetValue(id, out var b) ? b : null);
        public Task<IReadOnlyList<Board>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Board>>(_boards.Values.ToList());
        public Task<IReadOnlyList<Board>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Board>>(_boards.Values.Where(b => b.ProjectId == projectId).ToList());
        public Task<Board> AddAsync(Board board, CancellationToken ct = default) => Task.FromResult(board);
        public Task UpdateAsync(Board board, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Board board, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeStageRepo : IStageRepository
    {
        private readonly List<Stage> _stages;
        public FakeStageRepo(params Stage[] stages) => _stages = stages.ToList();

        public Task<Stage?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_stages.FirstOrDefault(s => s.Id == id));
        public Task<IReadOnlyList<Stage>> GetByBoardIdAsync(Guid boardId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Stage>>(_stages.Where(s => s.BoardId == boardId).ToList());
        public Task<Stage> AddAsync(Stage stage, CancellationToken ct = default) => Task.FromResult(stage);
        public Task UpdateAsync(Stage stage, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Stage stage, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeProjectRepo(params Project[] projects) : IProjectRepository
    {
        public Task<IReadOnlyList<Project>> GetForUserAsync(string userId, bool includeArchived = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Project>>(new List<Project>());
        public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(projects.FirstOrDefault(project => project.Id == id));
        public Task<Project?> GetByIdWithMembersAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(projects.FirstOrDefault(project => project.Id == id));
        public Task<bool> KeyExistsAsync(string key, CancellationToken ct = default)
            => Task.FromResult(false);
        public Task AddAsync(Project project, CancellationToken ct = default) => Task.CompletedTask;
        public void AddCustomField(ProjectCustomFieldDefinition field) { }
        public void AddEvent(ProjectEvent projectEvent) { }
        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeTeamRepo : ITeamRepository
    {
        public Task<Team?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<Team?>(null);
        public Task<Team?> GetByIdWithMembersAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<Team?>(null);
        public Task<IReadOnlyList<Team>> GetAllWithMembersAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Team>>(new List<Team>());
        public Task<TeamMember?> GetMemberAsync(Guid teamId, string userId, CancellationToken ct = default)
            => Task.FromResult<TeamMember?>(null);
        public Task<bool> NameExistsAsync(string name, Guid? exceptId = null, CancellationToken ct = default)
            => Task.FromResult(false);
        public Task AddAsync(Team team, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Team team, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveMemberAsync(TeamMember member, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeUserDirectory : IUserDirectory
    {
        public Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(
            IEnumerable<string> userIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(
                userIds.ToDictionary(id => id, id => id));
        public Task<IReadOnlyList<UserSummary>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<UserSummary>>(new List<UserSummary>());
        public Task<IReadOnlyList<UserSummary>> GetByIdsAsync(
            IEnumerable<string> userIds, bool includeInactive = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<UserSummary>>(new List<UserSummary>());
        public Task<UserSummary?> GetByIdAsync(string userId, CancellationToken ct = default)
            => Task.FromResult<UserSummary?>(null);
        public Task<UserSummary?> GetByIdUnscopedAsync(string userId, CancellationToken ct = default)
            => Task.FromResult<UserSummary?>(null);
        public Task<UserSummary?> GetByEmailAsync(string email, CancellationToken ct = default)
            => Task.FromResult<UserSummary?>(null);
    }

    private sealed class FakeTaskFeedRepoMove : ITaskFeedRepository
    {
        public Task<IReadOnlyList<Comment>> GetCommentsAsync(Guid workItemId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Comment>>(new List<Comment>());
        public Task<Comment> AddCommentAsync(Comment comment, CancellationToken ct = default)
            => Task.FromResult(comment);
        public Task<IReadOnlyList<TaskEvent>> GetEventsAsync(Guid workItemId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TaskEvent>>(new List<TaskEvent>());
        public Task<IReadOnlyList<StageHistory>> GetStageHistoriesAsync(Guid workItemId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<StageHistory>>(new List<StageHistory>());
        public Task AddEventAsync(TaskEvent taskEvent, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
