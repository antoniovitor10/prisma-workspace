using MediatR;

namespace Detran.Kanban.Application.Features.WorkItems.Commands;

/// <summary>
/// Comando para mover um item de trabalho (WorkItem) para outra etapa (Stage) ou posição.
/// </summary>
public record MoveWorkItemCommand(
    Guid WorkItemId,
    Guid? DestinationStageId,
    double Position,
    string? ActorId = null,
    string? ActorName = null,
    string? Reason = null) : IRequest;
