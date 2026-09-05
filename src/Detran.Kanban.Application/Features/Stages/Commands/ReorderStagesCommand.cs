using MediatR;

namespace Detran.Kanban.Application.Features.Stages.Commands;

/// <summary>
/// Reordena as etapas de um quadro atribuindo Position = (índice + 1) * 100.
/// </summary>
public record ReorderStagesCommand(
    Guid BoardId,
    IReadOnlyList<Guid> OrderedStageIds,
    string ActorId) : IRequest;
