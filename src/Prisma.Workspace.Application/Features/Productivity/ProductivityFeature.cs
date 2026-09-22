using System.Text.Json;
using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Features.Workflow;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Prisma.Workspace.Application.Features.Productivity;

public sealed record SavedFilterDto(Guid Id, string Name, string FilterJson);
public sealed record GetSavedFiltersQuery(Guid BoardId, string UserId)
    : IRequest<IReadOnlyList<SavedFilterDto>>;

public sealed class GetSavedFiltersQueryHandler
    : IRequestHandler<GetSavedFiltersQuery, IReadOnlyList<SavedFilterDto>>
{
    private readonly IProductivityRepository _repository;
    private readonly IProjectAccessService _access;

    public GetSavedFiltersQueryHandler(IProductivityRepository repository, IProjectAccessService access)
        => (_repository, _access) = (repository, access);

    public async Task<IReadOnlyList<SavedFilterDto>> Handle(GetSavedFiltersQuery request, CancellationToken ct)
    {
        var board = await ProductivityAccess.GetBoardAsync(_repository, request.BoardId, ct);
        await _access.EnsureAtLeastAsync(board.ProjectId, request.UserId, ProjectRole.Viewer, ct);
        return (await _repository.GetFiltersAsync(request.BoardId, request.UserId, ct))
            .Select(x => new SavedFilterDto(x.Id, x.Name, x.FilterJson))
            .ToList();
    }
}

public sealed record CreateSavedFilterCommand(Guid BoardId, string UserId, string Name, string FilterJson)
    : IRequest<SavedFilterDto>;

public sealed class CreateSavedFilterCommandValidator : AbstractValidator<CreateSavedFilterCommand>
{
    public CreateSavedFilterCommandValidator()
    {
        RuleFor(x => x.BoardId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FilterJson).NotEmpty().MaximumLength(4_000);
    }
}

public sealed class CreateSavedFilterCommandHandler
    : IRequestHandler<CreateSavedFilterCommand, SavedFilterDto>
{
    private readonly IProductivityRepository _repository;
    private readonly IProjectAccessService _access;

    public CreateSavedFilterCommandHandler(IProductivityRepository repository, IProjectAccessService access)
        => (_repository, _access) = (repository, access);

    public async Task<SavedFilterDto> Handle(CreateSavedFilterCommand request, CancellationToken ct)
    {
        try
        {
            using var _ = JsonDocument.Parse(request.FilterJson);
        }
        catch (JsonException)
        {
            throw new DomainException("Filtro inválido.");
        }

        var board = await ProductivityAccess.GetBoardAsync(_repository, request.BoardId, ct);
        await _access.EnsureAtLeastAsync(board.ProjectId, request.UserId, ProjectRole.Member, ct);
        var filter = new SavedFilter
        {
            Id = Guid.NewGuid(),
            BoardId = request.BoardId,
            UserId = request.UserId,
            Name = request.Name.Trim(),
            FilterJson = request.FilterJson,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _repository.AddFilterAsync(filter, ct);
        return new SavedFilterDto(filter.Id, filter.Name, filter.FilterJson);
    }
}

public sealed record DeleteSavedFilterCommand(Guid BoardId, Guid Id, string UserId) : IRequest;

public sealed class DeleteSavedFilterCommandHandler : IRequestHandler<DeleteSavedFilterCommand>
{
    private readonly IProductivityRepository _repository;
    private readonly IProjectAccessService _access;

    public DeleteSavedFilterCommandHandler(IProductivityRepository repository, IProjectAccessService access)
        => (_repository, _access) = (repository, access);

    public async Task Handle(DeleteSavedFilterCommand request, CancellationToken ct)
    {
        var board = await ProductivityAccess.GetBoardAsync(_repository, request.BoardId, ct);
        await _access.EnsureAtLeastAsync(board.ProjectId, request.UserId, ProjectRole.Member, ct);
        await _repository.DeleteFilterAsync(request.BoardId, request.Id, request.UserId, ct);
    }
}

public sealed record AutomationRuleDto(
    Guid Id,
    Guid TriggerStageId,
    string TriggerStageName,
    AutomationActionType ActionType,
    string ActionValue,
    bool IsActive);

public sealed record GetAutomationRulesQuery(Guid BoardId, string UserId)
    : IRequest<IReadOnlyList<AutomationRuleDto>>;

public sealed class GetAutomationRulesQueryHandler
    : IRequestHandler<GetAutomationRulesQuery, IReadOnlyList<AutomationRuleDto>>
{
    private readonly IProductivityRepository _repository;
    private readonly IProjectAccessService _access;
    private readonly IPermissionService _permissions;

    public GetAutomationRulesQueryHandler(
        IProductivityRepository repository,
        IProjectAccessService access,
        IPermissionService permissions)
        => (_repository, _access, _permissions) = (repository, access, permissions);

    public async Task<IReadOnlyList<AutomationRuleDto>> Handle(
        GetAutomationRulesQuery request,
        CancellationToken ct)
    {
        var board = await ProductivityAccess.GetBoardAsync(_repository, request.BoardId, ct);
        var projectId = board.ProjectId;
        await _access.EnsureAtLeastAsync(projectId, request.UserId, ProjectRole.Viewer, ct);
        await _permissions.EnsureAsync(
            request.UserId, PlatformPermission.View, PermissionScope.Project, projectId, ct);
        return (await _repository.GetRulesAsync(request.BoardId, ct))
            .Select(AutomationRuleMapper.Map)
            .ToList();
    }
}

public sealed record CreateAutomationRuleCommand(
    Guid BoardId,
    Guid TriggerStageId,
    AutomationActionType ActionType,
    string ActionValue,
    bool IsActive,
    string ActorId) : IRequest<AutomationRuleDto>;

public sealed record UpdateAutomationRuleCommand(
    Guid BoardId,
    Guid Id,
    Guid TriggerStageId,
    AutomationActionType ActionType,
    string ActionValue,
    bool IsActive,
    string ActorId) : IRequest<AutomationRuleDto>;

public sealed class AutomationRuleCommandValidator : AbstractValidator<CreateAutomationRuleCommand>
{
    public AutomationRuleCommandValidator()
    {
        RuleFor(x => x.BoardId).NotEmpty();
        RuleFor(x => x.TriggerStageId).NotEmpty();
        RuleFor(x => x.ActionType).IsInEnum();
        RuleFor(x => x.ActionValue).NotEmpty().MaximumLength(450);
        RuleFor(x => x.ActorId).NotEmpty();
    }
}

public sealed class UpdateAutomationRuleCommandValidator : AbstractValidator<UpdateAutomationRuleCommand>
{
    public UpdateAutomationRuleCommandValidator()
    {
        RuleFor(x => x.BoardId).NotEmpty();
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TriggerStageId).NotEmpty();
        RuleFor(x => x.ActionType).IsInEnum();
        RuleFor(x => x.ActionValue).NotEmpty().MaximumLength(450);
        RuleFor(x => x.ActorId).NotEmpty();
    }
}

public sealed class CreateAutomationRuleCommandHandler
    : IRequestHandler<CreateAutomationRuleCommand, AutomationRuleDto>
{
    private readonly IProductivityRepository _repository;
    private readonly IProjectAccessService _access;
    private readonly IPermissionService _permissions;
    private readonly IUserDirectory _users;

    public CreateAutomationRuleCommandHandler(
        IProductivityRepository repository,
        IProjectAccessService access,
        IPermissionService permissions,
        IUserDirectory users)
        => (_repository, _access, _permissions, _users) = (repository, access, permissions, users);

    public async Task<AutomationRuleDto> Handle(CreateAutomationRuleCommand request, CancellationToken ct)
    {
        var board = await ProductivityAccess.GetBoardAsync(_repository, request.BoardId, ct);
        await ProductivityAccess.EnsureAutomationAdminAsync(
            board, request.ActorId, PlatformPermission.Edit, _access, _permissions, ct);
        var normalizedValue = await AutomationRuleGuard.ValidateAsync(
            _repository, _users, request.BoardId, request.TriggerStageId,
            request.ActionType, request.ActionValue, request.IsActive, null, ct);

        var rule = new AutomationRule
        {
            Id = Guid.NewGuid(),
            BoardId = request.BoardId,
            TriggerStageId = request.TriggerStageId,
            ActionType = request.ActionType,
            ActionValue = normalizedValue,
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _repository.AddRuleAsync(rule, ct);
        var stage = await _repository.GetStageAsync(rule.TriggerStageId, ct)
            ?? throw new NaoEncontradoException("Etapa");
        return AutomationRuleMapper.Map(rule, stage.Name);
    }
}

public sealed class UpdateAutomationRuleCommandHandler
    : IRequestHandler<UpdateAutomationRuleCommand, AutomationRuleDto>
{
    private readonly IProductivityRepository _repository;
    private readonly IProjectAccessService _access;
    private readonly IPermissionService _permissions;
    private readonly IUserDirectory _users;

    public UpdateAutomationRuleCommandHandler(
        IProductivityRepository repository,
        IProjectAccessService access,
        IPermissionService permissions,
        IUserDirectory users)
        => (_repository, _access, _permissions, _users) = (repository, access, permissions, users);

    public async Task<AutomationRuleDto> Handle(UpdateAutomationRuleCommand request, CancellationToken ct)
    {
        var board = await ProductivityAccess.GetBoardAsync(_repository, request.BoardId, ct);
        await ProductivityAccess.EnsureAutomationAdminAsync(
            board, request.ActorId, PlatformPermission.Edit, _access, _permissions, ct);
        var rule = await _repository.GetRuleAsync(request.Id, ct)
            ?? throw new NaoEncontradoException("Automação");
        DomainException.Garantir(rule.BoardId == request.BoardId, "A automação não pertence ao quadro.");

        var normalizedValue = await AutomationRuleGuard.ValidateAsync(
            _repository, _users, request.BoardId, request.TriggerStageId,
            request.ActionType, request.ActionValue, request.IsActive, request.Id, ct);
        rule.TriggerStageId = request.TriggerStageId;
        rule.ActionType = request.ActionType;
        rule.ActionValue = normalizedValue;
        rule.IsActive = request.IsActive;
        await _repository.SaveAsync(ct);
        var stage = await _repository.GetStageAsync(rule.TriggerStageId, ct)
            ?? throw new NaoEncontradoException("Etapa");
        return AutomationRuleMapper.Map(rule, stage.Name);
    }
}

public sealed record DeleteAutomationRuleCommand(Guid BoardId, Guid Id, string ActorId) : IRequest;

public sealed class DeleteAutomationRuleCommandHandler : IRequestHandler<DeleteAutomationRuleCommand>
{
    private readonly IProductivityRepository _repository;
    private readonly IProjectAccessService _access;
    private readonly IPermissionService _permissions;

    public DeleteAutomationRuleCommandHandler(
        IProductivityRepository repository,
        IProjectAccessService access,
        IPermissionService permissions)
        => (_repository, _access, _permissions) = (repository, access, permissions);

    public async Task Handle(DeleteAutomationRuleCommand request, CancellationToken ct)
    {
        var board = await ProductivityAccess.GetBoardAsync(_repository, request.BoardId, ct);
        await ProductivityAccess.EnsureAutomationAdminAsync(
            board, request.ActorId, PlatformPermission.Delete, _access, _permissions, ct);
        var rule = await _repository.GetRuleAsync(request.Id, ct)
            ?? throw new NaoEncontradoException("Automação");
        DomainException.Garantir(rule.BoardId == request.BoardId, "A automação não pertence ao quadro.");
        await _repository.DeleteRuleAsync(rule, ct);
    }
}

public static class AutomationRuleGuard
{
    public static async Task<string> ValidateAsync(
        IProductivityRepository repository,
        IUserDirectory users,
        Guid boardId,
        Guid triggerStageId,
        AutomationActionType actionType,
        string actionValue,
        bool isActive,
        Guid? excludingRuleId,
        CancellationToken ct)
    {
        var trigger = await repository.GetStageAsync(triggerStageId, ct);
        var board = await repository.GetBoardAsync(boardId, ct);
        DomainException.Garantir(board is not null, "Quadro não encontrado.");
        DomainException.Garantir(trigger is not null && trigger.ProjectId == board.ProjectId && trigger.BoardId == boardId,
            "A etapa de disparo não pertence ao projeto do quadro.");

        string normalized;
        switch (actionType)
        {
            case AutomationActionType.AssignUser:
                normalized = actionValue.Trim();
                DomainException.Garantir(await users.GetByIdAsync(normalized, ct) is not null,
                    "O usuário de destino não pertence à organização.");
                break;
            case AutomationActionType.MoveToStage:
                DomainException.Garantir(Guid.TryParse(actionValue, out var destinationId),
                    "A etapa de destino é inválida.");
                var destination = await repository.GetStageAsync(destinationId, ct);
                DomainException.Garantir(destination is not null && destination.ProjectId == board.ProjectId && destination.BoardId == boardId,
                    "A etapa de destino não pertence ao projeto do quadro.");
                DomainException.Garantir(destinationId != triggerStageId,
                    "A automação não pode mover uma tarefa para a própria etapa de disparo.");
                normalized = destinationId.ToString();
                break;
            case AutomationActionType.SetPriority:
                DomainException.Garantir(Enum.TryParse<Priority>(actionValue, true, out var priority)
                    && Enum.IsDefined(priority), "Prioridade inválida.");
                normalized = priority.ToString();
                break;
            case AutomationActionType.AddTag:
                DomainException.Garantir(Guid.TryParse(actionValue, out var tagId)
                    && await repository.TagExistsAsync(tagId, ct), "Tag inválida.");
                normalized = tagId.ToString();
                break;
            default:
                throw new DomainException("Ação de automação inválida.");
        }

        var existing = await repository.GetRulesAsync(boardId, ct);
        DomainException.Garantir(!existing.Any(x => x.Id != excludingRuleId
            && x.TriggerStageId == triggerStageId
            && x.ActionType == actionType
            && string.Equals(x.ActionValue, normalized, StringComparison.OrdinalIgnoreCase)),
            "Já existe uma automação igual neste quadro.");

        if (isActive && actionType == AutomationActionType.MoveToStage)
            DomainException.Garantir(!existing.Any(x => x.Id != excludingRuleId
                && x.IsActive
                && x.TriggerStageId == triggerStageId
                && x.ActionType == AutomationActionType.MoveToStage),
                "Uma etapa pode ter somente uma automação ativa de movimentação.");

        if (isActive && actionType == AutomationActionType.MoveToStage)
        {
            var edges = existing
                .Where(x => x.Id != excludingRuleId && x.IsActive
                    && x.ActionType == AutomationActionType.MoveToStage
                    && Guid.TryParse(x.ActionValue, out _))
                .Select(x => (x.TriggerStageId, Guid.Parse(x.ActionValue)))
                .Append((triggerStageId, Guid.Parse(normalized)));
            DomainException.Garantir(!HasCycle(edges),
                "A automação criaria um ciclo entre etapas.");
        }

        return normalized;
    }

    public static bool HasCycle(IEnumerable<(Guid Source, Guid Target)> edges)
    {
        var graph = edges.GroupBy(x => x.Source)
            .ToDictionary(x => x.Key, x => x.Select(edge => edge.Target).Distinct().ToArray());
        var visiting = new HashSet<Guid>();
        var visited = new HashSet<Guid>();

        bool Visit(Guid node)
        {
            if (visiting.Contains(node)) return true;
            if (!visited.Add(node)) return false;
            visiting.Add(node);
            if (graph.TryGetValue(node, out var targets) && targets.Any(Visit)) return true;
            visiting.Remove(node);
            return false;
        }

        return graph.Keys.Any(Visit);
    }
}

public enum BulkActionType
{
    Move = 1,
    Assign = 2,
    Unassign = 3,
    SetPriority = 4,
    AddTag = 5,
    RemoveTag = 6,
    SetSprint = 7
}

public sealed record BulkWorkItemsCommand(
    Guid BoardId,
    IReadOnlyList<Guid> WorkItemIds,
    BulkActionType Action,
    string? TargetValue,
    Priority? Priority,
    string ActorId) : IRequest;

public sealed class BulkWorkItemsCommandValidator : AbstractValidator<BulkWorkItemsCommand>
{
    public BulkWorkItemsCommandValidator()
    {
        RuleFor(x => x.BoardId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Action).IsInEnum();
        RuleFor(x => x.WorkItemIds)
            .NotNull()
            .Must(x => x.Count is > 0 and <= 200)
            .WithMessage("Selecione entre 1 e 200 itens.")
            .Must(x => x.Distinct().Count() == x.Count)
            .WithMessage("A seleção não pode conter tarefas repetidas.");
    }
}

public sealed class BulkWorkItemsCommandHandler : IRequestHandler<BulkWorkItemsCommand>
{
    private readonly IProductivityRepository _repository;
    private readonly IProjectAccessService _access;
    private readonly IPermissionService _permissions;
    private readonly IUserDirectory _users;
    private readonly IWorkflowRepository _workflow;
    private readonly IAutomationExecutor? _automation;
    private readonly IBoardRealtimeNotifier? _realtime;
    private readonly IPlatformNotificationPublisher? _notifications;

    public BulkWorkItemsCommandHandler(
        IProductivityRepository repository,
        IProjectAccessService access,
        IPermissionService permissions,
        IUserDirectory users,
        IWorkflowRepository workflow,
        IAutomationExecutor? automation = null,
        IBoardRealtimeNotifier? realtime = null,
        IPlatformNotificationPublisher? notifications = null)
        => (_repository, _access, _permissions, _users, _workflow, _automation, _realtime, _notifications)
            = (repository, access, permissions, users, workflow, automation, realtime, notifications);

    public async Task Handle(BulkWorkItemsCommand request, CancellationToken ct)
    {
        var board = await ProductivityAccess.GetBoardAsync(_repository, request.BoardId, ct);
        var projectId = board.ProjectId;
        await _access.EnsureAtLeastAsync(projectId, request.ActorId, ProjectRole.Member, ct);
        await _permissions.EnsureAsync(
            request.ActorId, PermissionFor(request.Action), PermissionScope.Project, projectId, ct);

        var ids = request.WorkItemIds.Distinct().ToArray();
        var items = await _repository.GetItemsAsync(request.BoardId, ids, ct);
        DomainException.Garantir(items.Count == ids.Length,
            "Uma ou mais tarefas não pertencem ao quadro.");

        var target = await ValidateTargetAsync(request, board, items, ct);
        var now = DateTimeOffset.UtcNow;
        var changedItems = new List<WorkItem>();
        var events = new List<TaskEvent>();

        foreach (var item in items)
        {
            if (!Apply(item, request, target, now)) continue;
            item.UpdatedAt = now;
            changedItems.Add(item);
            events.Add(TaskEvent.Registrar(item.Id, request.ActorId, "bulk_action",
                JsonSerializer.Serialize(new
                {
                    action = request.Action.ToString(),
                    request.TargetValue,
                    priority = request.Priority?.ToString()
                })));
        }

        if (changedItems.Count == 0) return;
        _repository.AddTaskEvents(events);
        await _repository.SaveAsync(ct);

        if (request.Action == BulkActionType.Move && target.Stage is not null && _automation is not null)
            foreach (var item in changedItems)
                await _automation.ExecuteStageEnteredAsync(item.Id, target.Stage.Id, request.ActorId, ct);

        if (_notifications is not null)
            await PublishNotificationsAsync(board, changedItems, request, target, ct);
        if (_realtime is not null)
            await _realtime.BoardChangedAsync(request.BoardId, "workItemsBulkChanged", null, ct);
    }

    private async Task<BulkTarget> ValidateTargetAsync(
        BulkWorkItemsCommand request,
        Board board,
        IReadOnlyList<WorkItem> items,
        CancellationToken ct)
    {
        Guid? targetId = Guid.TryParse(request.TargetValue, out var parsed) ? parsed : null;
        Stage? stage = null;
        Sprint? sprint = null;

        switch (request.Action)
        {
            case BulkActionType.Move:
                DomainException.Garantir(targetId.HasValue, "Etapa de destino obrigatória.");
                stage = await _repository.GetStageAsync(targetId!.Value, ct);
                DomainException.Garantir(stage is not null && stage.ProjectId == board.ProjectId && stage.BoardId == board.Id,
                    "Etapa de destino inválida.");
                foreach (var item in items.Where(x => x.StageId != stage.Id))
                    await WorkflowMoveGuard.EnsureAllowedAsync(item, stage, _workflow, ct);
                break;
            case BulkActionType.Assign:
                DomainException.Garantir(!string.IsNullOrWhiteSpace(request.TargetValue),
                    "Usuário de destino obrigatório.");
                DomainException.Garantir(await _users.GetByIdAsync(request.TargetValue, ct) is not null,
                    "Usuário de destino inválido.");
                var targetRole = await _access.GetRoleAsync(board.ProjectId, request.TargetValue, ct);
                DomainException.Garantir(targetRole is not null,
                    "Conceda acesso ao projeto antes de atribuir a tarefa.");
                break;
            case BulkActionType.Unassign:
                DomainException.Garantir(!string.IsNullOrWhiteSpace(request.TargetValue),
                    "Usuário de destino obrigatório.");
                DomainException.Garantir(items.Any(x => x.Assignees.Any(a => a.UserId == request.TargetValue)),
                    "O usuário não está atribuído às tarefas selecionadas.");
                break;
            case BulkActionType.SetPriority:
                DomainException.Garantir(request.Priority.HasValue
                    && Enum.IsDefined(request.Priority.Value), "Prioridade obrigatória.");
                break;
            case BulkActionType.AddTag:
            case BulkActionType.RemoveTag:
                DomainException.Garantir(targetId.HasValue
                    && await _repository.TagExistsAsync(targetId.Value, ct), "Tag inválida.");
                break;
            case BulkActionType.SetSprint:
                if (targetId.HasValue)
                {
                    sprint = await _repository.GetSprintAsync(targetId.Value, ct);
                    DomainException.Garantir(sprint is not null
                        && sprint.ProjectId == board.ProjectId
                        && sprint.Status is SprintStatus.Planned or SprintStatus.Active,
                        "A sprint deve pertencer ao projeto e estar planejada ou ativa.");
                }
                else
                {
                    DomainException.Garantir(string.IsNullOrWhiteSpace(request.TargetValue),
                        "Sprint inválida.");
                }
                break;
            default:
                throw new DomainException("Ação em massa inválida.");
        }

        return new BulkTarget(targetId, stage, sprint);
    }

    private static bool Apply(
        WorkItem item,
        BulkWorkItemsCommand request,
        BulkTarget target,
        DateTimeOffset now)
    {
        switch (request.Action)
        {
            case BulkActionType.Move when item.StageId != target.Stage!.Id:
                foreach (var history in item.StageHistories.Where(x => x.LeftAt is null))
                    history.LeftAt = now;
                item.StageId = target.Stage.Id;
                item.WorkflowStatusId = target.Stage.WorkflowStatusId;
                item.StageHistories.Add(new StageHistory
                {
                    WorkItemId = item.Id,
                    StageId = target.Stage.Id,
                    EnteredAt = now
                });
                item.CompletedAt = target.Stage.Category == StageCategory.Done
                    ? item.CompletedAt ?? now
                    : null;
                if (target.Stage.Category == StageCategory.InProgress && item.StartDate is null)
                    item.StartDate = DateOnly.FromDateTime(now.UtcDateTime);
                return true;
            case BulkActionType.Assign when item.Assignees.All(x => x.UserId != request.TargetValue):
                item.Assignees.Add(new WorkItemAssignee
                {
                    WorkItemId = item.Id,
                    UserId = request.TargetValue!,
                    AssignedAt = now
                });
                return true;
            case BulkActionType.Unassign:
                var assignee = item.Assignees.FirstOrDefault(x => x.UserId == request.TargetValue);
                if (assignee is null) return false;
                item.Assignees.Remove(assignee);
                return true;
            case BulkActionType.SetPriority when item.Priority != request.Priority:
                item.Priority = request.Priority!.Value;
                return true;
            case BulkActionType.AddTag when item.WorkItemTags.All(x => x.TagId != target.Id):
                item.WorkItemTags.Add(new WorkItemTag { WorkItemId = item.Id, TagId = target.Id!.Value });
                return true;
            case BulkActionType.RemoveTag:
                var tag = item.WorkItemTags.FirstOrDefault(x => x.TagId == target.Id);
                if (tag is null) return false;
                item.WorkItemTags.Remove(tag);
                return true;
            case BulkActionType.SetSprint when item.SprintId != target.Id:
                item.SprintId = target.Id;
                return true;
            default:
                return false;
        }
    }

    private async Task PublishNotificationsAsync(
        Board board,
        IReadOnlyList<WorkItem> items,
        BulkWorkItemsCommand request,
        BulkTarget target,
        CancellationToken ct)
    {
        var envelopes = new List<NotificationEnvelope>();
        foreach (var item in items)
        {
            var recipients = item.Assignees.Select(x => x.UserId)
                .Concat(item.Followers.Select(x => x.UserId))
                .Append(item.ResponsibleId ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x) && x != request.ActorId)
                .Distinct();
            if (request.Action == BulkActionType.Assign && request.TargetValue != request.ActorId)
                recipients = recipients.Append(request.TargetValue!).Distinct();

            var type = request.Action == BulkActionType.Assign
                ? NotificationType.TaskAssigned
                : NotificationType.StatusChanged;
            var detail = request.Action == BulkActionType.Move
                ? $"movida para {target.Stage!.Name}"
                : $"alterada por ação em massa ({request.Action})";
            var link = $"/projects/{board.ProjectId}/backlog?item={item.Id}";
            envelopes.AddRange(recipients.Select(userId => new NotificationEnvelope(
                board.OrganizationId,
                userId,
                type,
                request.Action == BulkActionType.Assign ? "Tarefa atribuída" : "Tarefa atualizada",
                $"#{item.Number} {item.Title} foi {detail}.",
                link,
                WorkItemId: item.Id,
                ProjectId: board.ProjectId,
                DeduplicationKey: $"bulk:{request.Action}:{item.Id}:{userId}:{item.UpdatedAt:O}")));
        }
        if (envelopes.Count > 0)
            await _notifications!.PublishManyAsync(envelopes, ct);
    }

    private static PlatformPermission PermissionFor(BulkActionType action) => action switch
    {
        BulkActionType.Move => PlatformPermission.ChangeStatus,
        BulkActionType.Assign or BulkActionType.Unassign => PlatformPermission.Assign,
        _ => PlatformPermission.Edit
    };

    private sealed record BulkTarget(Guid? Id, Stage? Stage, Sprint? Sprint);
}

internal static class AutomationRuleMapper
{
    public static AutomationRuleDto Map(AutomationRule rule)
        => Map(rule, rule.TriggerStage.Name);

    public static AutomationRuleDto Map(AutomationRule rule, string triggerStageName)
        => new(rule.Id, rule.TriggerStageId, triggerStageName,
            rule.ActionType, rule.ActionValue, rule.IsActive);
}

internal static class ProductivityAccess
{
    public static async Task<Board> GetBoardAsync(
        IProductivityRepository repository,
        Guid boardId,
        CancellationToken ct)
    {
        var board = await repository.GetBoardAsync(boardId, ct)
            ?? throw new NaoEncontradoException("Quadro");
        return board;
    }

    public static async Task EnsureAutomationAdminAsync(
        Board board,
        string actorId,
        PlatformPermission permission,
        IProjectAccessService access,
        IPermissionService permissions,
        CancellationToken ct)
    {
        var projectId = board.ProjectId;
        await access.EnsureAtLeastAsync(projectId, actorId, ProjectRole.ProjectAdmin, ct);
        await permissions.EnsureAsync(actorId, permission, PermissionScope.Project, projectId, ct);
    }
}
