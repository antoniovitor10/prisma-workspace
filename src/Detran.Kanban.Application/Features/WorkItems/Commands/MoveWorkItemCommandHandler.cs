using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Domain.Exceptions;
using Detran.Kanban.Application.Features.Workflow;
using MediatR;

namespace Detran.Kanban.Application.Features.WorkItems.Commands;

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

        // Etapa de destino — seu BoardId indica em qual quadro ocorre a movimentação
        Stage? destStage = null;
        Guid destBoardId = workItem.BoardId;
        if (request.DestinationStageId.HasValue)
        {
            destStage = await _stageRepository.GetByIdAsync(request.DestinationStageId.Value, cancellationToken);
            if (destStage is null)
                throw new ArgumentException("A etapa de destino não existe.");
            destBoardId = destStage.BoardId;
        }

        // O item deve ter placement no quadro de destino ou ser o quadro home
        bool temPlacementNoDestino = workItem.BoardPlacements.Any(p => p.BoardId == destBoardId);
        bool ehQuadroHome = workItem.BoardId == destBoardId;
        if (!temPlacementNoDestino && !ehQuadroHome)
            throw new ArgumentException(
                $"O item não está posicionado no quadro de destino ({destBoardId}).");

        var stageChanged = workItem.StageId != request.DestinationStageId || !ehQuadroHome;
        var previousStageId = workItem.StageId;
        var previousStatusId = workItem.WorkflowStatusId;
        var now = DateTimeOffset.UtcNow;

        if (destStage is not null && _workflow is not null)
            await WorkflowMoveGuard.EnsureAllowedAsync(workItem, destStage, _workflow, cancellationToken);

        // Resolve toda a sincronização antes de alterar o agregado. Se algum quadro for
        // incompatível, a falha precisa deixar o item exatamente como estava.
        var compatibleStages = new Dictionary<Guid, Stage>();
        if (destStage?.WorkflowStatusId is not null)
        {
            foreach (var placement in workItem.BoardPlacements.Where(p => p.BoardId != destBoardId))
            {
                var stages = await _stageRepository.GetByBoardIdAsync(placement.BoardId, cancellationToken);
                var compatibleStage = stages.FirstOrDefault(s => s.WorkflowStatusId == destStage.WorkflowStatusId);
                if (compatibleStage is null)
                    throw new DomainException(
                        $"O quadro {placement.BoardId} não possui uma etapa com o status " +
                        $"'{destStage.WorkflowStatusId}'. Ajuste o mapeamento antes de mover o item.");
                compatibleStages[placement.BoardId] = compatibleStage;
            }
        }

        // Atualiza placement no quadro de destino
        var placementDestino = workItem.BoardPlacements.FirstOrDefault(p => p.BoardId == destBoardId);
        if (placementDestino is not null)
        {
            placementDestino.StageId = request.DestinationStageId;
            placementDestino.Position = request.Position;
            placementDestino.UpdatedAt = now;
        }

        // Sincroniza quadro home se destino == home
        if (ehQuadroHome)
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
        else
        {
            // Movimento em quadro secundário → atualiza status canônico do item
            workItem.WorkflowStatusId = destStage?.WorkflowStatusId;
            workItem.CompletedAt = destStage?.Category == StageCategory.Done
                ? workItem.CompletedAt ?? now
                : null;
        }

        // Sincroniza placements dos demais quadros pelo WorkflowStatusId do destino
        if (destStage is not null)
        {
            foreach (var outroPl in workItem.BoardPlacements.Where(p => p.BoardId != destBoardId))
            {
                if (destStage.WorkflowStatusId is null)
                {
                    // Sem status canônico — não sincroniza outros quadros
                    continue;
                }
                var stageCompativel = compatibleStages[outroPl.BoardId];
                outroPl.StageId = stageCompativel.Id;
                outroPl.UpdatedAt = now;
                // Se for o quadro home, sincroniza também StageId do item
                if (outroPl.BoardId == workItem.BoardId)
                {
                    workItem.StageId = stageCompativel.Id;
                }
            }
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
                var link = workItem.Board.ProjectId.HasValue
                    ? $"/projects/{workItem.Board.ProjectId}/backlog?item={workItem.Id}"
                    : $"/boards/{workItem.BoardId}?item={workItem.Id}";
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
            // Notifica todos os quadros afetados
            var boardsAfetados = workItem.BoardPlacements.Select(p => p.BoardId)
                .Append(workItem.BoardId)
                .Distinct();
            foreach (var bid in boardsAfetados)
                await _realtime.BoardChangedAsync(bid,
                    stageChanged ? "workItemMoved" : "workItemReordered", workItem.Id, cancellationToken);
        }
    }
}
