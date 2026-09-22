using System.Text.Json;
using Prisma.Workspace.Application.Features.Approvals.Dtos;
using Prisma.Workspace.Application.Features.Assignees;
using Prisma.Workspace.Application.Features.TaskFeed.Commands;
using Prisma.Workspace.Application.Features.TimeEntries;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;

namespace Prisma.Workspace.Tests;

public class CanonicalPersonNameTests
{
    [Fact]
    public async Task Comment_SnapshotsTheOrganizationDisplayName_InsteadOfTheJwtEmail()
    {
        var workItemId = Guid.NewGuid();
        var feed = new FeedFake();
        var users = Directory(
            new UserSummary("actor", "maria@detran.se.gov.br", "maria@detran.se.gov.br", "Maria Silva"));
        var handler = new AddCommentCommandHandler(
            feed, new WorkItemsFake(new WorkItem { Id = workItemId }), new AccessFake(), users);

        var result = await handler.Handle(new AddCommentCommand(
            workItemId, "actor", "maria@detran.se.gov.br", "Comentário"), default);

        Assert.Equal("Maria Silva", feed.Comment!.UserName);
        Assert.Equal("Maria Silva", result.DisplayName);
        Assert.Equal("Maria Silva", result.UserName);
    }

    [Fact]
    public async Task Assignment_SnapshotsActorAndTargetDisplayNames()
    {
        var workItemId = Guid.NewGuid();
        var feed = new FeedFake();
        var users = Directory(
            new UserSummary("actor", "gestor@detran.se.gov.br", "gestor@detran.se.gov.br", "Gestora Ana"),
            new UserSummary("target", "dev@detran.se.gov.br", "dev.legado", "Dev Bruno"));
        var handler = new AssignUserCommandHandler(
            new WorkItemsFake(new WorkItem
            {
                Id = workItemId,
                Board = new Board { ProjectId = Guid.NewGuid() }
            }), users, feed, new AccessFake(), new ProjectAccessFake());

        await handler.Handle(new AssignUserCommand(
            workItemId, "target", "actor", "gestor@detran.se.gov.br"), default);

        using var payload = JsonDocument.Parse(feed.Event!.Payload!);
        Assert.Equal("Gestora Ana", payload.RootElement.GetProperty("actorName").GetString());
        Assert.Equal("Dev Bruno", payload.RootElement.GetProperty("target").GetString());
    }

    [Fact]
    public async Task Assignment_RejectsTargetWithoutProjectAccess()
    {
        var workItemId = Guid.NewGuid();
        var workItems = new WorkItemsFake(new WorkItem
        {
            Id = workItemId,
            Board = new Board { ProjectId = Guid.NewGuid() }
        });
        var users = Directory(
            new UserSummary("actor", "gestor@detran.se.gov.br", "gestor", "Gestora Ana"),
            new UserSummary("target", "dev@detran.se.gov.br", "dev", "Dev Bruno"));
        var handler = new AssignUserCommandHandler(
            workItems, users, new FeedFake(), new AccessFake(), new ProjectAccessFake(false));

        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new AssignUserCommand(workItemId, "target", "actor", "Gestora Ana"), default));

        Assert.Equal(0, workItems.AddAssigneeCount);
    }

    [Fact]
    public async Task AssigneeContract_SeparatesDisplayNameFromLegacyIdentityFields()
    {
        var workItemId = Guid.NewGuid();
        var workItems = new WorkItemsFake(new WorkItem { Id = workItemId })
        {
            Assignees = [new WorkItemAssignee { WorkItemId = workItemId, UserId = "inactive" }]
        };
        var users = Directory(new UserSummary(
            "inactive", "inactive@detran.se.gov.br", "inactive.legacy", "Pessoa Inativa"));
        var handler = new GetAssigneesQueryHandler(workItems, users, new AccessFake());

        var result = Assert.Single(await handler.Handle(new GetAssigneesQuery(workItemId, "actor"), default));

        Assert.Equal("Pessoa Inativa", result.DisplayName);
        Assert.Equal("inactive@detran.se.gov.br", result.Email);
        Assert.Equal("inactive.legacy", result.UserName);
    }

    [Fact]
    public async Task ApprovalProjection_ResolvesInactiveMembersAndUsesNeutralFallback()
    {
        var approval = Approval.Solicitar(Guid.NewGuid(), "inactive", "missing-person-id");
        var users = Directory(new UserSummary(
            "inactive", "inactive@detran.se.gov.br", "inactive.legacy", "Pessoa Inativa"));

        var result = Assert.Single(await ApprovalDto.FromListAsync([approval], users, default));

        Assert.Equal("Pessoa Inativa", result.RequesterName);
        Assert.Equal("Usuário missing-", result.ApproverName);
    }

    [Fact]
    public void TimeEntryContract_CarriesResolvedDisplayName()
    {
        var entry = TimeEntry.Manual(
            Guid.NewGuid(), "inactive", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, null);

        var result = entry.ToDto("Pessoa Inativa");

        Assert.Equal("Pessoa Inativa", result.DisplayName);
        Assert.Equal("inactive", result.UserId);
    }

    private static UserDirectoryFake Directory(params UserSummary[] users) => new(users);

    private sealed class UserDirectoryFake(IEnumerable<UserSummary> users) : IUserDirectory
    {
        private readonly IReadOnlyDictionary<string, UserSummary> _users = users.ToDictionary(user => user.Id);

        public Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(
            IEnumerable<string> userIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(userIds.Distinct()
                .Where(_users.ContainsKey).ToDictionary(id => id, id => _users[id].DisplayName));

        public Task<IReadOnlyList<UserSummary>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserSummary>>(_users.Values.ToList());

        public Task<IReadOnlyList<UserSummary>> GetByIdsAsync(
            IEnumerable<string> userIds, bool includeInactive = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserSummary>>(userIds.Distinct()
                .Where(_users.ContainsKey).Select(id => _users[id]).ToList());

        public Task<UserSummary?> GetByIdAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_users.GetValueOrDefault(userId));

        public Task<UserSummary?> GetByIdUnscopedAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_users.GetValueOrDefault(userId));

        public Task<UserSummary?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(_users.Values.FirstOrDefault(user => user.Email == email));
    }

    private sealed class AccessFake : IWorkItemAccessService
    {
        public Task EnsureAsync(
            Guid workItemId, string userId, PlatformPermission permission,
            ProjectRole minimumRole = ProjectRole.Viewer,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FeedFake : ITaskFeedRepository
    {
        public Comment? Comment { get; private set; }
        public TaskEvent? Event { get; private set; }

        public Task<IReadOnlyList<Comment>> GetCommentsAsync(Guid workItemId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Comment>>([]);

        public Task<Comment> AddCommentAsync(Comment comment, CancellationToken cancellationToken = default)
        {
            Comment = comment;
            return Task.FromResult(comment);
        }

        public Task<IReadOnlyList<TaskEvent>> GetEventsAsync(Guid workItemId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TaskEvent>>([]);

        public Task<IReadOnlyList<StageHistory>> GetStageHistoriesAsync(Guid workItemId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StageHistory>>([]);

        public Task AddEventAsync(TaskEvent taskEvent, CancellationToken cancellationToken = default)
        {
            Event = taskEvent;
            return Task.CompletedTask;
        }
    }

    private sealed class WorkItemsFake(WorkItem item) : IWorkItemRepository
    {
        public int AddAssigneeCount { get; private set; }
        public IReadOnlyList<WorkItemAssignee> Assignees { get; init; } = [];
        public Task<WorkItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<WorkItem?>(item);
        public Task<IReadOnlyList<WorkItemAssignee>> GetAssigneesAsync(Guid workItemId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Assignees);
        public Task AddAssigneeAsync(Guid workItemId, string userId, CancellationToken cancellationToken = default)
        {
            AddAssigneeCount++;
            return Task.CompletedTask;
        }
        public Task RemoveAssigneeAsync(Guid workItemId, string userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<WorkItem?> GetForMoveAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<WorkItem>> GetByBoardIdAsync(Guid boardId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<WorkItem>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<WorkItem>> GetSubItemsAsync(Guid parentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkItem> AddAsync(WorkItem workItem, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(WorkItem workItem, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateWithEventAsync(WorkItem workItem, TaskEvent taskEvent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(WorkItem workItem, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<WorkItem>> GetAssignedToUserAsync(string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdatePersonalPrioritiesAsync(string userId, IReadOnlyList<Guid> orderedWorkItemIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<WorkItem>> GetExclusiveToBoardAsync(Guid boardId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkItem>>(new List<WorkItem>());
    }

    private sealed class ProjectAccessFake : IProjectAccessService
    {
        private readonly bool _allow;
        public ProjectAccessFake(bool allow = true) => _allow = allow;
        public Task<ProjectRole?> GetRoleAsync(Guid projectId, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectRole?>(_allow ? ProjectRole.Member : null);
        public Task EnsureAtLeastAsync(Guid projectId, string userId, ProjectRole minimumRole, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
