using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Domain.Services;
using MediatR;

namespace Prisma.Workspace.Application.Features.WorkItems.Commands;

/// <summary>
/// Handler do comando de criação de WorkItem.
/// A tarefa nasce em um único quadro, informado por BoardId ou pelo padrão do projeto.
/// </summary>
public class CreateWorkItemCommandHandler : IRequestHandler<CreateWorkItemCommand, Guid>
{
    private readonly IWorkItemRepository _workItemRepository;
    private readonly IBoardRepository _boardRepository;
    private readonly IStageRepository _stageRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IUserDirectory _users;
    private readonly IPermissionService? _permissions;
    private readonly IWorkflowRepository? _workflow;
    private readonly IBoardRealtimeNotifier? _realtime;
    private readonly IPlatformNotificationPublisher? _notifications;
    private readonly IProjectAccessService _projectAccess;

    public CreateWorkItemCommandHandler(
        IWorkItemRepository workItemRepository,
        IBoardRepository boardRepository,
        IStageRepository stageRepository,
        IProjectRepository projectRepository,
        ITeamRepository teamRepository,
        IUserDirectory users,
        IProjectAccessService projectAccess,
        IPermissionService? permissions = null,
        IWorkflowRepository? workflow = null,
        IBoardRealtimeNotifier? realtime = null,
        IPlatformNotificationPublisher? notifications = null)
    {
        _workItemRepository = workItemRepository;
        _boardRepository = boardRepository;
        _stageRepository = stageRepository;
        _projectRepository = projectRepository;
        _teamRepository = teamRepository;
        _users = users;
        _permissions = permissions;
        _workflow = workflow;
        _realtime = realtime;
        _notifications = notifications;
        _projectAccess = projectAccess;
    }

    public async Task<Guid> Handle(CreateWorkItemCommand request, CancellationToken cancellationToken)
    {
        // 1. Resolver o quadro de destino. A tarefa pertence a um único quadro (D83).
        var boardId = await ResolverBoardIdAsync(request, cancellationToken);
        var homeBoard = await _boardRepository.GetByIdAsync(boardId, cancellationToken)
            ?? throw new ArgumentException($"O Quadro {boardId} não existe.");

        if (_permissions is not null && request.CreatedBy is not null)
        {
            await _permissions.EnsureAsync(
                request.CreatedBy, PlatformPermission.Create, PermissionScope.Project,
                homeBoard.ProjectId, cancellationToken);
        }

        // 2. Localizar a etapa Backlog no fluxo do projeto (nome exato primeiro, depois Category Ready)
        var projectStages = await _stageRepository.GetByBoardIdAsync(homeBoard.Id, cancellationToken);
        var homeBacklog = request.StageId.HasValue ? null : projectStages.OrderBy(s => s.Position)
            .FirstOrDefault(s => s.Category != StageCategory.Done)
            ?? throw new ArgumentException("Escolha uma coluna de destino antes de adicionar itens.");

        // 3. Coluna de destino explícita (se informada) — deve pertencer ao projeto da tarefa
        Stage? selectedStage = homeBacklog;
        if (request.StageId.HasValue)
        {
            selectedStage = await _stageRepository.GetByIdAsync(request.StageId.Value, cancellationToken);
            if (selectedStage is null || selectedStage.ProjectId != homeBoard.ProjectId || selectedStage.BoardId != homeBoard.Id)
                throw new ArgumentException("A etapa especificada não pertence ao projeto da tarefa.");
            // Limite de WIP removido do produto pela D83.
        }

        // 5. Validar tarefa pai
        WorkItem? parent = null;
        if (request.ParentId.HasValue)
        {
            parent = await _workItemRepository.GetByIdAsync(request.ParentId.Value, cancellationToken);
            if (parent is null)
                throw new ArgumentException("A tarefa pai especificada não existe.");
            // Pai deve estar no mesmo quadro do item
            bool comparteQuadro = parent.BoardId == homeBoard.Id;
            if (!comparteQuadro)
                throw new ArgumentException(
                    "A tarefa pai não compartilha nenhum quadro com o item sendo criado.");
        }

        var effectiveKind = request.ParentId.HasValue
            && request.Kind == WorkItemKind.Task
            && parent?.Kind == WorkItemKind.Task
                ? WorkItemKind.Subtask
                : request.Kind;
        if (parent is not null)
            DomainException.Garantir(WorkItemHierarchyRules.IsAllowed(parent.Kind, effectiveKind),
                $"A relação {parent.Kind} → {effectiveKind} não pertence à hierarquia de trabalho permitida.");

        if (request.TeamId.HasValue)
            _ = await _teamRepository.GetByIdAsync(request.TeamId.Value, cancellationToken)
                ?? throw new ArgumentException("A equipe especificada nao existe.");

        var participantIds = (request.ParticipantIds ?? [])
            .Append(request.ResponsibleId ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
        if (participantIds.Count > 0)
        {
            if (_permissions is not null && !string.IsNullOrWhiteSpace(request.CreatedBy))
                await _permissions.EnsureAsync(request.CreatedBy, PlatformPermission.Assign,
                    PermissionScope.Project, homeBoard.ProjectId, cancellationToken);
            var users = await _users.GetByIdsAsync(participantIds, includeInactive: false, cancellationToken);
            if (users.Count != participantIds.Count)
                throw new ArgumentException("Um ou mais participantes nao existem.");
            foreach (var participantId in participantIds)
            {
                var role = await _projectAccess.GetRoleAsync(homeBoard.ProjectId, participantId, cancellationToken);
                DomainException.Garantir(role is not null,
                    "Conceda acesso ao projeto antes de atribuir a tarefa.");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            BoardId = homeBoard.Id,
            StageId = selectedStage?.Id,
            WorkflowStatusId = selectedStage?.WorkflowStatusId,
            ParentId = request.ParentId,
            TeamId = request.TeamId,
            Title = request.Title,
            Subtitle = request.Subtitle,
            Description = request.Description,
            Priority = request.Priority,
            Kind = effectiveKind,
            Origin = request.Origin,
            ResponsibleId = string.IsNullOrWhiteSpace(request.ResponsibleId) ? null : request.ResponsibleId,
            RequesterId = string.IsNullOrWhiteSpace(request.RequesterId) ? null : request.RequesterId,
            RequesterName = string.IsNullOrWhiteSpace(request.RequesterName) ? null : request.RequesterName.Trim(),
            RequesterEmail = string.IsNullOrWhiteSpace(request.RequesterEmail) ? null : request.RequesterEmail.Trim(),
            EstimatedHours = request.EstimatedHours,
            RemainingHours = request.RemainingHours ?? request.EstimatedHours,
            SprintId = request.SprintId,
            DueDate = request.DueDate,
            StartDate = request.StartDate,
            AcceptanceCriteria = string.IsNullOrWhiteSpace(request.AcceptanceCriteria)
                ? null : request.AcceptanceCriteria.Trim(),
            Position = request.Position,
            BacklogRank = Convert.ToDecimal(request.Position),
            CreatedBy = request.CreatedBy,
            CreatedAt = now,
            CompletedAt = selectedStage?.Category == StageCategory.Done ? now : null,
            UpdatedAt = now
        };

        foreach (var participantId in participantIds)
            workItem.Assignees.Add(new WorkItemAssignee
            {
                WorkItemId = workItem.Id,
                UserId = participantId,
                AssignedAt = now
            });

        // Registro de histórico para lead time (D5)
        if (selectedStage?.Id != null)
        {
            workItem.StageHistories.Add(new StageHistory
            {
                Id = Guid.NewGuid(),
                WorkItemId = workItem.Id,
                StageId = selectedStage.Id,
                EnteredAt = now
            });
        }

        await _workItemRepository.AddAsync(workItem, cancellationToken);

        if (_notifications is not null)
            await _notifications.PublishManyAsync(participantIds
                .Where(userId => userId != request.CreatedBy)
                .Select(userId => new NotificationEnvelope(
                    homeBoard.OrganizationId, userId, NotificationType.TaskAssigned,
                    "Nova tarefa atribuída",
                    $"#{workItem.Number} {workItem.Title} foi atribuída a você.",
                    $"/projects/{homeBoard.ProjectId}/backlog?item={workItem.Id}",
                    WorkItemId: workItem.Id, ProjectId: homeBoard.ProjectId)), cancellationToken);

        if (_realtime is not null)
        {
            await _realtime.BoardChangedAsync(
                homeBoard.Id, "workItemCreated", workItem.Id, cancellationToken);
        }

        return workItem.Id;
    }

    /// <summary>
    /// Resolve a lista definitiva de board IDs a partir das opções do request.
    /// Prioridade: BoardIds > BoardId > ProjectId.DefaultBoardId.
    /// </summary>
    private async Task<Guid> ResolverBoardIdAsync(
        CreateWorkItemCommand request,
        CancellationToken cancellationToken)
    {
        if (request.BoardId != Guid.Empty)
            return request.BoardId;

        if (request.ProjectId.HasValue)
        {
            var project = await _projectRepository.GetByIdAsync(request.ProjectId.Value, cancellationToken);
            if (project is null)
                throw new ArgumentException("O Projeto especificado não existe.");
            if (project.DefaultBoardId is null)
                throw new ArgumentException(
                    "O projeto não possui um quadro padrão. Crie um quadro antes de adicionar itens.");
            return project.DefaultBoardId.Value;
        }

        throw new ArgumentException("Informe BoardId ou ProjectId para definir o quadro da tarefa.");
    }
}
