using Prisma.Workspace.Application.Features.Stages.Dtos;
using MediatR;

namespace Prisma.Workspace.Application.Features.Stages.Queries;

/// <summary>
/// Query para buscar todas as etapas (Stages) do fluxo de um projeto.
/// </summary>
public record GetStagesByProjectIdQuery(Guid ProjectId, string ActorId)
    : IRequest<IReadOnlyList<StageDto>>;
