using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using MediatR;

namespace Prisma.Workspace.Application.Features.Stages.Commands;

/// <summary>
/// Handler para criação de Stage no fluxo do projeto.
/// </summary>
public class CreateStageCommandHandler : IRequestHandler<CreateStageCommand, Guid>
{
    private readonly IStageRepository _stageRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IWorkflowRepository? _workflow;
    private readonly IProjectAccessService? _access;
    private readonly IPermissionService? _permissions;

    public CreateStageCommandHandler(
        IStageRepository stageRepository,
        IProjectRepository projectRepository,
        IWorkflowRepository? workflow = null,
        IProjectAccessService? access = null,
        IPermissionService? permissions = null)
    {
        _stageRepository = stageRepository;
        _projectRepository = projectRepository;
        _workflow = workflow;
        _access = access;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(CreateStageCommand request, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);
        if (project is null)
            throw new ArgumentException("O projeto especificado não existe.");

        if (request.ActorId is not null && _access is not null)
        {
            await _access.EnsureAtLeastAsync(
                request.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, cancellationToken);
            if (_permissions is not null)
                await _permissions.EnsureAsync(request.ActorId, PlatformPermission.Edit,
                    PermissionScope.Project, request.ProjectId, cancellationToken);
        }

        WorkflowStatus? workflowStatus = null;
        if (request.WorkflowStatusId.HasValue && _workflow is not null)
        {
            workflowStatus = await _workflow.GetStatusAsync(request.WorkflowStatusId.Value, cancellationToken);
            DomainException.Garantir(workflowStatus?.ProjectId == request.ProjectId,
                "O status informado não pertence ao projeto.");
        }
        else if (_workflow is not null)
        {
            var currentStatuses = await _workflow.GetStatusesAsync(request.ProjectId, ct: cancellationToken);
            var inheritance = await _workflow.GetProjectInheritanceModeAsync(
                request.ProjectId, cancellationToken);
            if (inheritance == WorkflowInheritanceMode.Inherited)
            {
                workflowStatus = currentStatuses.Where(x => x.IsActive)
                    .OrderBy(x => x.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(x => x.Position)
                    .FirstOrDefault(x => x.Category == request.Category);
                DomainException.Garantir(workflowStatus is not null,
                    "O template herdado não possui status ativo compatível com a categoria da etapa.");
            }
            else
            {
                workflowStatus = WorkflowStatus.Create(request.ProjectId, request.Name, request.Color,
                    currentStatuses.Count == 0 ? 0 : currentStatuses.Max(x => x.Position) + 1,
                    request.Category, currentStatuses.Count == 0, request.Category == StageCategory.Done);
                _workflow.AddStatus(workflowStatus);
                var currentTransitions = await _workflow.GetTransitionsAsync(request.ProjectId, cancellationToken);
                var permissiveTransitions = currentTransitions
                    .Select(x => WorkflowTransition.Create(x.SourceStatusId, x.TargetStatusId))
                    .Concat(currentStatuses.SelectMany(x => new[]
                {
                    WorkflowTransition.Create(x.Id, workflowStatus.Id),
                    WorkflowTransition.Create(workflowStatus.Id, x.Id)
                })).ToList();
                _workflow.ReplaceTransitions(currentTransitions, permissiveTransitions);
                await _workflow.SaveAsync(cancellationToken);
            }
        }

        var stage = new Stage
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            WorkflowStatusId = workflowStatus?.Id,
            Name = request.Name,
            Position = request.Position,
            Category = workflowStatus?.Category ?? request.Category,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _stageRepository.AddAsync(stage, cancellationToken);
        return stage.Id;
    }
}
