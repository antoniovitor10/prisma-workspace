using Detran.Kanban.Application.Features.Backlog;
using Detran.Kanban.Application.Features.WorkItems.Commands;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Domain.Exceptions;

namespace Detran.Kanban.Tests;

public class BacklogFeatureTests
{
    [Fact]
    public async Task ProjectBacklog_ReturnsSubtasksWithTheirParentId()
    {
        var parent = NewItem(WorkItemKind.Task, "Tarefa pai");
        var child = NewItem(WorkItemKind.Subtask, "Subtarefa");
        child.ParentId = parent.Id;
        var board = new Board { Id = Guid.NewGuid(), Name = "Produto" };
        parent.Board = board;
        child.Board = board;
        var handler = new GetProjectBacklogQueryHandler(
            new BacklogRepositoryFake([parent, child]), new ProjectAccessFake());

        var result = await handler.Handle(
            new GetProjectBacklogQuery(Guid.NewGuid(), "viewer"), default);

        Assert.Equal(2, result.Count);
        Assert.Equal(parent.Id, result.Single(item => item.Id == child.Id).ParentId);
    }

    [Fact]
    public async Task CreateWorkItem_RejectsPositionOutsideBacklogRankPrecision()
    {
        var validator = new CreateWorkItemCommandValidator();
        var command = new CreateWorkItemCommand(
            Guid.NewGuid(), null, null, "Item", null, null,
            Priority.Medium, null, null, 1_784_203_449_579d, "actor");

        var result = await validator.ValidateAsync(command);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.Position));
    }

    [Fact]
    public async Task UpdateBacklogItem_ChangesInlineFields_AndRelatesEpic()
    {
        var projectId = Guid.NewGuid();
        var epic = NewItem(WorkItemKind.Epic, "Plataforma");
        var story = NewItem(WorkItemKind.UserStory, "Titulo antigo");
        var repository = new BacklogRepositoryFake([epic, story]);
        var handler = new UpdateBacklogItemCommandHandler(repository, new ProjectAccessFake());

        await handler.Handle(new UpdateBacklogItemCommand(
            projectId, story.Id, "  Novo titulo  ", Priority.High,
            8, epic.Id, true, "product-owner"), default);

        Assert.Equal("Novo titulo", story.Title);
        Assert.Equal(Priority.High, story.Priority);
        Assert.Equal(8, story.Points);
        Assert.Equal(epic.Id, story.ParentId);
        Assert.True(repository.Saved);
    }

    [Fact]
    public async Task UpdateBacklogItem_RejectsParentThatIsNotEpic()
    {
        var projectId = Guid.NewGuid();
        var feature = NewItem(WorkItemKind.Feature, "Feature");
        var story = NewItem(WorkItemKind.UserStory, "Historia");
        var handler = new UpdateBacklogItemCommandHandler(
            new BacklogRepositoryFake([feature, story]), new ProjectAccessFake());

        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new UpdateBacklogItemCommand(projectId, story.Id, story.Title,
                Priority.Medium, 5, feature.Id, true, "product-owner"), default));
    }

    [Fact]
    public async Task UpdateBacklogItem_PreservesHierarchy_WhenEpicWasNotEdited()
    {
        var projectId = Guid.NewGuid();
        var feature = NewItem(WorkItemKind.Feature, "Feature");
        var story = NewItem(WorkItemKind.UserStory, "Historia");
        story.ParentId = feature.Id;
        var handler = new UpdateBacklogItemCommandHandler(
            new BacklogRepositoryFake([feature, story]), new ProjectAccessFake());

        await handler.Handle(new UpdateBacklogItemCommand(
            projectId, story.Id, "Historia refinada", Priority.Critical,
            13, null, false, "product-owner"), default);

        Assert.Equal(feature.Id, story.ParentId);
        Assert.Equal("Historia refinada", story.Title);
    }

    [Fact]
    public async Task PlanSprint_MovesAndReturnsAllSelectedItems()
    {
        var projectId = Guid.NewGuid();
        var sprint = Sprint.Criar(projectId, Guid.NewGuid(), "Sprint 1",
            new DateOnly(2026, 7, 16), new DateOnly(2026, 7, 30), "Validar backlog");
        var first = NewItem(WorkItemKind.UserStory, "Primeira");
        var second = NewItem(WorkItemKind.Bug, "Segunda");
        var repository = new BacklogRepositoryFake([first, second]);
        var handler = new PlanSprintCommandHandler(
            repository, new SprintRepositoryFake(sprint), new ProjectAccessFake());
        var selectedIds = new[] { first.Id, second.Id };

        await handler.Handle(new PlanSprintCommand(
            projectId, sprint.Id, selectedIds, "scrum-master"), default);

        Assert.All(new[] { first, second }, item => Assert.Equal(sprint.Id, item.SprintId));

        await handler.Handle(new PlanSprintCommand(
            projectId, null, selectedIds, "scrum-master"), default);

        Assert.All(new[] { first, second }, item => Assert.Null(item.SprintId));
    }

    [Fact]
    public async Task PlanSprint_DoesNotRemoveItemsFromAClosedSprint()
    {
        var projectId = Guid.NewGuid();
        var sprint = Sprint.Criar(projectId, Guid.NewGuid(), "Sprint encerrada",
            new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 14), "Meta entregue");
        sprint.ChangeStatus(SprintStatus.Active);
        sprint.ChangeStatus(SprintStatus.Closed);
        var story = NewItem(WorkItemKind.UserStory, "História entregue");
        story.SprintId = sprint.Id;
        var handler = new PlanSprintCommandHandler(
            new BacklogRepositoryFake([story]), new SprintRepositoryFake(sprint), new ProjectAccessFake());

        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new PlanSprintCommand(projectId, null, [story.Id], "scrum-master"), default));

        Assert.Equal(sprint.Id, story.SprintId);
    }

    private static WorkItem NewItem(WorkItemKind kind, string title) => new()
    {
        Id = Guid.NewGuid(),
        Kind = kind,
        Title = title,
        Priority = Priority.Medium,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private sealed class BacklogRepositoryFake(IReadOnlyList<WorkItem> items) : IBacklogRepository
    {
        public bool Saved { get; private set; }

        public Task<IReadOnlyList<WorkItem>> GetProjectBacklogAsync(
            Guid projectId, CancellationToken cancellationToken = default)
            => Task.FromResult(items);

        public Task<IReadOnlyList<WorkItem>> GetTrackedByIdsAsync(
            Guid projectId,
            IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkItem>>(items.Where(item => ids.Contains(item.Id)).ToList());

        public Task SaveAsync(CancellationToken cancellationToken = default)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }

    private sealed class ProjectAccessFake : IProjectAccessService
    {
        public Task<ProjectRole?> GetRoleAsync(
            Guid projectId, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectRole?>(ProjectRole.ProjectAdmin);

        public Task EnsureAtLeastAsync(
            Guid projectId,
            string userId,
            ProjectRole minimumRole,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class SprintRepositoryFake(Sprint sprint) : ISprintRepository
    {
        public Task<IReadOnlyList<Sprint>> GetByProjectAsync(
            Guid projectId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Sprint>>([sprint]);

        public Task<Sprint?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Sprint?>(id == sprint.Id ? sprint : null);

        public Task<bool> HasActiveAsync(
            Guid projectId, Guid? excludingId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddAsync(Sprint value, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
