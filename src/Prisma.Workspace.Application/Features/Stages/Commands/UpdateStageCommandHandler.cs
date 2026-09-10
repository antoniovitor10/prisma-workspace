using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using MediatR;

namespace Prisma.Workspace.Application.Features.Stages.Commands;

/// <summary>
/// Handler para atualização de Stage no fluxo do projeto.
/// </summary>
public class UpdateStageCommandHandler : IRequestHandler<UpdateStageCommand>
{
    private readonly IStageRepository _stageRepository;
    private readonly IWorkflowRepository? _workflow;
    private readonly IProjectAccessService? _access;
    private readonly IPermissionService? _permissions;

    public UpdateStageCommandHandler(
        IStageRepository stageRepository,
        IWorkflowRepository? workflow = null,
        IProjectAccessService? access = null,
        IPermissionService? permissions = null)
    {
        _stageRepository = stageRepository;
        _workflow = workflow;
        _access = access;
        _permissions = permissions;
    }

    public async Task Handle(UpdateStageCommand request, CancellationToken cancellationToken)
    {
        var stage = await _stageRepository.GetByIdAsync(request.StageId, cancellationToken);
        if (stage is null)
            throw new ArgumentException("A etapa especificada não existe.");

        if (request.ActorId is not null && _access is not null)
        {
            await _access.EnsureAtLeastAsync(
                stage.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, cancellationToken);
            if (_permissions is not null)
                await _permissions.EnsureAsync(request.ActorId, PlatformPermission.Edit,
                    PermissionScope.Project, stage.ProjectId, cancellationToken);
        }

        stage.Name = request.Name.Trim();

        if (request.Category.HasValue)
        {
            stage.Category = request.Category.Value;
        }

        if (stage.WorkflowStatusId.HasValue && _workflow is not null)
        {
            var status = await _workflow.GetStatusAsync(stage.WorkflowStatusId.Value, cancellationToken);
            if (status is not null && status.ProjectId == stage.ProjectId)
            {
                var targetCategory = request.Category ?? status.Category;
                status.Update(
                    request.Name.Trim(),
                    request.Color ?? status.Color,
                    status.Position,
                    targetCategory,
                    status.IsInitial,
                    targetCategory == StageCategory.Done);
                await _workflow.SaveAsync(cancellationToken);
            }
        }

        await _stageRepository.UpdateAsync(stage, cancellationToken);
    }
}
