using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Features.TaskFeed.Queries;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Tests;

public class TaskTimelineTests
{
    [Fact]
    public async Task TimelinePreservesRevisitsSnapshotsAndCreationInReverseChronologicalOrder()
    {
        var start = DateTimeOffset.Parse("2026-10-12T10:00:00Z");
        var todo = new Stage { Id = Guid.NewGuid(), Name = "A fazer" };
        var doing = new Stage { Id = Guid.NewGuid(), Name = "Em andamento" };
        StageHistory Entry(int id, Stage stage, int hour, string? actor) => new()
        {
            Id = Guid.Parse($"00000000-0000-0000-0000-{id:D12}"), Stage = stage, StageId = stage.Id,
            EnteredAt = start.AddHours(hour), WorkItem = new WorkItem { CreatedAt = start }, ActorId = "identity-removida", ActorName = actor,
        };
        var initial = Entry(1, todo, 0, "Ana Alves");
        var first = Entry(2, doing, 1, "Bruno Costa");
        var reclassified = Entry(3, doing, 2, "Outra pessoa");
        var returned = Entry(4, todo, 3, null);
        var feed = new Feed([returned, reclassified, first, initial]);
        var access = new Access();
        var result = await new GetTaskTimelineQueryHandler(feed, access).Handle(new(Guid.NewGuid(), "viewer"), default);
        Assert.Equal([returned.Id, first.Id, initial.Id], result.Items.Select(item => item.Id));
        Assert.Equal("column_moved", result.Items[0].Kind);
        Assert.Equal("Em andamento", result.Items[0].FromColumnName);
        Assert.Equal("A fazer", result.Items[0].ToColumnName);
        Assert.Null(result.Items[0].ActorName);
        Assert.Equal("Bruno Costa", result.Items[1].ActorName);
        Assert.Equal("created_in_column", result.Items[2].Kind);
        Assert.Null(result.Items[2].FromColumnId);
        Assert.Equal("Ana Alves", result.Items[2].ActorName);
        Assert.True(result.UsesCurrentColumnNames);
        Assert.Equal(PlatformPermission.View, access.Permission);
        Assert.Equal(ProjectRole.Viewer, access.Role);
        Assert.Equal(0, feed.OtherReads);
        Assert.Null(initial.LeftAt);
        Assert.Equal("Ana Alves", initial.ActorName);
    }

    [Fact]
    public async Task FirstEntryAfterCreationDoesNotClaimCreationInThatColumn()
    {
        var history = new StageHistory { Id = Guid.NewGuid(), StageId = Guid.NewGuid(),
            EnteredAt = DateTimeOffset.Parse("2026-10-20T11:00:00Z"),
            WorkItem = new WorkItem { CreatedAt = DateTimeOffset.Parse("2026-10-20T10:00:00Z") } };
        var result = await new GetTaskTimelineQueryHandler(new Feed([history]), new Access())
            .Handle(new(Guid.NewGuid(), "viewer"), default);
        Assert.Equal("column_entered", Assert.Single(result.Items).Kind);
    }

    [Fact]
    public async Task TimelineWithNoPersistedHistoryDoesNotInventCreation()
    {
        var result = await new GetTaskTimelineQueryHandler(new Feed([]), new Access()).Handle(new(Guid.NewGuid(), "viewer"), default);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task DeniedTaskDoesNotReadHistory()
    {
        var feed = new Feed([]);
        await Assert.ThrowsAsync<NaoEncontradoException>(() => new GetTaskTimelineQueryHandler(feed, new Access(true))
            .Handle(new(Guid.NewGuid(), "outsider"), default));
        Assert.Equal(0, feed.HistoryReads);
    }

    private sealed class Access(bool denied = false) : IWorkItemAccessService
    {
        public PlatformPermission Permission { get; private set; }
        public ProjectRole Role { get; private set; }
        public Task EnsureAsync(Guid workItemId, string userId, PlatformPermission permission, ProjectRole minimumRole = ProjectRole.Viewer, CancellationToken cancellationToken = default)
        {
            Permission = permission; Role = minimumRole;
            return denied ? Task.FromException(new NaoEncontradoException("Tarefa")) : Task.CompletedTask;
        }
    }
    private sealed class Feed(IReadOnlyList<StageHistory> entries) : ITaskFeedRepository
    {
        public int HistoryReads { get; private set; }
        public int OtherReads { get; private set; }
        public Task<IReadOnlyList<StageHistory>> GetStageHistoriesAsync(Guid workItemId, CancellationToken cancellationToken = default)
        { HistoryReads++; return Task.FromResult(entries); }
        public Task<IReadOnlyList<Comment>> GetCommentsAsync(Guid workItemId, CancellationToken cancellationToken = default)
        { OtherReads++; throw new NotSupportedException(); }
        public Task<IReadOnlyList<TaskEvent>> GetEventsAsync(Guid workItemId, CancellationToken cancellationToken = default)
        { OtherReads++; throw new NotSupportedException(); }
        public Task<Comment> AddCommentAsync(Comment comment, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddEventAsync(TaskEvent taskEvent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
