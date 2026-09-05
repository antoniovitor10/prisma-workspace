using MediatR;

namespace Prisma.Workspace.Application.Features.Stages.Commands;

/// <summary>
/// Reordena as etapas de um quadro atribuindo Position = (índice + 1) * 100.
/// </summary>
public record ReorderStagesCommand(
    Guid BoardId,
    IReadOnlyList<Guid> OrderedStageIds,
    string ActorId) : IRequest;
