using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Application.Features.Workflow;
using MediatR;

namespace Prisma.Workspace.Application.Features.WorkItems.Commands;

/// <summary>
/// Handler do comando de movimentação (reordenação/coluna) de WorkItem.
/// Sincroniza automaticamente os placements em quadros compatíveis.
/// </summary>
public class MoveWorkItemCommandHandler : IRequestHandler<MoveWorkItemCommand>
{
    private readonly IWorkItemRepository _workItemRepository;
    private readonly IStageRepository _stageRepository;
    private readonly ITaskFeedRepository _taskFeedRepository;
    private readonly IAutomationExecutor? _automationExecutor;
    private readonly IPermissionService? _permissions;
    private readonly IWorkflowRepository? _workflow;
    private readonly IBoardRealtimeNotifier? _realtime;
    private readonly IPlatformNotificationPublisher? _notifications;
    private readonly IUserDirectory? _users;

    public MoveWorkItemCommandHandler(
        IWorkItemRepository workItemRepository,
        IStageRepository stageRepository,
        ITaskFeedRepository taskFeedRepository,
        IAutomationExecutor? automationExecutor = null,
        IPermissionService? permissions = null,
        IWorkflowRepository? workflow = null,
        IBoardRealtimeNotifier? realtime = null,
        IPlatformNotificationPublisher? notifications = null,
        IUserDirectory? users = null)
    {
        _workItemRepository = workItemRepository;
        _stageRepository = stageRepository;
        _taskFeedRepository = taskFeedRepository;
        _automationExecutor = automationExecutor;
        _permissions = permissions;
        _workflow = workflow;
        _realtime = realtime;
        _notifications = notifications;
        _users = users;
    }

    public async Task Handle(MoveWorkItemCommand request, CancellationToken cancellationToken)
    {
        var workItem = await _workItemRepository.GetForMoveAsync(request.WorkItemId, cancellationToken);
        if (workItem is null)
            throw new ArgumentException("O item de trabalho especificado não existe.");

        if (_permissions is not null && request.ActorId is not null)
            await _permissions.EnsureAsync(
                request.ActorId, PlatformPermission.ChangeStatus,
                PermissionScope.WorkItem, workItem.Id, cancellationToken);

        var actorName = request.ActorName;
        if (_users is not null && !string.IsNullOrWhiteSpace(request.ActorId))
        {
            var displayNames = await _users.GetDisplayNamesAsync([request.ActorId], cancellationToken);
            if (displayNames.TryGetValue(request.ActorId, out var displayName)) actorName = displayName;
        }

        // Etapa de destino — deve pertencer ao projeto da tarefa (D83).
        Stage? destStage = null;
        if (request.DestinationStageId.HasValue)
        {
            destStage = await _stageRepository.GetByIdAsync(request.DestinationStageId.Value, cancellationToken);
            if (destStage is null)
                throw new ArgumentException("A etapa de destino não existe.");
            if (destStage.ProjectId != workItem.Board.ProjectId)
                throw new ArgumentException(
                    "A etapa de destino não pertence ao projeto da tarefa.");
        }

        var stageChanged = workItem.StageId != request.DestinationStageId;
        var previousStageId = workItem.StageId;
        var previousStatusId = workItem.WorkflowStatusId;
        var now = DateTimeOffset.UtcNow;

        if (destStage is not null && _workflow is not null)
            await WorkflowMoveGuard.EnsureAllowedAsync(workItem, destStage, _workflow, cancellationToken);

        {
            if (stageChanged)
            {
                var currentHistory = workItem.StageHistories.FirstOrDefault(h => h.LeftAt == null);
                if (currentHistory is not null)
                    currentHistory.LeftAt = now;

                if (request.DestinationStageId.HasValue)
                {
                    workItem.StageHistories.Add(new StageHistory
                    {
                        WorkItemId = workItem.Id,
                        StageId = request.DestinationStageId.Value,
                        EnteredAt = now,
                        ActorId = request.ActorId,
                        ActorName = actorName,
                        Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim()
                    });
                }

                workItem.StageId = request.DestinationStageId;
                workItem.WorkflowStatusId = destStage?.WorkflowStatusId;
            }

            workItem.CompletedAt = destStage?.Category == StageCategory.Done
                ? workItem.CompletedAt ?? now
                : null;
            if (destStage?.Category == StageCategory.InProgress && workItem.StartDate is null)
                workItem.StartDate = DateOnly.FromDateTime(now.UtcDateTime);
        }

        workItem.Position = request.Position;
        workItem.UpdatedAt = now;

        TaskEvent? stageEvent = null;
        if (stageChanged)
        {
            var fromStage = previousStageId.HasValue
                ? await _stageRepository.GetByIdAsync(previousStageId.Value, cancellationToken)
                : null;

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                fromStageId = previousStageId,
                fromStageName = fromStage?.Name,
                toStageId = request.DestinationStageId,
                toStageName = destStage?.Name,
                fromStatusId = previousStatusId,
                toStatusId = destStage?.WorkflowStatusId,
                actorId = request.ActorId,
                actorName,
                reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim()
            });

            stageEvent = new TaskEvent
            {
                Id = Guid.NewGuid(),
                WorkItemId = workItem.Id,
                ActorId = request.ActorId ?? string.Empty,
                Kind = "stage_changed",
                Payload = payload,
                CreatedAt = now
            };

            await _workItemRepository.UpdateWithEventAsync(workItem, stageEvent, cancellationToken);

            if (_notifications is not null)
            {
                var recipients = workItem.Assignees.Select(x => x.UserId)
                    .Concat(workItem.Followers.Select(x => x.UserId))
                    .Append(workItem.ResponsibleId ?? string.Empty)
                    .Where(x => !string.IsNullOrWhiteSpace(x) && x != request.ActorId)
                    .Distinct();
                var link = $"/projects/{workItem.Board.ProjectId}/backlog?item={workItem.Id}";
                await _notifications.PublishManyAsync(recipients.Select(userId => new NotificationEnvelope(
                    workItem.Board.OrganizationId, userId, NotificationType.StatusChanged,
                    "Status da tarefa alterado",
                    $"#{workItem.Number} {workItem.Title} agora está em {destStage?.Name ?? "Backlog"}.",
                    link, WorkItemId: workItem.Id, ProjectId: workItem.Board.ProjectId)), cancellationToken);
            }

            if (request.DestinationStageId.HasValue && _automationExecutor is not null)
                await _automationExecutor.ExecuteStageEnteredAsync(workItem.Id, request.DestinationStageId.Value,
                    request.ActorId ?? string.Empty, cancellationToken);
        }
        else
        {
            await _workItemRepository.UpdateAsync(workItem, cancellationToken);
        }

        if (_realtime is not null)
        {
            await _realtime.BoardChangedAsync(workItem.BoardId,
                stageChanged ? "workItemMoved" : "workItemReordered", workItem.Id, cancellationToken);
        }
    }
}
