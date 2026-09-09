using Prisma.Workspace.Application.Features.WorkItems.Dtos;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;
using MediatR;

namespace Prisma.Workspace.Application.Features.WorkItems.Queries;

/// <summary>
/// Handler da query para listar os cards de um Board.
/// </summary>
public class GetWorkItemsByBoardIdQueryHandler : IRequestHandler<GetWorkItemsByBoardIdQuery, IReadOnlyList<WorkItemDto>>
{
    private readonly IWorkItemRepository _workItemRepository;
    private readonly IBoardAccessService _access;

    public GetWorkItemsByBoardIdQueryHandler(
        IWorkItemRepository workItemRepository,
        IBoardAccessService access)
    {
        _workItemRepository = workItemRepository;
        _access = access;
    }

    public async Task<IReadOnlyList<WorkItemDto>> Handle(
        GetWorkItemsByBoardIdQuery request,
        CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(request.BoardId, request.CurrentUserId ?? string.Empty,
            PlatformPermission.View, ProjectRole.Viewer, cancellationToken);
        var items = await _workItemRepository.GetByBoardIdAsync(request.BoardId, cancellationToken);

        return items.Select(w =>
        {
            // Etapa e posição vêm do próprio item: fonte única, sem projeção por quadro.
            return new WorkItemDto
            {
            Id = w.Id,
            Number = w.Number,
            BoardId = w.BoardId,
            TeamId = w.TeamId,
            StageId = w.StageId,
            WorkflowStatusId = w.WorkflowStatusId,
            StatusName = w.WorkflowStatus?.Name,
            StatusColor = w.WorkflowStatus?.Color,
            ParentId = w.ParentId,
            SprintId = w.SprintId,
            Kind = w.Kind,
            Origin = w.Origin,
            Title = w.Title,
            Subtitle = w.Subtitle,
            Description = w.Description,
            Priority = w.Priority,
            ResponsibleId = w.ResponsibleId,
            TeamName = w.Team?.Name,
            RequesterId = w.RequesterId,
            RequesterName = w.RequesterName,
            RequesterEmail = w.RequesterEmail,
            EstimatedHours = w.EstimatedHours,
            RemainingHours = w.RemainingHours,
            DueDate = w.DueDate,
            StartDate = w.StartDate,
            AcceptanceCriteria = w.AcceptanceCriteria,
            Position = w.Position,
            BacklogRank = w.BacklogRank,
            CompletedAt = w.CompletedAt,
            IsArchived = w.IsArchived,
            Version = w.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(w.RowVersion),
            CreatedBy = w.CreatedBy,
            CreatedAt = w.CreatedAt,
            UpdatedAt = w.UpdatedAt,
            Assignees = w.Assignees.Select(a => new WorkItemAssigneeDto
            {
                UserId = a.UserId,
                AssignedAt = a.AssignedAt
            }).ToList().AsReadOnly(),
            SubItemsCount = w.SubItems.Count,
            AttachmentsCount = w.Attachments.Count,
            TotalTimeSeconds = w.TimeEntries.Sum(entry =>
            {
                var ended = entry.EndedAt ?? DateTimeOffset.UtcNow;
                return Math.Max(0, (int)(ended - entry.StartedAt).TotalSeconds);
            }),
            UserTimeSeconds = string.IsNullOrEmpty(request.CurrentUserId) ? 0 : w.TimeEntries
                .Where(entry => entry.UserId == request.CurrentUserId)
                .Sum(entry =>
                {
                    var ended = entry.EndedAt ?? DateTimeOffset.UtcNow;
                    return Math.Max(0, (int)(ended - entry.StartedAt).TotalSeconds);
                }),
            TaskTypeId = w.TaskTypeId,
            TaskTypeName = w.TaskType?.Name,
            TaskTypeColor = w.TaskType?.Color,
            Points = w.Points,
            Tags = w.WorkItemTags
                .Where(wt => wt.Tag != null)
                .Select(wt => new TagDto { Id = wt.Tag.Id, Name = wt.Tag.Name, Color = wt.Tag.Color })
                .ToList()
                .AsReadOnly(),
            ChecklistTotal = w.ChecklistItems.Count,
            ChecklistDone = w.ChecklistItems.Count(c => c.Done),
            IsBlocked = w.OutgoingLinks.Any(x => x.Type == WorkItemLinkType.DependsOn
                    && x.TargetWorkItem.CompletedAt == null)
                || w.IncomingLinks.Any(x => x.Type == WorkItemLinkType.Blocks
                    && x.SourceWorkItem.CompletedAt == null),
            CustomFields = w.CustomFieldValues
                .Select(x => new WorkItemCustomValueDto(x.FieldDefinitionId, x.Value)).ToList(),
            BoardIds = new List<Guid> { w.BoardId }.AsReadOnly()
            };
        }).ToList().AsReadOnly();
    }
}
