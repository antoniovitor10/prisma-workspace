using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using MediatR;

namespace Prisma.Workspace.Application.Features.Stages.Commands;

/// <summary>
/// Reordena etapas do fluxo do projeto. Aceita lista parcial ou completa de IDs.
/// </summary>
public class ReorderStagesCommandHandler : IRequestHandler<ReorderStagesCommand>
{
    private readonly IStageRepository _stageRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectAccessService? _access;

    public ReorderStagesCommandHandler(
        IStageRepository stageRepository,
        IProjectRepository projectRepository,
        IProjectAccessService? access = null)
    {
        _stageRepository = stageRepository;
        _projectRepository = projectRepository;
        _access = access;
    }

    public async Task Handle(ReorderStagesCommand request, CancellationToken cancellationToken)
    {
        DomainException.Garantir(request.OrderedStageIds.Count > 0,
            "A lista de etapas não pode ser vazia.");

        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);
        if (project is null)
            throw new ArgumentException("O projeto especificado não existe.");

        if (_access is not null)
            await _access.EnsureAtLeastAsync(
                request.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, cancellationToken);

        var stagesDoProjeto = await _stageRepository.GetByProjectIdAsync(request.ProjectId, cancellationToken);
        var idsDoProjetoSet = stagesDoProjeto.Select(s => s.Id).ToHashSet();

        foreach (var sid in request.OrderedStageIds)
            DomainException.Garantir(idsDoProjetoSet.Contains(sid),
                $"A etapa {sid} não pertence ao projeto {request.ProjectId}.");

        for (int i = 0; i < request.OrderedStageIds.Count; i++)
        {
            var stage = stagesDoProjeto.First(s => s.Id == request.OrderedStageIds[i]);
            stage.Position = (i + 1) * 100.0;
            await _stageRepository.UpdateAsync(stage, cancellationToken);
        }
    }
}
