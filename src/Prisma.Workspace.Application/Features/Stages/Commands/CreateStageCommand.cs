using MediatR;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Features.Stages.Commands;

/// <summary>
/// Comando para criar uma nova etapa (Stage) no fluxo do projeto.
/// </summary>
public record CreateStageCommand(
    Guid ProjectId,
    string Name,
    double Position,
    Guid? WorkflowStatusId = null,
    StageCategory Category = StageCategory.InProgress,
    string Color = "#64748B",
    string? ActorId = null) : IRequest<Guid>;
