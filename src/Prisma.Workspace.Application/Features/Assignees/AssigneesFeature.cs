using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Domain.Enums;
using MediatR;
using System.Text.Json;

namespace Prisma.Workspace.Application.Features.Assignees;

/// <summary>Responsável por uma tarefa, com dados de exibição.</summary>
public class AssignedUserDto
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? UserName { get; init; }
    public DateTimeOffset AssignedAt { get; init; }
}

/// <summary>Responsáveis de uma tarefa.</summary>
public record GetAssigneesQuery(Guid WorkItemId, string ActorId) : IRequest<IReadOnlyList<AssignedUserDto>>;

/// <summary>Atribui um usuário à tarefa e registra no feed.</summary>
public record AssignUserCommand(Guid WorkItemId, string TargetUserId, string ActorId, string ActorName) : IRequest;

/// <summary>Remove um responsável da tarefa e registra no feed.</summary>
public record UnassignUserCommand(Guid WorkItemId, string TargetUserId, string ActorId, string ActorName) : IRequest;

public class GetAssigneesQueryHandler : IRequestHandler<GetAssigneesQuery, IReadOnlyList<AssignedUserDto>>
{
    private readonly IWorkItemRepository _workItems;
    private readonly IUserDirectory _users;
    private readonly IWorkItemAccessService _access;

    public GetAssigneesQueryHandler(
        IWorkItemRepository workItems,
        IUserDirectory users,
        IWorkItemAccessService access)
    {
        _workItems = workItems;
        _users = users;
        _access = access;
    }

    public async Task<IReadOnlyList<AssignedUserDto>> Handle(GetAssigneesQuery request, CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(
            request.WorkItemId, request.ActorId, PlatformPermission.View,
            ProjectRole.Viewer, cancellationToken);
        _ = await _workItems.GetByIdAsync(request.WorkItemId, cancellationToken)
            ?? throw new NaoEncontradoException("Tarefa");

        var assignees = await _workItems.GetAssigneesAsync(request.WorkItemId, cancellationToken);
        var users = (await _users.GetByIdsAsync(
            assignees.Select(a => a.UserId), includeInactive: true, cancellationToken))
            .ToDictionary(user => user.Id);

        return assignees.Select(a => new AssignedUserDto
        {
            Id = a.UserId,
            DisplayName = users.TryGetValue(a.UserId, out var user)
                ? user.DisplayName
                : UserDisplayName.Resolve(a.UserId, null, null, null),
            Email = user?.Email,
            UserName = user?.UserName,
            AssignedAt = a.AssignedAt
        }).ToList();
    }
}

public class AssignUserCommandHandler : IRequestHandler<AssignUserCommand>
{
    private readonly IWorkItemRepository _workItems;
    private readonly IUserDirectory _users;
    private readonly ITaskFeedRepository _feed;
    private readonly IWorkItemAccessService _access;
    private readonly IPlatformNotificationPublisher? _notifications;

    public AssignUserCommandHandler(
        IWorkItemRepository workItems,
        IUserDirectory users,
        ITaskFeedRepository feed,
        IWorkItemAccessService access,
        IPlatformNotificationPublisher? notifications = null)
    {
        _workItems = workItems;
        _users = users;
        _feed = feed;
        _access = access;
        _notifications = notifications;
    }

    public async Task Handle(AssignUserCommand request, CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(
            request.WorkItemId, request.ActorId, PlatformPermission.Assign,
            ProjectRole.Member, cancellationToken);
        var nomes = await _users.GetDisplayNamesAsync(new[] { request.TargetUserId }, cancellationToken);
        DomainException.Garantir(nomes.ContainsKey(request.TargetUserId), "Usuário informado não existe.");

        var item = await _workItems.GetByIdAsync(request.WorkItemId, cancellationToken)
            ?? throw new NaoEncontradoException("Tarefa");
        var actor = (await _users.GetByIdsAsync(
            [request.ActorId], includeInactive: true, cancellationToken)).FirstOrDefault();
        var actorName = actor?.DisplayName
            ?? UserDisplayName.Resolve(request.ActorId, null, request.ActorName, null);

        await _workItems.AddAssigneeAsync(request.WorkItemId, request.TargetUserId, cancellationToken);
        await _feed.AddEventAsync(TaskEvent.Registrar(
            request.WorkItemId, request.ActorId, "assigned",
            JsonSerializer.Serialize(new { actorName, target = nomes[request.TargetUserId] })),
            cancellationToken);
        if (_notifications is not null && request.TargetUserId != request.ActorId)
            await _notifications.PublishAsync(new NotificationEnvelope(
                item.Board.OrganizationId, request.TargetUserId, NotificationType.TaskAssigned,
                "Tarefa atribuída a você",
                $"{actorName} atribuiu #{item.Number} {item.Title} a você.",
                $"/projects/{item.Board.ProjectId}/backlog?item={item.Id}",
                WorkItemId: item.Id, ProjectId: item.Board.ProjectId), cancellationToken);
    }
}

public class UnassignUserCommandHandler : IRequestHandler<UnassignUserCommand>
{
    private readonly IWorkItemRepository _workItems;
    private readonly IUserDirectory _users;
    private readonly ITaskFeedRepository _feed;
    private readonly IWorkItemAccessService _access;

    public UnassignUserCommandHandler(
        IWorkItemRepository workItems,
        IUserDirectory users,
        ITaskFeedRepository feed,
        IWorkItemAccessService access)
    {
        _workItems = workItems;
        _users = users;
        _feed = feed;
        _access = access;
    }

    public async Task Handle(UnassignUserCommand request, CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(
            request.WorkItemId, request.ActorId, PlatformPermission.Assign,
            ProjectRole.Member, cancellationToken);
        _ = await _workItems.GetByIdAsync(request.WorkItemId, cancellationToken)
            ?? throw new NaoEncontradoException("Tarefa");

        await _workItems.RemoveAssigneeAsync(request.WorkItemId, request.TargetUserId, cancellationToken);

        var people = await _users.GetByIdsAsync(
            [request.ActorId, request.TargetUserId], includeInactive: true, cancellationToken);
        var actorName = people.FirstOrDefault(person => person.Id == request.ActorId)?.DisplayName
            ?? UserDisplayName.Resolve(request.ActorId, null, request.ActorName, null);
        var target = people.FirstOrDefault(person => person.Id == request.TargetUserId)?.DisplayName
            ?? UserDisplayName.Resolve(request.TargetUserId, null, null, null);
        await _feed.AddEventAsync(TaskEvent.Registrar(
            request.WorkItemId, request.ActorId, "unassigned",
            JsonSerializer.Serialize(new { actorName, target })),
            cancellationToken);
    }
}
