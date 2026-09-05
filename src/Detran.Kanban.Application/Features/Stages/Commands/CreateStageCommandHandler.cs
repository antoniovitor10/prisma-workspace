using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Domain.Exceptions;
using MediatR;

namespace Detran.Kanban.Application.Features.Stages.Commands;

/// <summary>
/// Handler para criação de Stage.
/// </summary>
public class CreateStageCommandHandler : IRequestHandler<CreateStageCommand, Guid>
{
    private readonly IStageRepository _stageRepository;
    private readonly IBoardRepository _boardRepository;
    private readonly IWorkflowRepository? _workflow;
    private readonly IProjectAccessService? _access;
    private readonly IPermissionService? _permissions;

    public CreateStageCommandHandler(
        IStageRepository stageRepository,
        IBoardRepository boardRepository,
        IWorkflowRepository? workflow = null,
        IProjectAccessService? access = null,
        IPermissionService? permissions = null)
    {
        _stageRepository = stageRepository;
        _boardRepository = boardRepository;
        _workflow = workflow;
        _access = access;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(CreateStageCommand request, CancellationToken cancellationToken)
    {
        var board = await _boardRepository.GetByIdAsync(request.BoardId, cancellationToken);
        if (board is null)
        {
            throw new ArgumentException("O Quadro especificado não existe.");
        }

        DomainException.Garantir(request.WipLimit is null or > 0,
            "O limite de WIP deve ser maior que zero.");
        if (board.ProjectId.HasValue && request.ActorId is not null && _access is not null)
        {
            await _access.EnsureAtLeastAsync(
                board.ProjectId.Value, request.ActorId, ProjectRole.ProjectAdmin, cancellationToken);
            if (_permissions is not null)
                await _permissions.EnsureAsync(request.ActorId, PlatformPermission.Edit,
                    PermissionScope.Project, board.ProjectId.Value, cancellationToken);
        }

        WorkflowStatus? workflowStatus = null;
        if (request.WorkflowStatusId.HasValue && _workflow is not null)
        {
            workflowStatus = await _workflow.GetStatusAsync(request.WorkflowStatusId.Value, cancellationToken);
            DomainException.Garantir(workflowStatus?.ProjectId == board.ProjectId,
                "O status informado nao pertence ao projeto do quadro.");
        }
        else if (board.ProjectId.HasValue && _workflow is not null)
        {
            var currentStatuses = await _workflow.GetStatusesAsync(board.ProjectId.Value, ct: cancellationToken);
            var inheritance = await _workflow.GetProjectInheritanceModeAsync(
                board.ProjectId.Value, cancellationToken);
            if (inheritance == WorkflowInheritanceMode.Inherited)
            {
                workflowStatus = currentStatuses.Where(x => x.IsActive)
                    .OrderBy(x => x.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(x => x.Position)
                    .FirstOrDefault(x => x.Category == request.Category);
                DomainException.Garantir(workflowStatus is not null,
                    "O template herdado nao possui status ativo compativel com a categoria da etapa.");
            }
            else
            {
                workflowStatus = WorkflowStatus.Create(board.ProjectId.Value, request.Name, request.Color,
                    currentStatuses.Count == 0 ? 0 : currentStatuses.Max(x => x.Position) + 1,
                    request.Category, currentStatuses.Count == 0, request.Category == StageCategory.Done);
                _workflow.AddStatus(workflowStatus);
                var currentTransitions = await _workflow.GetTransitionsAsync(board.ProjectId.Value, cancellationToken);
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
            BoardId = request.BoardId,
            WorkflowStatusId = workflowStatus?.Id,
            Name = request.Name,
            Position = request.Position,
            WipLimit = request.WipLimit,
            Category = workflowStatus?.Category ?? request.Category,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _stageRepository.AddAsync(stage, cancellationToken);
        return stage.Id;
    }
}
