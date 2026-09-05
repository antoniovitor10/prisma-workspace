using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Domain.Exceptions;
using Detran.Kanban.Domain.Services;
using MediatR;

namespace Detran.Kanban.Application.Features.WorkItems.Commands;

/// <summary>
/// Handler do comando de criação de WorkItem.
/// Suporta criação em múltiplos quadros via BoardIds ou resolução pelo ProjectId.
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

    public CreateWorkItemCommandHandler(
        IWorkItemRepository workItemRepository,
        IBoardRepository boardRepository,
        IStageRepository stageRepository,
        IProjectRepository projectRepository,
        ITeamRepository teamRepository,
        IUserDirectory users,
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
    }

    public async Task<Guid> Handle(CreateWorkItemCommand request, CancellationToken cancellationToken)
    {
        // 1. Resolver lista de quadros de destino
        List<Guid> boardIds = await ResolverBoardIdsAsync(request, cancellationToken);

        // 2. Carregar todos os quadros e validar existência
        var boards = new List<Board>();
        foreach (var bid in boardIds)
        {
            var b = await _boardRepository.GetByIdAsync(bid, cancellationToken);
            if (b is null)
                throw new ArgumentException($"O Quadro {bid} não existe.");
            boards.Add(b);
        }

        var homeBoard = boards[0];

        if (_permissions is not null && request.CreatedBy is not null)
        {
            var scope = homeBoard.ProjectId.HasValue
                ? PermissionScope.Project
                : homeBoard.TeamId.HasValue ? PermissionScope.Team : PermissionScope.Organization;
            await _permissions.EnsureAsync(
                request.CreatedBy, PlatformPermission.Create, scope,
                homeBoard.ProjectId ?? homeBoard.TeamId, cancellationToken);
        }

        // 3. Para cada quadro, localizar estágio Backlog (nome exato primeiro, depois Category Ready)
        var stagePorBoard = new Dictionary<Guid, Stage>();
        foreach (var board in boards)
        {
            var stages = await _stageRepository.GetByBoardIdAsync(board.Id, cancellationToken);
            var backlog = stages.FirstOrDefault(s =>
                    string.Equals(s.Name.Trim(), "Backlog", StringComparison.OrdinalIgnoreCase))
                ?? stages.FirstOrDefault(s =>
                    s.Name.Contains("backlog", StringComparison.OrdinalIgnoreCase))
                ?? stages.OrderBy(s => s.Position)
                    .FirstOrDefault(s => s.Category is StageCategory.Ready or StageCategory.Backlog);
            if (backlog is null)
                throw new ArgumentException(
                    $"O quadro '{board.Name}' não possui uma etapa 'Backlog'. " +
                    "Crie uma etapa com nome 'Backlog' e categoria Ready antes de adicionar itens.");
            stagePorBoard[board.Id] = backlog;
        }

        var homeBacklog = stagePorBoard[homeBoard.Id];

        // 4. Coluna de destino explícita (se informada) — deve pertencer ao quadro home
        Stage? selectedStage = homeBacklog;
        if (request.StageId.HasValue)
        {
            selectedStage = await _stageRepository.GetByIdAsync(request.StageId.Value, cancellationToken);
            if (selectedStage is null || selectedStage.BoardId != homeBoard.Id)
                throw new ArgumentException("A etapa especificada não pertence ao quadro informado.");
            if (selectedStage.WipLimit.HasValue && _workflow is not null)
            {
                var count = await _workflow.CountActiveItemsInStageAsync(selectedStage.Id, ct: cancellationToken);
                DomainException.Garantir(count < selectedStage.WipLimit.Value,
                    $"A coluna '{selectedStage.Name}' atingiu o limite de WIP ({selectedStage.WipLimit.Value}).");
            }
        }

        // 5. Validar tarefa pai
        WorkItem? parent = null;
        if (request.ParentId.HasValue)
        {
            parent = await _workItemRepository.GetByIdAsync(request.ParentId.Value, cancellationToken);
            if (parent is null)
                throw new ArgumentException("A tarefa pai especificada não existe.");
            // Pai deve compartilhar pelo menos um quadro com o item
            var parentBoardIds = parent.BoardPlacements.Select(p => p.BoardId)
                .Append(parent.BoardId)
                .Distinct();
            bool comparteQuadro = parentBoardIds.Any(bid => boardIds.Contains(bid));
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
            var users = await _users.GetDisplayNamesAsync(participantIds, cancellationToken);
            if (users.Count != participantIds.Count)
                throw new ArgumentException("Um ou mais participantes nao existem.");
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

        // Placement no quadro home (sempre cria)
        workItem.BoardPlacements.Add(new WorkItemBoardPlacement
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItem.Id,
            BoardId = homeBoard.Id,
            StageId = selectedStage?.Id,
            Position = request.Position,
            CreatedAt = now,
            UpdatedAt = now
        });

        // Placements nos quadros extras (exceto home já adicionado)
        foreach (var board in boards.Skip(1))
        {
            var stage = stagePorBoard[board.Id];
            workItem.BoardPlacements.Add(new WorkItemBoardPlacement
            {
                Id = Guid.NewGuid(),
                WorkItemId = workItem.Id,
                BoardId = board.Id,
                StageId = stage.Id,
                Position = request.Position,
                CreatedAt = now,
                UpdatedAt = now
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
                    homeBoard.ProjectId.HasValue
                        ? $"/projects/{homeBoard.ProjectId}/backlog?item={workItem.Id}"
                        : $"/boards/{homeBoard.Id}?item={workItem.Id}",
                    WorkItemId: workItem.Id, ProjectId: homeBoard.ProjectId)), cancellationToken);

        if (_realtime is not null)
        {
            foreach (var bid in boardIds)
                await _realtime.BoardChangedAsync(bid, "workItemCreated", workItem.Id, cancellationToken);
        }

        return workItem.Id;
    }

    /// <summary>
    /// Resolve a lista definitiva de board IDs a partir das opções do request.
    /// Prioridade: BoardIds > BoardId > ProjectId.DefaultBoardId.
    /// </summary>
    private async Task<List<Guid>> ResolverBoardIdsAsync(
        CreateWorkItemCommand request,
        CancellationToken cancellationToken)
    {
        if (request.BoardIds is { Count: > 0 })
            return request.BoardIds.Distinct().ToList();

        if (request.BoardId != Guid.Empty)
            return [request.BoardId];

        if (request.ProjectId.HasValue)
        {
            var project = await _projectRepository.GetByIdAsync(request.ProjectId.Value, cancellationToken);
            if (project is null)
                throw new ArgumentException("O Projeto especificado não existe.");
            if (project.DefaultBoardId is null)
                throw new ArgumentException(
                    "O projeto não possui um quadro padrão. Crie um quadro antes de adicionar itens.");
            return [project.DefaultBoardId.Value];
        }

        throw new ArgumentException("Informe BoardId, BoardIds ou ProjectId para definir o quadro da tarefa.");
    }
}
