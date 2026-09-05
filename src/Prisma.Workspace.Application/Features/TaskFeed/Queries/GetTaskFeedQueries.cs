using Prisma.Workspace.Application.Features.TaskFeed.Dtos;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;
using MediatR;

namespace Prisma.Workspace.Application.Features.TaskFeed.Queries;

/// <summary>Comentários de uma tarefa (mais antigos primeiro).</summary>
public record GetCommentsQuery(Guid WorkItemId, string ActorId) : IRequest<IReadOnlyList<CommentDto>>;

/// <summary>Eventos de sistema de uma tarefa.</summary>
public record GetTaskEventsQuery(Guid WorkItemId, string ActorId) : IRequest<IReadOnlyList<TaskEventDto>>;

/// <summary>Grafo do caminho real percorrido pela tarefa entre etapas.</summary>
public record GetTaskStateGraphQuery(Guid WorkItemId, string ActorId) : IRequest<TaskStateGraphDto>;

public class GetCommentsQueryHandler : IRequestHandler<GetCommentsQuery, IReadOnlyList<CommentDto>>
{
    private readonly ITaskFeedRepository _feed;
    private readonly IWorkItemAccessService _access;

    public GetCommentsQueryHandler(ITaskFeedRepository feed, IWorkItemAccessService access)
        => (_feed, _access) = (feed, access);

    public async Task<IReadOnlyList<CommentDto>> Handle(GetCommentsQuery request, CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(request.WorkItemId, request.ActorId,
            PlatformPermission.View, ProjectRole.Viewer, cancellationToken);
        var comments = await _feed.GetCommentsAsync(request.WorkItemId, cancellationToken);
        return comments.Select(CommentDto.From).ToList();
    }
}

public class GetTaskEventsQueryHandler : IRequestHandler<GetTaskEventsQuery, IReadOnlyList<TaskEventDto>>
{
    private readonly ITaskFeedRepository _feed;
    private readonly IWorkItemAccessService _access;

    public GetTaskEventsQueryHandler(ITaskFeedRepository feed, IWorkItemAccessService access)
        => (_feed, _access) = (feed, access);

    public async Task<IReadOnlyList<TaskEventDto>> Handle(GetTaskEventsQuery request, CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(request.WorkItemId, request.ActorId,
            PlatformPermission.View, ProjectRole.Viewer, cancellationToken);
        var events = await _feed.GetEventsAsync(request.WorkItemId, cancellationToken);
        return events.Select(TaskEventDto.From).ToList();
    }
}

public class GetTaskStateGraphQueryHandler : IRequestHandler<GetTaskStateGraphQuery, TaskStateGraphDto>
{
    private readonly ITaskFeedRepository _feed;
    private readonly IWorkItemAccessService _access;

    public GetTaskStateGraphQueryHandler(ITaskFeedRepository feed, IWorkItemAccessService access)
        => (_feed, _access) = (feed, access);

    public async Task<TaskStateGraphDto> Handle(
        GetTaskStateGraphQuery request,
        CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(request.WorkItemId, request.ActorId,
            PlatformPermission.View, ProjectRole.Viewer, cancellationToken);

        var histories = await _feed.GetStageHistoriesAsync(request.WorkItemId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var nodes = histories
            .GroupBy(history => new { history.StageId, history.Stage.Name })
            .Select(group => new TaskStateNodeDto(
                group.Key.StageId,
                group.Key.Name,
                group.Count(),
                group.Sum(history => (long)Math.Max(0,
                    ((history.LeftAt ?? now) - history.EnteredAt).TotalSeconds)),
                group.Any(history => history.LeftAt is null)))
            .ToList();

        var edges = histories
            .Zip(histories.Skip(1), (source, target) => new TaskStateEdgeDto(
                target.Id,
                source.StageId,
                target.StageId,
                target.EnteredAt,
                target.ActorId,
                target.ActorName,
                target.Reason))
            .ToList();

        return new TaskStateGraphDto(nodes, edges);
    }
}
