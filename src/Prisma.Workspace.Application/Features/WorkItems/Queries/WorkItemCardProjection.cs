using Prisma.Workspace.Application.Features.WorkItems.Dtos;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Features.WorkItems.Queries;

/// <summary>
/// Projeção de <see cref="WorkItem"/> para <see cref="WorkItemDto"/> usada pelo quadro.
///
/// Fonte única de propósito: o Kanban do projeto e o do quadro precisam devolver
/// exatamente o mesmo cartão. Duplicar este mapeamento faria os dois divergirem no
/// primeiro campo que alguém acrescentasse em um só.
/// </summary>
public static class WorkItemCardProjection
{
    /// <param name="currentUserId">
    /// Quem está olhando: <c>UserTimeSeconds</c> é o tempo lançado por essa pessoa, e não
    /// o total do cartão. Vazio devolve zero, sem somar o de ninguém.
    /// </param>
    public static IReadOnlyList<WorkItemDto> ToCards(
        IEnumerable<WorkItem> items,
        string? currentUserId)
    {
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
            UserTimeSeconds = string.IsNullOrEmpty(currentUserId) ? 0 : w.TimeEntries
                .Where(entry => entry.UserId == currentUserId)
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
