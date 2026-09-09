using MediatR;

namespace Prisma.Workspace.Application.Features.Stages.Commands;

/// <summary>
/// Reordena as etapas do projeto atribuindo Position = (índice + 1) * 100.
/// </summary>
public record ReorderStagesCommand(
    Guid ProjectId,
    IReadOnlyList<Guid> OrderedStageIds,
    string ActorId) : IRequest;
