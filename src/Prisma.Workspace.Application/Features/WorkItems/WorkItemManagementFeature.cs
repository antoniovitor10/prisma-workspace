using System.Globalization;
using System.Text.Json;
using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Features.Projects;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Application.Features.Workflow;
using Prisma.Workspace.Application.Features.ExternalPortal;
using FluentValidation;
using MediatR;

namespace Prisma.Workspace.Application.Features.WorkItems;

public record WorkItemPersonDto(string UserId, string? DisplayName);
public record WorkItemLinkDto(
    Guid Id,
    WorkItemLinkType Type,
    bool IsIncoming,
    Guid RelatedWorkItemId,
    long RelatedNumber,
    string RelatedTitle,
    string? RelatedProjectKey);
public record WorkItemCustomFieldValueDto(
    Guid FieldId,
    string Name,
    CustomFieldType Type,
    bool IsRequired,
    string? OptionsJson,
    string? Value);
public record WorkItemChecklistDto(Guid Id, string Text, bool Done, double Position);
public record WorkItemSubtaskDto(Guid Id, long Number, string Title, bool IsArchived, DateTimeOffset? CompletedAt);
public record WorkItemExternalCommunicationDto(
    string Protocol,
    ExternalRequestTriageStatus TriageStatus,
    string? RequesterPhone,
    int? Rating,
    string? RatingComment,
    DateTimeOffset? CompletionConfirmedAt,
    IReadOnlyList<ExternalRequestMessageDto> Messages);

public record WorkItemDetailsDto(
    Guid Id,
    long Number,
    string Reference,
    Guid BoardId,
    string BoardName,
    Guid? ProjectId,
    string? ProjectKey,
    Guid? TeamId,
    string? TeamName,
    Guid? StageId,
    string? StageName,
    Guid? WorkflowStatusId,
    string? WorkflowStatusName,
    Guid? ParentId,
    Guid? SprintId,
    string? SprintName,
    WorkItemKind Kind,
    WorkItemOrigin Origin,
    string Title,
    string? Subtitle,
    string? Description,
    Priority Priority,
    string? ResponsibleId,
    string? ResponsibleName,
    IReadOnlyList<WorkItemPersonDto> Participants,
    string? RequesterId,
    string? RequesterName,
    string? RequesterEmail,
    DateOnly? StartDate,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt,
    decimal? EstimatedHours,
    decimal? RemainingHours,
    decimal RealizedHours,
    int? Points,
    Guid? TaskTypeId,
    string? TaskTypeName,
    IReadOnlyList<ProjectTagDto> Tags,
    string? AcceptanceCriteria,
    bool IsArchived,
    IReadOnlyList<WorkItemChecklistDto> Checklist,
    IReadOnlyList<WorkItemSubtaskDto> Subtasks,
    int AttachmentsCount,
    int CommentsCount,
    IReadOnlyList<string> FollowerIds,
    bool IsFollowing,
    IReadOnlyList<WorkItemLinkDto> Links,
    IReadOnlyList<WorkItemCustomFieldValueDto> CustomFields,
    WorkItemExternalCommunicationDto? ExternalCommunication,
    string Version);

public record GetWorkItemDetailsQuery(Guid WorkItemId, string UserId) : IRequest<WorkItemDetailsDto>;

public class GetWorkItemDetailsQueryHandler : IRequestHandler<GetWorkItemDetailsQuery, WorkItemDetailsDto>
{
    private readonly IWorkItemManagementRepository _management;
    private readonly IProjectAccessService _projectAccess;
    private readonly IPermissionService _permissions;
    private readonly IUserDirectory _users;

    public GetWorkItemDetailsQueryHandler(
        IWorkItemManagementRepository management,
        IProjectAccessService projectAccess,
        IPermissionService permissions,
        IUserDirectory users)
        => (_management, _projectAccess, _permissions, _users)
            = (management, projectAccess, permissions, users);

    public async Task<WorkItemDetailsDto> Handle(GetWorkItemDetailsQuery request, CancellationToken ct)
    {
        var item = await _management.GetDetailedAsync(request.WorkItemId, ct)
            ?? throw new NaoEncontradoException("Tarefa");
        await WorkItemAccessGuard.EnsureAsync(
            item, request.UserId, ProjectRole.Viewer, PlatformPermission.View,
            _projectAccess, _permissions, ct);

        var ids = item.Assignees.Select(x => x.UserId)
            .Concat(item.Followers.Select(x => x.UserId))
            .Append(item.ResponsibleId ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct();
        var people = await _users.GetByIdsAsync(ids, includeInactive: true, cancellationToken: ct);
        var names = people.ToDictionary(person => person.Id, person => person.DisplayName);
        var project = item.Board.Project;
        var reference = project is null || item.Number <= 0
            ? item.Id.ToString("N")[..8].ToUpperInvariant()
            : $"{project.Key}-{item.Number}";

        var outgoing = item.OutgoingLinks.Select(x => MapLink(
            x, false, x.TargetWorkItem, x.TargetWorkItem.Board.Project?.Key));
        var incoming = item.IncomingLinks.Select(x => MapLink(
            x, true, x.SourceWorkItem, x.SourceWorkItem.Board.Project?.Key));
        var values = item.CustomFieldValues.ToDictionary(x => x.FieldDefinitionId, x => x.Value);
        var definitions = project?.CustomFields
            .Where(x => x.IsActive || values.ContainsKey(x.Id))
            .OrderBy(x => x.Position)
            .Select(x => new WorkItemCustomFieldValueDto(
                x.Id, x.Name, x.Type, x.IsRequired, x.OptionsJson,
                values.GetValueOrDefault(x.Id)))
            .ToList() ?? [];

        return new WorkItemDetailsDto(
            item.Id, item.Number, reference,
            item.BoardId, item.Board.Name, item.Board.ProjectId, project?.Key,
            item.TeamId ?? item.Board.TeamId, item.Team?.Name ?? item.Board.Team?.Name,
            item.StageId, item.Stage?.Name,
            item.WorkflowStatusId, item.WorkflowStatus?.Name,
            item.ParentId, item.SprintId, item.Sprint?.Name,
            item.Kind, item.Origin, item.Title, item.Subtitle, item.Description, item.Priority,
            item.ResponsibleId, DisplayName(item.ResponsibleId, names),
            item.Assignees.OrderBy(x => x.AssignedAt)
                .Select(x => new WorkItemPersonDto(x.UserId, DisplayName(x.UserId, names))).ToList(),
            item.RequesterId, item.RequesterName, item.RequesterEmail,
            item.StartDate, item.DueDate, item.CreatedAt, item.UpdatedAt, item.CompletedAt,
            item.EstimatedHours, item.RemainingHours,
            Math.Round(item.TimeEntries.Sum(x =>
                (decimal)((x.EndedAt ?? DateTimeOffset.UtcNow) - x.StartedAt).TotalHours), 2),
            item.Points, item.TaskTypeId, item.TaskType?.Name,
            item.WorkItemTags.Where(x => x.Tag is not null)
                .Select(x => new ProjectTagDto(x.TagId, x.Tag.Name, x.Tag.Color)).ToList(),
            item.AcceptanceCriteria, item.IsArchived,
            item.ChecklistItems.OrderBy(x => x.Position)
                .Select(x => new WorkItemChecklistDto(x.Id, x.Text, x.Done, x.Position)).ToList(),
            item.SubItems.OrderBy(x => x.Position)
                .Select(x => new WorkItemSubtaskDto(x.Id, x.Number, x.Title, x.IsArchived, x.CompletedAt)).ToList(),
            item.Attachments.Count, item.Comments.Count,
            item.Followers.Select(x => x.UserId).ToList(),
            item.Followers.Any(x => x.UserId == request.UserId),
            outgoing.Concat(incoming).OrderBy(x => x.RelatedTitle).ToList(),
            definitions,
            item.ExternalRequest is null ? null : new WorkItemExternalCommunicationDto(
                item.ExternalRequest.Protocol,
                item.ExternalRequest.TriageStatus,
                item.ExternalRequest.RequesterPhone,
                item.ExternalRequest.Rating,
                item.ExternalRequest.RatingComment,
                item.ExternalRequest.CompletionConfirmedAt,
                item.ExternalRequest.Messages.OrderBy(x => x.CreatedAt)
                    .Select(x => new ExternalRequestMessageDto(
                        x.Id, x.AuthorType, x.AuthorName, x.Content, x.CreatedAt)).ToList()),
            item.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(item.RowVersion));
    }

    private static string? DisplayName(string? id, IReadOnlyDictionary<string, string> names)
        => id is null ? null : names.GetValueOrDefault(id, UserDisplayName.Resolve(id, null, null, null));

    private static WorkItemLinkDto MapLink(
        WorkItemLink link,
        bool incoming,
        WorkItem related,
        string? projectKey)
        => new(link.Id, link.Type, incoming, related.Id, related.Number, related.Title, projectKey);
}

public record UpdateWorkItemCommand(
    Guid WorkItemId,
    string Title,
    string? Description,
    WorkItemKind Kind,
    Guid? StageId,
    Priority Priority,
    string? ResponsibleId,
    Guid? TeamId,
    WorkItemOrigin Origin,
    string? RequesterId,
    string? RequesterName,
    string? RequesterEmail,
    DateOnly? StartDate,
    DateOnly? DueDate,
    decimal? EstimatedHours,
    decimal? RemainingHours,
    int? Points,
    string? AcceptanceCriteria,
    string ActorId,
    string ActorName) : IRequest;

public class UpdateWorkItemCommandValidator : AbstractValidator<UpdateWorkItemCommand>
{
    public UpdateWorkItemCommandValidator()
    {
        RuleFor(x => x.WorkItemId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.RequesterName).MaximumLength(200);
        RuleFor(x => x.RequesterEmail).MaximumLength(320).EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.RequesterEmail));
        RuleFor(x => x.EstimatedHours).GreaterThan(0).When(x => x.EstimatedHours.HasValue);
        RuleFor(x => x.RemainingHours).GreaterThanOrEqualTo(0).When(x => x.RemainingHours.HasValue);
        RuleFor(x => x.Points).GreaterThanOrEqualTo(0).When(x => x.Points.HasValue);
        RuleFor(x => x.AcceptanceCriteria).MaximumLength(8000);
        RuleFor(x => x)
            .Must(x => !x.StartDate.HasValue || !x.DueDate.HasValue || x.DueDate >= x.StartDate)
            .WithMessage("O prazo nao pode ser anterior a data de inicio.");
    }
}

public class UpdateWorkItemCommandHandler : IRequestHandler<UpdateWorkItemCommand>
{
    private readonly IWorkItemManagementRepository _management;
    private readonly IProjectAccessService _projectAccess;
    private readonly IPermissionService _permissions;
    private readonly IStageRepository _stages;
    private readonly ITeamRepository _teams;
    private readonly IUserDirectory _users;
    private readonly ITaskFeedRepository _feed;
    private readonly IAutomationExecutor? _automations;
    private readonly IWorkflowRepository? _workflow;
    private readonly IBoardRealtimeNotifier? _realtime;
    private readonly IPlatformNotificationPublisher? _notifications;
    private readonly IHtmlSanitizer? _htmlSanitizer;

    public UpdateWorkItemCommandHandler(
        IWorkItemManagementRepository management,
        IProjectAccessService projectAccess,
        IPermissionService permissions,
        IStageRepository stages,
        ITeamRepository teams,
        IUserDirectory users,
        ITaskFeedRepository feed,
        IAutomationExecutor? automations = null,
        IWorkflowRepository? workflow = null,
        IBoardRealtimeNotifier? realtime = null,
        IPlatformNotificationPublisher? notifications = null,
        IHtmlSanitizer? htmlSanitizer = null)
        => (_management, _projectAccess, _permissions, _stages, _teams, _users, _feed, _automations, _workflow, _realtime, _notifications, _htmlSanitizer)
            = (management, projectAccess, permissions, stages, teams, users, feed, automations, workflow, realtime, notifications, htmlSanitizer);

    public async Task Handle(UpdateWorkItemCommand request, CancellationToken ct)
    {
        var item = await _management.GetEditableAsync(request.WorkItemId, ct)
            ?? throw new NaoEncontradoException("Tarefa");
        await WorkItemAccessGuard.EnsureAsync(
            item, request.ActorId, ProjectRole.Member, PlatformPermission.Edit,
            _projectAccess, _permissions, ct);
        var actor = (await _users.GetByIdsAsync(
            [request.ActorId], includeInactive: true, cancellationToken: ct)).FirstOrDefault();
        var actorName = actor?.DisplayName
            ?? UserDisplayName.Resolve(request.ActorId, null, request.ActorName, null);

        Stage? destinationStage = null;
        DomainException.Garantir(request.StageId.HasValue, "Escolha uma coluna do quadro da tarefa.");
        if (request.StageId.HasValue)
        {
            destinationStage = await _stages.GetByIdAsync(request.StageId.Value, ct)
                ?? throw new NaoEncontradoException("Etapa");
            DomainException.Garantir(destinationStage.ProjectId == item.Board.ProjectId && destinationStage.BoardId == item.BoardId,
                "A etapa nao pertence ao projeto da tarefa.");
            if (item.StageId != request.StageId && _workflow is not null)
                await WorkflowMoveGuard.EnsureAllowedAsync(item, destinationStage, _workflow, ct);
        }

        if (request.TeamId.HasValue)
        {
            _ = await _teams.GetByIdAsync(request.TeamId.Value, ct)
                ?? throw new NaoEncontradoException("Equipe");
            var projectTeams = item.Board.Project?.Teams.Select(x => x.TeamId).ToHashSet();
            DomainException.Garantir(projectTeams is null || projectTeams.Count == 0
                || projectTeams.Contains(request.TeamId.Value),
                "A equipe nao esta associada ao projeto.");
        }

        if (!string.IsNullOrWhiteSpace(request.ResponsibleId)
            && !string.Equals(item.ResponsibleId, request.ResponsibleId, StringComparison.Ordinal))
        {
            var responsible = await _users.GetByIdsAsync(
                [request.ResponsibleId], includeInactive: false, ct);
            DomainException.Garantir(responsible.Any(x => x.Id == request.ResponsibleId),
                "O responsavel informado nao existe.");
            var role = await _projectAccess.GetRoleAsync(item.Board.ProjectId, request.ResponsibleId, ct);
            DomainException.Garantir(role is not null,
                "Conceda acesso ao projeto antes de atribuir a tarefa.");
            if (item.Assignees.All(x => x.UserId != request.ResponsibleId))
                item.Assignees.Add(new WorkItemAssignee
                {
                    WorkItemId = item.Id,
                    UserId = request.ResponsibleId,
                    AssignedAt = DateTimeOffset.UtcNow
                });
        }

        var previous = new
        {
            item.Title,
            item.Description,
            item.StageId,
            item.WorkflowStatusId,
            item.Priority,
            item.ResponsibleId,
            item.TeamId,
            item.Origin,
            item.StartDate,
            item.DueDate,
            item.EstimatedHours,
            item.RemainingHours,
            item.Points,
            item.AcceptanceCriteria,
            item.Kind,
        };
        var stageChanged = item.StageId != request.StageId;
        var now = DateTimeOffset.UtcNow;
        if (stageChanged)
        {
            var currentHistory = item.StageHistories.FirstOrDefault(x => x.LeftAt is null);
            if (currentHistory is not null) currentHistory.LeftAt = now;
            if (request.StageId.HasValue)
                item.StageHistories.Add(new StageHistory
                {
                    WorkItemId = item.Id,
                    StageId = request.StageId.Value,
                    EnteredAt = now,
                    ActorId = request.ActorId,
                    ActorName = actorName,
                });
            item.StageId = request.StageId;
            item.WorkflowStatusId = destinationStage?.WorkflowStatusId;
        }

        item.Title = request.Title.Trim();
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        item.Description = description is null ? null : (_htmlSanitizer?.Sanitize(description) ?? description);
        item.Kind = item.ParentId.HasValue && request.Kind == WorkItemKind.Task
            ? WorkItemKind.Subtask : request.Kind;
        item.Priority = request.Priority;
        item.ResponsibleId = string.IsNullOrWhiteSpace(request.ResponsibleId) ? null : request.ResponsibleId;
        item.TeamId = request.TeamId;
        item.Origin = request.Origin;
        item.RequesterId = string.IsNullOrWhiteSpace(request.RequesterId) ? null : request.RequesterId;
        item.RequesterName = string.IsNullOrWhiteSpace(request.RequesterName) ? null : request.RequesterName.Trim();
        item.RequesterEmail = string.IsNullOrWhiteSpace(request.RequesterEmail) ? null : request.RequesterEmail.Trim();
        item.StartDate = request.StartDate;
        item.DueDate = request.DueDate;
        item.EstimatedHours = request.EstimatedHours;
        item.RemainingHours = request.RemainingHours;
        item.Points = request.Points;
        item.AcceptanceCriteria = string.IsNullOrWhiteSpace(request.AcceptanceCriteria)
            ? null : request.AcceptanceCriteria.Trim();
        item.CompletedAt = destinationStage?.Category == StageCategory.Done
            ? item.CompletedAt ?? now
            : null;
        if (destinationStage?.Category == StageCategory.InProgress && item.StartDate is null)
            item.StartDate = DateOnly.FromDateTime(now.UtcDateTime);
        item.UpdatedAt = now;

        var current = new
        {
            item.Title,
            item.Description,
            item.StageId,
            item.WorkflowStatusId,
            item.Priority,
            item.ResponsibleId,
            item.TeamId,
            item.Origin,
            item.StartDate,
            item.DueDate,
            item.EstimatedHours,
            item.RemainingHours,
            item.Points,
            item.AcceptanceCriteria,
            item.Kind,
        };
        var taskEvent = TaskEvent.Registrar(
            item.Id, request.ActorId, "updated",
            JsonSerializer.Serialize(new
            {
                actorName,
                previous,
                current,
            }));
        await _management.SaveWithEventAsync(taskEvent, ct);
        if (_notifications is not null)
        {
            var link = $"/projects/{item.Board.ProjectId}/backlog?item={item.Id}";
            if (!string.IsNullOrWhiteSpace(item.ResponsibleId)
                && item.ResponsibleId != previous.ResponsibleId
                && item.ResponsibleId != request.ActorId)
                await _notifications.PublishAsync(new NotificationEnvelope(
                    item.Board.OrganizationId, item.ResponsibleId, NotificationType.TaskAssigned,
                    "Tarefa atribuída a você",
                    $"{actorName} atribuiu #{item.Number} {item.Title} a você.", link,
                    WorkItemId: item.Id, ProjectId: item.Board.ProjectId), ct);

            if (stageChanged)
            {
                var recipients = item.Assignees.Select(x => x.UserId)
                    .Concat(item.Followers.Select(x => x.UserId))
                    .Append(item.ResponsibleId ?? string.Empty)
                    .Where(x => !string.IsNullOrWhiteSpace(x) && x != request.ActorId)
                    .Distinct();
                await _notifications.PublishManyAsync(recipients.Select(userId => new NotificationEnvelope(
                    item.Board.OrganizationId, userId, NotificationType.StatusChanged,
                    "Status da tarefa alterado",
                    $"#{item.Number} {item.Title} agora está em {destinationStage?.Name ?? "Backlog"}.", link,
                    WorkItemId: item.Id, ProjectId: item.Board.ProjectId)), ct);
            }
        }
        if (stageChanged && request.StageId.HasValue && _automations is not null)
            await _automations.ExecuteStageEnteredAsync(item.Id, request.StageId.Value, request.ActorId, ct);
        if (_realtime is not null)
            await _realtime.BoardChangedAsync(item.BoardId,
                stageChanged ? "workItemMoved" : "workItemUpdated", item.Id, ct);
    }
}

public record SetWorkItemArchivedCommand(Guid WorkItemId, bool Archived, string ActorId) : IRequest;

public class SetWorkItemArchivedCommandHandler : IRequestHandler<SetWorkItemArchivedCommand>
{
    private readonly IWorkItemManagementRepository _management;
    private readonly IProjectAccessService _projectAccess;
    private readonly IPermissionService _permissions;
    private readonly ITaskFeedRepository _feed;

    public SetWorkItemArchivedCommandHandler(
        IWorkItemManagementRepository management,
        IProjectAccessService projectAccess,
        IPermissionService permissions,
        ITaskFeedRepository feed)
        => (_management, _projectAccess, _permissions, _feed)
            = (management, projectAccess, permissions, feed);

    public async Task Handle(SetWorkItemArchivedCommand request, CancellationToken ct)
    {
        var item = await _management.GetEditableAsync(request.WorkItemId, ct)
            ?? throw new NaoEncontradoException("Tarefa");
        await WorkItemAccessGuard.EnsureAsync(
            item, request.ActorId, ProjectRole.Member, PlatformPermission.Delete,
            _projectAccess, _permissions, ct);
        if (request.Archived) item.Archive(); else item.Reactivate();
        await _management.SaveAsync(ct);
        await _feed.AddEventAsync(TaskEvent.Registrar(
            item.Id, request.ActorId, request.Archived ? "archived" : "reactivated"), ct);
    }
}

public record DuplicateWorkItemCommand(Guid WorkItemId, string ActorId) : IRequest<Guid>;

public class DuplicateWorkItemCommandHandler : IRequestHandler<DuplicateWorkItemCommand, Guid>
{
    private readonly IWorkItemManagementRepository _management;
    private readonly IWorkItemRepository _workItems;
    private readonly IProjectAccessService _projectAccess;
    private readonly IPermissionService _permissions;
    private readonly ITaskFeedRepository _feed;

    public DuplicateWorkItemCommandHandler(
        IWorkItemManagementRepository management,
        IWorkItemRepository workItems,
        IProjectAccessService projectAccess,
        IPermissionService permissions,
        ITaskFeedRepository feed)
        => (_management, _workItems, _projectAccess, _permissions, _feed)
            = (management, workItems, projectAccess, permissions, feed);

    public async Task<Guid> Handle(DuplicateWorkItemCommand request, CancellationToken ct)
    {
        var source = await _management.GetDetailedAsync(request.WorkItemId, ct)
            ?? throw new NaoEncontradoException("Tarefa");
        await WorkItemAccessGuard.EnsureAsync(
            source, request.ActorId, ProjectRole.Member, PlatformPermission.Create,
            _projectAccess, _permissions, ct,
            PermissionScope.Project,
            source.Board.ProjectId);

        var now = DateTimeOffset.UtcNow;
        var copy = new WorkItem
        {
            Id = Guid.NewGuid(),
            BoardId = source.BoardId,
            StageId = source.StageId,
            WorkflowStatusId = source.WorkflowStatusId,
            ParentId = source.ParentId,
            TeamId = source.TeamId,
            Title = $"{source.Title} (copia)",
            Subtitle = source.Subtitle,
            Description = source.Description,
            Priority = source.Priority,
            Kind = source.Kind,
            Origin = WorkItemOrigin.Internal,
            ResponsibleId = source.ResponsibleId,
            StartDate = source.StartDate,
            DueDate = source.DueDate,
            AcceptanceCriteria = source.AcceptanceCriteria,
            EstimatedHours = source.EstimatedHours,
            RemainingHours = source.EstimatedHours,
            Position = source.Position + 100,
            BacklogRank = source.BacklogRank + 100,
            TaskTypeId = source.TaskTypeId,
            Points = source.Points,
            CreatedBy = request.ActorId,
            CreatedAt = now,
            UpdatedAt = now
        };
        foreach (var tag in source.WorkItemTags)
            copy.WorkItemTags.Add(new WorkItemTag { WorkItemId = copy.Id, TagId = tag.TagId });
        foreach (var assignee in source.Assignees)
            copy.Assignees.Add(new WorkItemAssignee
            {
                WorkItemId = copy.Id,
                UserId = assignee.UserId,
                AssignedAt = now
            });
        foreach (var checklist in source.ChecklistItems.OrderBy(x => x.Position))
            copy.ChecklistItems.Add(new ChecklistItem
            {
                Id = Guid.NewGuid(),
                WorkItemId = copy.Id,
                Text = checklist.Text,
                Position = checklist.Position,
                CreatedAt = now
            });
        foreach (var custom in source.CustomFieldValues)
            copy.CustomFieldValues.Add(new WorkItemCustomFieldValue
            {
                WorkItemId = copy.Id,
                FieldDefinitionId = custom.FieldDefinitionId,
                Value = custom.Value,
                UpdatedBy = request.ActorId,
                UpdatedAt = now
            });
        if (copy.StageId.HasValue)
            copy.StageHistories.Add(new StageHistory
            {
                WorkItemId = copy.Id,
                StageId = copy.StageId.Value,
                EnteredAt = now
            });

        await _workItems.AddAsync(copy, ct);
        await _feed.AddEventAsync(TaskEvent.Registrar(
            copy.Id, request.ActorId, "duplicated",
            JsonSerializer.Serialize(new { sourceWorkItemId = source.Id, source.Number })), ct);
        return copy.Id;
    }
}

public record CreateWorkItemLinkCommand(
    Guid WorkItemId,
    Guid TargetWorkItemId,
    WorkItemLinkType Type,
    string ActorId) : IRequest<Guid>;

public class CreateWorkItemLinkCommandHandler : IRequestHandler<CreateWorkItemLinkCommand, Guid>
{
    private readonly IWorkItemManagementRepository _management;
    private readonly IProjectAccessService _projectAccess;
    private readonly IPermissionService _permissions;
    private readonly ITaskFeedRepository _feed;

    public CreateWorkItemLinkCommandHandler(
        IWorkItemManagementRepository management,
        IProjectAccessService projectAccess,
        IPermissionService permissions,
        ITaskFeedRepository feed)
        => (_management, _projectAccess, _permissions, _feed)
            = (management, projectAccess, permissions, feed);

    public async Task<Guid> Handle(CreateWorkItemLinkCommand request, CancellationToken ct)
    {
        var source = await _management.GetDetailedAsync(request.WorkItemId, ct)
            ?? throw new NaoEncontradoException("Tarefa");
        var target = await _management.GetDetailedAsync(request.TargetWorkItemId, ct)
            ?? throw new NaoEncontradoException("Tarefa relacionada");
        await WorkItemAccessGuard.EnsureAsync(
            source, request.ActorId, ProjectRole.Member, PlatformPermission.Edit,
            _projectAccess, _permissions, ct);
        await WorkItemAccessGuard.EnsureAsync(
            target, request.ActorId, ProjectRole.Viewer, PlatformPermission.View,
            _projectAccess, _permissions, ct);
        DomainException.Garantir(source.Board.OrganizationId == target.Board.OrganizationId,
            "As tarefas relacionadas devem pertencer à mesma organização.");
        DomainException.Garantir(source.OutgoingLinks.All(x =>
                x.TargetWorkItemId != target.Id || x.Type != request.Type),
            "Este relacionamento ja existe.");
        var link = WorkItemLink.Create(source.Id, target.Id, request.Type, request.ActorId);
        var added = await _management.TryAddLinkAcyclicAsync(link, ct);
        DomainException.Garantir(added,
            $"Dependência cíclica detectada: {source.Board.Project?.Key}-{source.Number} não pode depender de {target.Board.Project?.Key}-{target.Number}.");
        await _feed.AddEventAsync(TaskEvent.Registrar(
            source.Id, request.ActorId, "link_added",
            JsonSerializer.Serialize(new { targetWorkItemId = target.Id, target.Number, request.Type })), ct);
        return link.Id;
    }
}

public record RemoveWorkItemLinkCommand(Guid WorkItemId, Guid LinkId, string ActorId) : IRequest;

public class RemoveWorkItemLinkCommandHandler : IRequestHandler<RemoveWorkItemLinkCommand>
{
    private readonly IWorkItemManagementRepository _management;
    private readonly IProjectAccessService _projectAccess;
    private readonly IPermissionService _permissions;
    private readonly ITaskFeedRepository _feed;

    public RemoveWorkItemLinkCommandHandler(
        IWorkItemManagementRepository management,
        IProjectAccessService projectAccess,
        IPermissionService permissions,
        ITaskFeedRepository feed)
        => (_management, _projectAccess, _permissions, _feed)
            = (management, projectAccess, permissions, feed);

    public async Task Handle(RemoveWorkItemLinkCommand request, CancellationToken ct)
    {
        var item = await _management.GetDetailedAsync(request.WorkItemId, ct)
            ?? throw new NaoEncontradoException("Tarefa");
        await WorkItemAccessGuard.EnsureAsync(
            item, request.ActorId, ProjectRole.Member, PlatformPermission.Edit,
            _projectAccess, _permissions, ct);
        var link = await _management.GetLinkAsync(request.WorkItemId, request.LinkId, ct)
            ?? throw new NaoEncontradoException("Relacionamento");
        await _management.RemoveLinkAsync(link, ct);
        await _feed.AddEventAsync(TaskEvent.Registrar(
            item.Id, request.ActorId, "link_removed",
            JsonSerializer.Serialize(new { link.Id, link.Type })), ct);
    }
}

public record SetWorkItemFollowingCommand(Guid WorkItemId, bool Following, string UserId) : IRequest;

public class SetWorkItemFollowingCommandHandler : IRequestHandler<SetWorkItemFollowingCommand>
{
    private readonly IWorkItemManagementRepository _management;
    private readonly IProjectAccessService _projectAccess;
    private readonly IPermissionService _permissions;

    public SetWorkItemFollowingCommandHandler(
        IWorkItemManagementRepository management,
        IProjectAccessService projectAccess,
        IPermissionService permissions)
        => (_management, _projectAccess, _permissions) = (management, projectAccess, permissions);

    public async Task Handle(SetWorkItemFollowingCommand request, CancellationToken ct)
    {
        var item = await _management.GetDetailedAsync(request.WorkItemId, ct)
            ?? throw new NaoEncontradoException("Tarefa");
        await WorkItemAccessGuard.EnsureAsync(
            item, request.UserId, ProjectRole.Viewer, PlatformPermission.View,
            _projectAccess, _permissions, ct);
        if (request.Following)
            await _management.AddFollowerAsync(item.Id, request.UserId, ct);
        else
            await _management.RemoveFollowerAsync(item.Id, request.UserId, ct);
    }
}

public record SetWorkItemCustomFieldsCommand(
    Guid WorkItemId,
    IReadOnlyDictionary<Guid, string?> Values,
    string ActorId) : IRequest;

public class SetWorkItemCustomFieldsCommandHandler : IRequestHandler<SetWorkItemCustomFieldsCommand>
{
    private readonly IWorkItemManagementRepository _management;
    private readonly IProjectAccessService _projectAccess;
    private readonly IPermissionService _permissions;
    private readonly ITaskFeedRepository _feed;

    public SetWorkItemCustomFieldsCommandHandler(
        IWorkItemManagementRepository management,
        IProjectAccessService projectAccess,
        IPermissionService permissions,
        ITaskFeedRepository feed)
        => (_management, _projectAccess, _permissions, _feed)
            = (management, projectAccess, permissions, feed);

    public async Task Handle(SetWorkItemCustomFieldsCommand request, CancellationToken ct)
    {
        var item = await _management.GetEditableAsync(request.WorkItemId, ct)
            ?? throw new NaoEncontradoException("Tarefa");
        await WorkItemAccessGuard.EnsureAsync(
            item, request.ActorId, ProjectRole.Member, PlatformPermission.Edit,
            _projectAccess, _permissions, ct);
        var definitions = item.Board.Project?.CustomFields.Where(x => x.IsActive).ToDictionary(x => x.Id)
            ?? new Dictionary<Guid, ProjectCustomFieldDefinition>();
        DomainException.Garantir(request.Values.Keys.All(definitions.ContainsKey),
            "Um ou mais campos personalizados nao pertencem ao projeto.");

        var merged = item.CustomFieldValues.ToDictionary(x => x.FieldDefinitionId, x => x.Value);
        foreach (var pair in request.Values) merged[pair.Key] = Normalize(pair.Value);
        foreach (var definition in definitions.Values)
        {
            merged.TryGetValue(definition.Id, out var value);
            DomainException.Garantir(!definition.IsRequired || !string.IsNullOrWhiteSpace(value),
                $"O campo {definition.Name} e obrigatorio.");
            CustomFieldValueRules.Validate(definition, value, item.Board.Project!);
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var pair in request.Values)
        {
            var current = item.CustomFieldValues.FirstOrDefault(x => x.FieldDefinitionId == pair.Key);
            var value = Normalize(pair.Value);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (current is not null) item.CustomFieldValues.Remove(current);
                continue;
            }
            if (current is null)
                item.CustomFieldValues.Add(new WorkItemCustomFieldValue
                {
                    WorkItemId = item.Id,
                    FieldDefinitionId = pair.Key,
                    Value = value,
                    UpdatedBy = request.ActorId,
                    UpdatedAt = now
                });
            else
            {
                current.Value = value;
                current.UpdatedBy = request.ActorId;
                current.UpdatedAt = now;
            }
        }
        item.UpdatedAt = now;
        await _management.SaveAsync(ct);
        await _feed.AddEventAsync(TaskEvent.Registrar(
            item.Id, request.ActorId, "custom_fields_updated",
            JsonSerializer.Serialize(new { fieldIds = request.Values.Keys })), ct);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

}

internal static class WorkItemAccessGuard
{
    public static async Task EnsureAsync(
        WorkItem item,
        string actorId,
        ProjectRole minimumRole,
        PlatformPermission permission,
        IProjectAccessService projectAccess,
        IPermissionService permissions,
        CancellationToken ct,
        PermissionScope scope = PermissionScope.WorkItem,
        Guid? scopeId = null)
    {
        await projectAccess.EnsureAtLeastAsync(item.Board.ProjectId, actorId, minimumRole, ct);
        await permissions.EnsureAsync(actorId, permission, scope, scopeId ?? item.Id, ct);
    }
}
