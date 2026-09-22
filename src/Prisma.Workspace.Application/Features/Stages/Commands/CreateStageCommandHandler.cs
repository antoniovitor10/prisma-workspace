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
    private readonly IBoardRepository? _boards;

    public CreateStageCommandHandler(
        IStageRepository stageRepository,
        IProjectRepository projectRepository,
        IWorkflowRepository? workflow = null,
        IProjectAccessService? access = null,
        IPermissionService? permissions = null,
        IBoardRepository? boards = null)
    {
        _stageRepository = stageRepository;
        _projectRepository = projectRepository;
        _workflow = workflow;
        _access = access;
        _permissions = permissions;
        _boards = boards;
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

        DomainException.Garantir(request.BoardId.HasValue, "Escolha o quadro da coluna.");
        var board = _boards is null ? null : await _boards.GetByIdAsync(request.BoardId!.Value, cancellationToken);
        DomainException.Garantir(board?.ProjectId == request.ProjectId, "O quadro informado não pertence ao projeto.");
        // O status técnico é exclusivo desta coluna; templates legados não governam o quadro.
        var workflowStatus = WorkflowStatus.Create(request.ProjectId, request.Name, request.Color,
            request.Position, request.Category, false, request.Category == StageCategory.Done);

        var stage = new Stage
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            BoardId = request.BoardId,
            WorkflowStatusId = workflowStatus.Id,
            WorkflowStatus = workflowStatus,
            Name = request.Name,
            Position = request.Position,
            Category = request.Category,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _stageRepository.AddAsync(stage, cancellationToken);
        return stage.Id;
    }
}
