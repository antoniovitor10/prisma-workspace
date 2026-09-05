using Prisma.Workspace.Domain.Enums;
using MediatR;

namespace Prisma.Workspace.Application.Features.WorkItems.Commands;

/// <summary>
/// Comando para criar um novo item de trabalho (WorkItem).
/// Permite especificar BoardId (quadro home), uma lista de BoardIds (multi-board)
/// ou apenas ProjectId (usa o DefaultBoard do projeto).
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
    /// Lista de quadros onde o item deve aparecer (multi-board).
    /// Quando preenchida, substitui BoardId como lista de destinos.
    /// </summary>
    IReadOnlyList<Guid>? BoardIds = null,
    /// <summary>
    /// Projecto de origem; quando BoardId e BoardIds são vazios, o quadro padrão é usado.
    /// </summary>
    Guid? ProjectId = null
) : IRequest<Guid>;
