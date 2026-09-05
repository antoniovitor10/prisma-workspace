using Detran.Kanban.Application.Features.WorkItems;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Domain.Exceptions;
using Detran.Kanban.Domain.Services;

namespace Detran.Kanban.Tests;

public class DependencyGraphTests
{
    [Fact]
    public void DetectsDirectAndIndirectCycles_WithCanonicalBlocksDirection()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        Assert.True(DependencyGraphService.WouldCreateCycle(
            [new(a, b)], new(b, a)));
        Assert.True(DependencyGraphService.WouldCreateCycle(
            [new(a, b), new(b, c)], new(c, a)));

        Assert.True(DependencyGraphService.TryCanonicalize(
            a, b, WorkItemLinkType.Blocks, out var blocks));
        Assert.Equal(new DependencyEdge(b, a), blocks);
        Assert.False(DependencyGraphService.TryCanonicalize(
            a, b, WorkItemLinkType.Related, out _));
    }

    [Theory]
    [InlineData(WorkItemKind.Epic, WorkItemKind.Feature)]
    [InlineData(WorkItemKind.Epic, WorkItemKind.UserStory)]
    [InlineData(WorkItemKind.Feature, WorkItemKind.UserStory)]
    [InlineData(WorkItemKind.UserStory, WorkItemKind.Task)]
    [InlineData(WorkItemKind.Task, WorkItemKind.Subtask)]
    public void HierarchyRules_AllowOnlyD36Chain(WorkItemKind parent, WorkItemKind child)
        => Assert.True(WorkItemHierarchyRules.IsAllowed(parent, child));

    [Fact]
    public async Task CreateLinkHandler_DoesNotPersistOrPublish_WhenCycleIsDetected()
    {
        var organizationId = Guid.NewGuid();
        var project = new Project { Id = Guid.NewGuid(), Key = "DET", OrganizationId = organizationId };
        var board = new Board
        {
            Id = Guid.NewGuid(), OrganizationId = organizationId,
            ProjectId = project.Id, Project = project, Name = "Produto"
        };
        var source = new WorkItem { Id = Guid.NewGuid(), Number = 43, Board = board, BoardId = board.Id };
        var target = new WorkItem { Id = Guid.NewGuid(), Number = 42, Board = board, BoardId = board.Id };
        var repository = new ManagementRepositoryFake(source, target) { AddResult = false };
        var feed = new FeedFake();
        var handler = new CreateWorkItemLinkCommandHandler(
            repository, new AccessFake(), new PermissionFake(), feed);

        var error = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new CreateWorkItemLinkCommand(source.Id, target.Id, WorkItemLinkType.DependsOn, "actor"), default));

        Assert.Contains("Dependência cíclica", error.Message);
        Assert.Empty(feed.Events);
        Assert.Equal(1, repository.AddAttempts);
    }

    [Fact]
    public async Task SearchHandler_UsesSourceProjectAsAuthorizationContext()
    {
        var projectId = Guid.NewGuid();
        var targetProjectId = Guid.NewGuid();
        var search = new SearchRepositoryFake([
            new(Guid.NewGuid(), targetProjectId, "OUTRO", 42, "Emitir documento", "Em andamento")
        ]);
        var access = new AccessFake();
        var handler = new SearchWorkItemsQueryHandler(search, access);

        var result = await handler.Handle(
            new SearchWorkItemsQuery(projectId, " OUTRO-4 ", "actor"), default);

        Assert.Equal(projectId, access.LastProjectId);
        Assert.Equal("OUTRO-4", search.LastQuery);
        Assert.Equal("OUTRO-42", $"{result[0].ProjectKey}-{result[0].Number}");
    }

    private sealed class SearchRepositoryFake(IReadOnlyList<WorkItemSearchEntry> results)
        : IWorkItemSearchRepository
    {
        public string? LastQuery { get; private set; }
        public Task<IReadOnlyList<WorkItemSearchEntry>> SearchVisibleAsync(
            string query, string userId, int limit, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(results);
        }
    }

    private sealed class ManagementRepositoryFake(params WorkItem[] items) : IWorkItemManagementRepository
    {
        public bool AddResult { get; set; } = true;
        public int AddAttempts { get; private set; }
        public Task<WorkItem?> GetDetailedAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(items.SingleOrDefault(item => item.Id == id));
        public Task<WorkItem?> GetEditableAsync(Guid id, CancellationToken cancellationToken = default)
            => GetDetailedAsync(id, cancellationToken);
        public Task<WorkItemLink?> GetLinkAsync(Guid workItemId, Guid linkId, CancellationToken cancellationToken = default)
            => Task.FromResult<WorkItemLink?>(null);
        public Task AddLinkAsync(WorkItemLink link, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<bool> TryAddLinkAcyclicAsync(WorkItemLink link, CancellationToken cancellationToken = default)
        {
            AddAttempts++;
            return Task.FromResult(AddResult);
        }
        public Task RemoveLinkAsync(WorkItemLink link, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddFollowerAsync(Guid workItemId, string userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveFollowerAsync(Guid workItemId, string userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveWithEventAsync(TaskEvent taskEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class AccessFake : IProjectAccessService
    {
        public Guid? LastProjectId { get; private set; }
        public Task<ProjectRole?> GetRoleAsync(Guid projectId, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectRole?>(ProjectRole.ProjectAdmin);
        public Task EnsureAtLeastAsync(Guid projectId, string userId, ProjectRole minimumRole, CancellationToken cancellationToken = default)
        {
            LastProjectId = projectId;
            return Task.CompletedTask;
        }
    }

    private sealed class PermissionFake : IPermissionService
    {
        public Task<bool> HasAsync(string userId, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization, Guid? scopeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
        public Task EnsureAsync(string userId, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization, Guid? scopeId = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FeedFake : ITaskFeedRepository
    {
        public List<TaskEvent> Events { get; } = [];
        public Task<IReadOnlyList<Comment>> GetCommentsAsync(Guid workItemId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Comment>>([]);
        public Task<Comment> AddCommentAsync(Comment comment, CancellationToken cancellationToken = default)
            => Task.FromResult(comment);
        public Task<IReadOnlyList<TaskEvent>> GetEventsAsync(Guid workItemId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TaskEvent>>(Events);
        public Task<IReadOnlyList<StageHistory>> GetStageHistoriesAsync(Guid workItemId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StageHistory>>([]);
        public Task AddEventAsync(TaskEvent taskEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(taskEvent);
            return Task.CompletedTask;
        }
    }
}
