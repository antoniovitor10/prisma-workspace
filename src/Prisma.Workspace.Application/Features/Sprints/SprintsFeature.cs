using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Prisma.Workspace.Application.Features.Sprints;

public record SprintCapacityDto(string UserId, decimal AvailableHours, decimal DaysOffHours);
public record SprintItemSnapshotDto(
    Guid WorkItemId, long WorkItemNumber, string Title, WorkItemKind Kind,
    int? Points, decimal? EstimatedHours, bool WasCompleted,
    DateTimeOffset? WorkItemCompletedAt, SprintItemOutcome Outcome, Guid? DestinationSprintId);
public record SprintDto(Guid Id, Guid ProjectId, Guid? TeamId, string? TeamName, string Name, string? Goal,
    DateOnly StartDate, DateOnly EndDate, SprintStatus Status, int ItemCount, int CompletedItemCount,
    decimal StoryPoints, decimal CompletedStoryPoints, decimal PlannedHours, decimal RemainingHours,
    decimal CapacityHours, int ProgressPercentage, DateTimeOffset? CompletedAt, DateTimeOffset? CancelledAt,
    IReadOnlyList<SprintCapacityDto> Capacities,
    IReadOnlyList<SprintItemSnapshotDto> ItemSnapshots);

public record GetProjectSprintsQuery(Guid ProjectId, string UserId) : IRequest<IReadOnlyList<SprintDto>>;
public class GetProjectSprintsQueryHandler : IRequestHandler<GetProjectSprintsQuery, IReadOnlyList<SprintDto>>
{
    private readonly ISprintRepository _sprints;
    private readonly IProjectAccessService _access;
    public GetProjectSprintsQueryHandler(ISprintRepository sprints, IProjectAccessService access)
        => (_sprints, _access) = (sprints, access);
    public async Task<IReadOnlyList<SprintDto>> Handle(GetProjectSprintsQuery request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.UserId, ProjectRole.Viewer, ct);
        return (await _sprints.GetByProjectAsync(request.ProjectId, ct)).Select(Map).ToList();
    }
    /// <summary>
    /// O DTO devolve o estado calculado pelas datas (D84), nunca a coluna persistida:
    /// a API é a única fonte funcional do estado da sprint.
    /// </summary>
    internal static SprintDto Map(Sprint sprint)
    {
        var snapshots = sprint.ItemSnapshots;
        var hasSnapshot = snapshots.Count > 0;
        var itemCount = hasSnapshot ? snapshots.Count : sprint.WorkItems.Count;
        var completedItemCount = hasSnapshot
            ? snapshots.Count(x => x.WasCompleted)
            : sprint.WorkItems.Count(x => x.CompletedAt.HasValue);
        var storyPoints = hasSnapshot
            ? snapshots.Where(x => x.Kind is WorkItemKind.UserStory or WorkItemKind.Bug)
                .Sum(x => (decimal)(x.Points ?? 0))
            : sprint.WorkItems.Where(x => x.Kind is WorkItemKind.UserStory or WorkItemKind.Bug)
                .Sum(x => (decimal)(x.Points ?? 0));
        var completedStoryPoints = hasSnapshot
            ? snapshots.Where(x => x.WasCompleted && x.Kind is (WorkItemKind.UserStory or WorkItemKind.Bug))
                .Sum(x => (decimal)(x.Points ?? 0))
            : sprint.WorkItems.Where(x => x.CompletedAt.HasValue && x.Kind is (WorkItemKind.UserStory or WorkItemKind.Bug))
                .Sum(x => (decimal)(x.Points ?? 0));
        var plannedHours = hasSnapshot
            ? snapshots.Sum(x => x.EstimatedHours ?? 0)
            : sprint.WorkItems.Sum(x => x.EstimatedHours ?? 0);
        var hoje = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var statusFuncional = sprint.StatusEm(hoje);
        var remainingHours = sprint.EstaEncerradaEm(hoje)
            ? 0
            : sprint.WorkItems.Where(x => !x.CompletedAt.HasValue).Sum(x => x.RemainingHours ?? 0);
        var capacityHours = sprint.Capacities.Sum(x => Math.Max(0, x.AvailableHours - x.DaysOffHours));
        var progress = itemCount == 0 ? 0 : (int)Math.Round(completedItemCount * 100m / itemCount);

        return new SprintDto(sprint.Id, sprint.ProjectId, sprint.TeamId, sprint.Team?.Name, sprint.Name, sprint.Goal,
            sprint.StartDate, sprint.EndDate, statusFuncional, itemCount, completedItemCount,
            storyPoints, completedStoryPoints, plannedHours, remainingHours, capacityHours, progress,
            sprint.CompletedAt, sprint.CancelledAt,
            sprint.Capacities.Select(x => new SprintCapacityDto(x.UserId, x.AvailableHours, x.DaysOffHours)).ToList(),
            snapshots.OrderBy(x => x.WorkItemNumber).Select(x => new SprintItemSnapshotDto(
                x.WorkItemId, x.WorkItemNumber, x.Title, x.Kind, x.Points, x.EstimatedHours,
                x.WasCompleted, x.WorkItemCompletedAt, x.Outcome, x.DestinationSprintId)).ToList());
    }
}

public record CreateSprintCommand(Guid ProjectId, Guid? TeamId, string Name, string? Goal,
    DateOnly StartDate, DateOnly EndDate, string ActorId) : IRequest<Guid>;
public class CreateSprintCommandValidator : AbstractValidator<CreateSprintCommand>
{
    public CreateSprintCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Goal).MaximumLength(1000);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
    }
}
public class CreateSprintCommandHandler : IRequestHandler<CreateSprintCommand, Guid>
{
    private readonly ISprintRepository _sprints;
    private readonly IProjectRepository _projects;
    private readonly IProjectAccessService _access;
    private readonly IPermissionService? _permissions;
    public CreateSprintCommandHandler(ISprintRepository sprints, IProjectRepository projects,
        IProjectAccessService access, IPermissionService? permissions = null)
        => (_sprints, _projects, _access, _permissions) = (sprints, projects, access, permissions);
    public async Task<Guid> Handle(CreateSprintCommand request, CancellationToken ct)
    {
        await SprintAccessGuard.EnsureCanManageAsync(request.ProjectId, request.ActorId, _access, _permissions, ct);
        var project = await _projects.GetByIdAsync(request.ProjectId, ct) ?? throw new NaoEncontradoException("Projeto");
        DomainException.Garantir(!request.TeamId.HasValue || project.Teams.Any(x => x.TeamId == request.TeamId),
            "O time não pertence ao projeto.");
        var sprint = Sprint.Criar(request.ProjectId, request.TeamId, request.Name, request.StartDate, request.EndDate, request.Goal);
        await _sprints.AddAsync(sprint, ct);
        return sprint.Id;
    }
}

public record UpdateSprintCommand(Guid SprintId, string Name, string? Goal,
    DateOnly StartDate, DateOnly EndDate, string ActorId) : IRequest;
public class UpdateSprintCommandValidator : AbstractValidator<UpdateSprintCommand>
{
    public UpdateSprintCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Goal).MaximumLength(1000);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
    }
}
public class UpdateSprintCommandHandler : IRequestHandler<UpdateSprintCommand>
{
    private readonly ISprintRepository _sprints;
    private readonly IProjectAccessService _access;
    private readonly IPermissionService? _permissions;
    public UpdateSprintCommandHandler(ISprintRepository sprints, IProjectAccessService access,
        IPermissionService? permissions = null)
        => (_sprints, _access, _permissions) = (sprints, access, permissions);

    public async Task Handle(UpdateSprintCommand request, CancellationToken ct)
    {
        var sprint = await _sprints.GetByIdAsync(request.SprintId, ct)
            ?? throw new NaoEncontradoException("Sprint");
        await SprintAccessGuard.EnsureCanManageAsync(sprint.ProjectId, request.ActorId, _access, _permissions, ct);
        sprint.Update(request.Name, request.Goal, request.StartDate, request.EndDate);
        await _sprints.SaveAsync(ct);
    }
}

/// <summary>
/// Encerra ou cancela a sprint. Não inicia: o estado Ativa é derivado das datas (D84).
/// </summary>
public record ChangeSprintStatusCommand(
    Guid SprintId,
    SprintStatus Status,
    string ActorId,
    SprintIncompleteItemsAction IncompleteItemsAction = SprintIncompleteItemsAction.ReturnToBacklog,
    Guid? TargetSprintId = null) : IRequest;
public class ChangeSprintStatusCommandHandler : IRequestHandler<ChangeSprintStatusCommand>
{
    private readonly ISprintRepository _sprints;
    private readonly IProjectAccessService _access;
    private readonly IPlatformNotificationPublisher? _notifications;
    private readonly IPermissionService? _permissions;
    public ChangeSprintStatusCommandHandler(
        ISprintRepository sprints,
        IProjectAccessService access,
        IPlatformNotificationPublisher? notifications = null,
        IPermissionService? permissions = null)
        => (_sprints, _access, _notifications, _permissions) = (sprints, access, notifications, permissions);
    public async Task Handle(ChangeSprintStatusCommand request, CancellationToken ct)
    {
        var sprint = await _sprints.GetByIdAsync(request.SprintId, ct) ?? throw new NaoEncontradoException("Sprint");
        await SprintAccessGuard.EnsureCanManageAsync(sprint.ProjectId, request.ActorId, _access, _permissions, ct);
        var hoje = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        {
            Sprint? targetSprint = null;
            if (request.IncompleteItemsAction == SprintIncompleteItemsAction.MoveToSprint)
            {
                DomainException.Garantir(request.TargetSprintId.HasValue && request.TargetSprintId != sprint.Id,
                    "Selecione outra sprint para receber os itens não concluídos.");
                targetSprint = await _sprints.GetByIdAsync(request.TargetSprintId!.Value, ct)
                    ?? throw new NaoEncontradoException("Sprint de destino");
                DomainException.Garantir(targetSprint.ProjectId == sprint.ProjectId,
                    "A sprint de destino não pertence ao projeto.");
                DomainException.Garantir(!targetSprint.EstaEncerradaEm(hoje),
                    "A sprint de destino não pode estar encerrada ou cancelada.");
            }

            sprint.CaptureHistory(sprint.WorkItems, request.IncompleteItemsAction, targetSprint?.Id);
            foreach (var item in sprint.WorkItems.Where(x => !x.CompletedAt.HasValue).ToList())
                item.SprintId = targetSprint?.Id;
        }

        sprint.Encerrar(request.Status, hoje);
        await _sprints.SaveAsync(ct);
        if (_notifications is not null)
        {
            var recipients = (sprint.Team?.Members.Select(x => x.UserId) ?? Enumerable.Empty<string>())
                .Append(sprint.Project.OwnerId)
                .Where(x => !string.IsNullOrWhiteSpace(x) && x != request.ActorId).Distinct();
            await _notifications.PublishManyAsync(recipients.Select(userId => new NotificationEnvelope(
                sprint.Project.OrganizationId, userId, NotificationType.SprintCompleted,
                "Sprint encerrada",
                $"{sprint.Name} foi encerrada.",
                $"/projects/{sprint.ProjectId}/sprints?sprint={sprint.Id}",
                ProjectId: sprint.ProjectId, SprintId: sprint.Id)), ct);
        }
    }
}

/// <summary>
/// Exclui a sprint. As tarefas vinculadas apenas perdem o <c>SprintId</c> e voltam ao
/// Product Backlog: nunca são excluídas, arquivadas nem movidas de quadro, coluna ou
/// posição (SPEC-S-003 v3, itens 17 a 19).
/// </summary>
public record DeleteSprintCommand(Guid SprintId, string ActorId) : IRequest;

public class DeleteSprintCommandHandler : IRequestHandler<DeleteSprintCommand>
{
    private readonly ISprintRepository _sprints;
    private readonly IProjectAccessService _access;
    private readonly IPermissionService? _permissions;

    public DeleteSprintCommandHandler(ISprintRepository sprints, IProjectAccessService access,
        IPermissionService? permissions = null)
        => (_sprints, _access, _permissions) = (sprints, access, permissions);

    public async Task Handle(DeleteSprintCommand request, CancellationToken ct)
    {
        var sprint = await _sprints.GetByIdAsync(request.SprintId, ct)
            ?? throw new NaoEncontradoException("Sprint");
        await SprintAccessGuard.EnsureCanManageAsync(sprint.ProjectId, request.ActorId, _access, _permissions, ct);

        // Excluir é permitido em qualquer estado, inclusive encerrada: o que a spec proíbe
        // é a exclusão tocar na tarefa, não a exclusão em si.
        await _sprints.RemoveWithUnlinkAsync(sprint, ct);
    }
}

public record SetSprintCapacityCommand(Guid SprintId, string UserId, decimal AvailableHours,
    decimal DaysOffHours, string ActorId) : IRequest;
public class SetSprintCapacityCommandValidator : AbstractValidator<SetSprintCapacityCommand>
{
    public SetSprintCapacityCommandValidator()
    {
        RuleFor(x => x.AvailableHours).GreaterThanOrEqualTo(0).LessThanOrEqualTo(500);
        RuleFor(x => x.DaysOffHours).GreaterThanOrEqualTo(0).LessThanOrEqualTo(x => x.AvailableHours);
    }
}
public class SetSprintCapacityCommandHandler : IRequestHandler<SetSprintCapacityCommand>
{
    private readonly ISprintRepository _sprints;
    private readonly IProjectAccessService _access;
    private readonly IPermissionService? _permissions;
    public SetSprintCapacityCommandHandler(ISprintRepository sprints, IProjectAccessService access,
        IPermissionService? permissions = null)
        => (_sprints, _access, _permissions) = (sprints, access, permissions);
    public async Task Handle(SetSprintCapacityCommand request, CancellationToken ct)
    {
        var sprint = await _sprints.GetByIdAsync(request.SprintId, ct) ?? throw new NaoEncontradoException("Sprint");
        await SprintAccessGuard.EnsureCanManageAsync(sprint.ProjectId, request.ActorId, _access, _permissions, ct);
        DomainException.Garantir(
            !sprint.EstaEncerradaEm(DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime)),
            "Não é possível alterar capacidade de sprint encerrada ou cancelada.");
        var capacity = sprint.Capacities.FirstOrDefault(x => x.UserId == request.UserId);
        if (capacity is null)
            sprint.Capacities.Add(new SprintCapacity { SprintId = sprint.Id, UserId = request.UserId,
                AvailableHours = request.AvailableHours, DaysOffHours = request.DaysOffHours });
        else
        {
            capacity.AvailableHours = request.AvailableHours;
            capacity.DaysOffHours = request.DaysOffHours;
        }
        await _sprints.SaveAsync(ct);
    }
}

/// <summary>
/// Autorização de gestão de sprint. O acesso ao projeto continua sendo pré-requisito, mas
/// quem pode criar, editar, encerrar ou excluir é definido pela permissão configurável
/// <c>ManageSprint</c>, e não mais pelo papel fixo <c>ScrumMaster</c> (SPEC-S-003 v3,
/// item 16). A API é a autoridade final, mesmo quando a interface oculta a ação.
/// </summary>
internal static class SprintAccessGuard
{
    public static async Task EnsureCanManageAsync(
        Guid projectId,
        string actorId,
        IProjectAccessService access,
        IPermissionService? permissions,
        CancellationToken ct)
    {
        await access.EnsureAtLeastAsync(projectId, actorId, ProjectRole.Member, ct);
        if (permissions is not null)
            await permissions.EnsureAsync(
                actorId, PlatformPermission.ManageSprint, PermissionScope.Project, projectId, ct);
    }
}
