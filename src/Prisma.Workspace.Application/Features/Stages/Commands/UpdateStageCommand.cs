using MediatR;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Features.Stages.Commands;

/// <summary>
/// Comando para atualizar uma etapa (Stage) existente no fluxo do projeto.
/// </summary>
public record UpdateStageCommand(
    Guid StageId,
    string Name,
    StageCategory? Category = null,
    string? Color = null,
    string? ActorId = null) : IRequest;
