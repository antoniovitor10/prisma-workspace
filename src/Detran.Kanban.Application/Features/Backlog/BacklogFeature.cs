using Detran.Kanban.Application.Common.Exceptions;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Domain.Exceptions;
using Detran.Kanban.Domain.Services;
using FluentValidation;
using MediatR;

namespace Detran.Kanban.Application.Features.Backlog;

public record BacklogLinkDto(Guid Id, WorkItemLinkType Type, bool IsIncoming,
    Guid RelatedWorkItemId, long RelatedNumber, string RelatedTitle, bool IsOpen);

public record BacklogItemDto(Guid Id, Guid BoardId, string BoardName, Guid? StageId, string? StageName,
    Guid? ParentId, Guid? SprintId, WorkItemKind Kind, string Title, string? Description, Priority Priority, int? Points,
    decimal? EstimatedHours, decimal? RemainingHours, decimal Rank, double Position, DateOnly? DueDate,
    DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, IReadOnlyList<string> AssigneeIds, string Version,
    long Number, Guid? TeamId, string? ResponsibleId, WorkItemOrigin Origin,
    string? RequesterId, string? RequesterName, string? RequesterEmail,
    DateOnly? StartDate, string? AcceptanceCriteria, IReadOnlyList<BacklogLinkDto> Links, bool IsBlocked);

public record GetProjectBacklogQuery(Guid ProjectId, string UserId) : IRequest<IReadOnlyList<BacklogItemDto>>;
public class GetProjectBacklogQueryHandler : IRequestHandler<GetProjectBacklogQuery, IReadOnlyList<BacklogItemDto>>
{
    private readonly IBacklogRepository _backlog;
    private readonly IProjectAccessService _access;
    public GetProjectBacklogQueryHandler(IBacklogRepository backlog, IProjectAccessService access)
        => (_backlog, _access) = (backlog, access);
    public async Task<IReadOnlyList<BacklogItemDto>> Handle(GetProjectBacklogQuery request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.UserId, ProjectRole.Viewer, ct);
        return (await _backlog.GetProjectBacklogAsync(request.ProjectId, ct)).Select(Map).ToList();
    }

    private static BacklogItemDto Map(Detran.Kanban.Domain.Entities.WorkItem item)
    {
        var links = item.OutgoingLinks.Select(link => new BacklogLinkDto(
                link.Id, link.Type, false, link.TargetWorkItemId,
                link.TargetWorkItem.Number, link.TargetWorkItem.Title,
                link.TargetWorkItem.CompletedAt is null))
            .Concat(item.IncomingLinks.Select(link => new BacklogLinkDto(
                link.Id, link.Type, true, link.SourceWorkItemId,
                link.SourceWorkItem.Number, link.SourceWorkItem.Title,
                link.SourceWorkItem.CompletedAt is null)))
            .OrderBy(link => link.RelatedNumber)
            .ToList();
        var blocked = links.Any(link => link.IsOpen &&
            (!link.IsIncoming && link.Type == WorkItemLinkType.DependsOn
             || link.IsIncoming && link.Type == WorkItemLinkType.Blocks));

        return new BacklogItemDto(
            item.Id, item.BoardId, item.Board.Name, item.StageId, item.Stage?.Name,
            item.ParentId, item.SprintId, item.Kind, item.Title, item.Description,
            item.Priority, item.Points, item.EstimatedHours, item.RemainingHours,
            item.BacklogRank, item.Position, item.DueDate, item.CreatedAt, item.CompletedAt,
            item.Assignees.Select(assignee => assignee.UserId).ToList(),
            item.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(item.RowVersion),
            item.Number, item.TeamId, item.ResponsibleId, item.Origin,
            item.RequesterId, item.RequesterName, item.RequesterEmail,
            item.StartDate, item.AcceptanceCriteria, links, blocked);
    }
}

public record UpdateBacklogItemCommand(
    Guid ProjectId,
    Guid WorkItemId,
    string Title,
    Priority Priority,
    int? Points,
    Guid? EpicId,
    bool UpdateEpic,
    string ActorId) : IRequest;

public sealed class UpdateBacklogItemCommandValidator : AbstractValidator<UpdateBacklogItemCommand>
{
    public UpdateBacklogItemCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.WorkItemId).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(500);
        RuleFor(command => command.Priority).IsInEnum();
        RuleFor(command => command.Points).GreaterThanOrEqualTo(0).When(command => command.Points.HasValue);
    }
}

public sealed class UpdateBacklogItemCommandHandler : IRequestHandler<UpdateBacklogItemCommand>
{
    private readonly IBacklogRepository _backlog;
    private readonly IProjectAccessService _access;

    public UpdateBacklogItemCommandHandler(IBacklogRepository backlog, IProjectAccessService access)
        => (_backlog, _access) = (backlog, access);

    public async Task Handle(UpdateBacklogItemCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.ProductOwner, ct);
        if (request.UpdateEpic)
            DomainException.Garantir(request.EpicId != request.WorkItemId,
                "Um item nao pode ser pai dele mesmo.");

        var requestedIds = request.UpdateEpic && request.EpicId.HasValue
            ? new[] { request.WorkItemId, request.EpicId.Value }
            : new[] { request.WorkItemId };
        var items = await _backlog.GetTrackedByIdsAsync(request.ProjectId, requestedIds, ct);
        var item = items.SingleOrDefault(current => current.Id == request.WorkItemId)
            ?? throw new NaoEncontradoException("Item do backlog");

        if (request.UpdateEpic && request.EpicId.HasValue)
        {
            var epic = items.SingleOrDefault(current => current.Id == request.EpicId.Value)
                ?? throw new NaoEncontradoException("Epico");
            DomainException.Garantir(item.Kind != WorkItemKind.Epic,
                "Um epico nao pode ser relacionado como filho de outro epico.");
            DomainException.Garantir(epic.Kind == WorkItemKind.Epic,
                "O item relacionado deve ser um epico do projeto.");
            DomainException.Garantir(WorkItemHierarchyRules.IsAllowed(epic.Kind, item.Kind),
                $"A relação {epic.Kind} → {item.Kind} não pertence à hierarquia de trabalho permitida.");
        }

        item.Title = request.Title.Trim();
        item.Priority = request.Priority;
        item.Points = request.Points;
        if (request.UpdateEpic) item.ParentId = request.EpicId;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await _backlog.SaveAsync(ct);
    }
}

public record PlanSprintCommand(Guid ProjectId, Guid? SprintId, IReadOnlyList<Guid> WorkItemIds, string ActorId) : IRequest;
public class PlanSprintCommandHandler : IRequestHandler<PlanSprintCommand>
{
    private readonly IBacklogRepository _backlog;
    private readonly ISprintRepository _sprints;
    private readonly IProjectAccessService _access;
    public PlanSprintCommandHandler(IBacklogRepository backlog, ISprintRepository sprints, IProjectAccessService access)
        => (_backlog, _sprints, _access) = (backlog, sprints, access);
    public async Task Handle(PlanSprintCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.ScrumMaster, ct);
        var loadedSprints = new Dictionary<Guid, Sprint>();
        if (request.SprintId.HasValue)
        {
            var sprint = await _sprints.GetByIdAsync(request.SprintId.Value, ct) ?? throw new NaoEncontradoException("Sprint");
            DomainException.Garantir(sprint.ProjectId == request.ProjectId, "Sprint não pertence ao projeto.");
            DomainException.Garantir(!sprint.IsTerminal, "Sprint concluída ou cancelada não aceita alteração de escopo.");
            loadedSprints[sprint.Id] = sprint;
        }
        var items = await _backlog.GetTrackedByIdsAsync(request.ProjectId, request.WorkItemIds, ct);
        DomainException.Garantir(items.Count == request.WorkItemIds.Distinct().Count(), "Um ou mais itens não pertencem ao projeto.");

        foreach (var sourceSprintId in items
                     .Select(item => item.SprintId)
                     .Where(id => id.HasValue && id != request.SprintId)
                     .Select(id => id!.Value)
                     .Distinct())
        {
            if (!loadedSprints.TryGetValue(sourceSprintId, out var sourceSprint))
            {
                sourceSprint = await _sprints.GetByIdAsync(sourceSprintId, ct)
                    ?? throw new NaoEncontradoException("Sprint de origem");
                loadedSprints[sourceSprintId] = sourceSprint;
            }

            DomainException.Garantir(sourceSprint.ProjectId == request.ProjectId,
                "Sprint de origem não pertence ao projeto.");
            DomainException.Garantir(!sourceSprint.IsTerminal,
                "Itens de sprint concluída ou cancelada não podem ter o planejamento alterado.");
        }

        foreach (var item in items) item.SprintId = request.SprintId;
        await _backlog.SaveAsync(ct);
    }
}

public record ReorderBacklogCommand(Guid ProjectId, IReadOnlyList<Guid> OrderedIds, string ActorId) : IRequest;
public class ReorderBacklogCommandHandler : IRequestHandler<ReorderBacklogCommand>
{
    private readonly IBacklogRepository _backlog;
    private readonly IProjectAccessService _access;
    public ReorderBacklogCommandHandler(IBacklogRepository backlog, IProjectAccessService access)
        => (_backlog, _access) = (backlog, access);
    public async Task Handle(ReorderBacklogCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.ProductOwner, ct);
        var ids = request.OrderedIds.Distinct().ToList();
        var items = await _backlog.GetTrackedByIdsAsync(request.ProjectId, ids, ct);
        DomainException.Garantir(items.Count == ids.Count, "Um ou mais itens não pertencem ao projeto.");
        var byId = items.ToDictionary(x => x.Id);
        for (var i = 0; i < ids.Count; i++) byId[ids[i]].BacklogRank = (i + 1) * 1000m;
        await _backlog.SaveAsync(ct);
    }
}
