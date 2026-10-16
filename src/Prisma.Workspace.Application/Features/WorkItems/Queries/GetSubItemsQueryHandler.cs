using Prisma.Workspace.Application.Features.WorkItems.Dtos;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;
using MediatR;

namespace Prisma.Workspace.Application.Features.WorkItems.Queries;

/// <summary>
/// Handler da query para listar subtarefas de um WorkItem pai.
/// </summary>
public class GetSubItemsQueryHandler : IRequestHandler<GetSubItemsQuery, IReadOnlyList<WorkItemDto>>
{
    private readonly IWorkItemRepository _workItemRepository;
    private readonly IWorkItemAccessService _access;
    private readonly IHoursReadAccessService _hours;

    public GetSubItemsQueryHandler(
        IWorkItemRepository workItemRepository,
        IWorkItemAccessService access, IHoursReadAccessService hours)
    {
        _workItemRepository = workItemRepository;
        _access = access;
        _hours = hours;
    }

    public async Task<IReadOnlyList<WorkItemDto>> Handle(GetSubItemsQuery request, CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(request.ParentId, request.CurrentUserId ?? string.Empty,
            PlatformPermission.View, ProjectRole.Viewer, cancellationToken);
        var items = await _workItemRepository.GetSubItemsAsync(request.ParentId, cancellationToken);

        var scope = await _hours.GetScopeAsync(request.CurrentUserId ?? string.Empty,false,cancellationToken);
        var now = DateTimeOffset.UtcNow;
        return items.Select(w => new WorkItemDto
        {
            Id = w.Id,
            Number = w.Number,
            BoardId = w.BoardId,
            TeamId = w.TeamId,
            StageId = w.StageId,
            ParentId = w.ParentId,
            SprintId = w.SprintId,
            Kind = w.Kind,
            Origin = w.Origin,
            Title = w.Title,
            Subtitle = w.Subtitle,
            Description = w.Description,
            Priority = w.Priority,
            ResponsibleId = w.ResponsibleId,
            RequesterId = w.RequesterId,
            RequesterName = w.RequesterName,
            RequesterEmail = w.RequesterEmail,
            EstimatedHours = w.EstimatedHours,
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
            TimeCalculatedAt = now,
            TotalTimeSeconds = w.TimeEntries.Where(entry=>scope.CanRead(w,entry)).Sum(entry =>
            {
                var ended = entry.EndedAt ?? now;
                return Math.Max(0, (int)(ended - entry.StartedAt).TotalSeconds);
            }),
            UserTimeSeconds = string.IsNullOrEmpty(request.CurrentUserId) ? 0 : w.TimeEntries
                .Where(entry => entry.UserId == request.CurrentUserId)
                .Sum(entry =>
                {
                    var ended = entry.EndedAt ?? now;
                    return Math.Max(0, (int)(ended - entry.StartedAt).TotalSeconds);
                })
        }).ToList().AsReadOnly();
    }
}
