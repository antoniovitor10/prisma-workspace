using MediatR;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Features.Stages.Commands;

/// <summary>
/// Comando para criar uma nova etapa (Stage) em um Quadro.
/// </summary>
public record CreateStageCommand(
    Guid BoardId,
    string Name,
    double Position,
    int? WipLimit,
    Guid? WorkflowStatusId = null,
    StageCategory Category = StageCategory.InProgress,
    string Color = "#64748B",
    string? ActorId = null) : IRequest<Guid>;
