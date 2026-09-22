using Prisma.Workspace.Domain.Enums;
using MediatR;

namespace Prisma.Workspace.Application.Features.WorkItems.Commands;

/// <summary>
/// Comando para criar um novo item de trabalho (WorkItem).
/// Permite especificar BoardId ou apenas ProjectId (usa o DefaultBoard do projeto).
/// A tarefa pertence a um único quadro (D83).
/// </summary>
public record CreateWorkItemCommand(
    Guid BoardId,
    Guid? StageId,
    Guid? ParentId,
    string Title,
    string? Subtitle,
    string? Description,
    Priority Priority,
    decimal? EstimatedHours,
    DateOnly? DueDate,
    double Position,
    string? CreatedBy,
    WorkItemKind Kind = WorkItemKind.Task,
    Guid? SprintId = null,
    decimal? RemainingHours = null,
    Guid? TeamId = null,
    string? ResponsibleId = null,
    IReadOnlyList<string>? ParticipantIds = null,
    WorkItemOrigin Origin = WorkItemOrigin.Internal,
    string? RequesterId = null,
    string? RequesterName = null,
    string? RequesterEmail = null,
    DateOnly? StartDate = null,
    string? AcceptanceCriteria = null,
    /// <summary>
    /// Projeto de origem; quando BoardId é vazio, o quadro padrão do projeto é usado.
    /// </summary>
    Guid? ProjectId = null
) : IRequest<Guid>;
