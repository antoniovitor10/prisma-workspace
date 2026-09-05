using System.Text.Json;
using Detran.Kanban.Application.Common.Exceptions;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Domain.Exceptions;
using MediatR;

namespace Detran.Kanban.Application.Features.Workflow;

public sealed record WorkflowStatusDto(
    Guid Id, string Name, string Color, double Position, StageCategory Category,
    bool IsInitial, bool IsFinal);
public sealed record WorkflowTransitionDto(Guid SourceStatusId, Guid TargetStatusId);
public sealed record WorkflowStageDto(
    Guid Id, Guid BoardId, string BoardName, string Name, double Position,
    int? WipLimit, Guid? WorkflowStatusId);
public sealed record WorkflowBoardDto(Guid Id, string Name, string? CardSettingsJson);
public sealed record ProjectWorkflowDto(
    WorkflowInheritanceMode InheritanceMode, Guid? WorkflowTemplateId, string? WorkflowTemplateName,
    bool IsSynchronized,
    IReadOnlyList<WorkflowStatusDto> Statuses,
    IReadOnlyList<WorkflowTransitionDto> Transitions,
    IReadOnlyList<WorkflowStageDto> Stages,
    IReadOnlyList<WorkflowBoardDto> Boards);

public sealed record GetProjectWorkflowQuery(Guid ProjectId, string UserId) : IRequest<ProjectWorkflowDto>;

public sealed class GetProjectWorkflowQueryHandler : IRequestHandler<GetProjectWorkflowQuery, ProjectWorkflowDto>
{
    private readonly IWorkflowRepository _workflow;
    private readonly IProjectRepository _projects;
    private readonly IProjectAccessService _access;

    public GetProjectWorkflowQueryHandler(
        IWorkflowRepository workflow, IProjectRepository projects, IProjectAccessService access)
        => (_workflow, _projects, _access) = (workflow, projects, access);

    public async Task<ProjectWorkflowDto> Handle(GetProjectWorkflowQuery request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.UserId, ProjectRole.Viewer, ct);
        var project = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NaoEncontradoException("Projeto");
        var statuses = await _workflow.GetStatusesAsync(request.ProjectId, ct: ct);
        var transitions = await _workflow.GetTransitionsAsync(request.ProjectId, ct);

        var boards = new List<WorkflowBoardDto>();
        var stages = new List<WorkflowStageDto>();
        foreach (var projectBoard in project.Boards.OrderBy(x => x.Name))
        {
            var board = await _workflow.GetBoardAsync(projectBoard.Id, ct);
            if (board is null) continue;
            boards.Add(new WorkflowBoardDto(board.Id, board.Name, board.CardSettingsJson));
            stages.AddRange(board.Stages.OrderBy(x => x.Position).Select(x =>
                new WorkflowStageDto(x.Id, board.Id, board.Name, x.Name, x.Position,
                    x.WipLimit, x.WorkflowStatusId)));
        }

        return new ProjectWorkflowDto(
            project.WorkflowInheritanceMode, project.WorkflowTemplateId, project.WorkflowTemplate?.Name,
            project.WorkflowInheritanceMode == WorkflowInheritanceMode.Custom
                || project.WorkflowTemplateVersion == project.WorkflowTemplate?.Version,
            statuses.Select(Map).ToList(),
            transitions.Select(x => new WorkflowTransitionDto(x.SourceStatusId, x.TargetStatusId)).ToList(),
            stages,
            boards);
    }

    internal static WorkflowStatusDto Map(WorkflowStatus x)
        => new(x.Id, x.Name, x.Color, x.Position, x.Category, x.IsInitial, x.IsFinal);
}

public sealed record CreateWorkflowStatusCommand(
    Guid ProjectId, string Name, string Color, double Position, StageCategory Category,
    bool IsInitial, bool IsFinal, string ActorId) : IRequest<WorkflowStatusDto>;

public sealed class CreateWorkflowStatusCommandHandler
    : IRequestHandler<CreateWorkflowStatusCommand, WorkflowStatusDto>
{
    private readonly IWorkflowRepository _workflow;
    private readonly IProjectAccessService _access;
    public CreateWorkflowStatusCommandHandler(IWorkflowRepository workflow, IProjectAccessService access)
        => (_workflow, _access) = (workflow, access);

    public async Task<WorkflowStatusDto> Handle(CreateWorkflowStatusCommand request, CancellationToken ct)
    {
        await WorkflowInheritanceGuard.EnsureCustomAsync(_workflow, request.ProjectId, ct);
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, ct);
        DomainException.Garantir(!await _workflow.NameExistsAsync(request.ProjectId, request.Name.Trim(), ct: ct),
            "Ja existe um status com esse nome no projeto.");

        var current = await _workflow.GetStatusesAsync(request.ProjectId, true, ct);
        var isInitial = request.IsInitial || current.Count == 0;
        if (isInitial)
            foreach (var status in current) status.IsInitial = false;

        var created = WorkflowStatus.Create(request.ProjectId, request.Name, request.Color,
            request.Position, request.Category, isInitial, request.IsFinal);
        _workflow.AddStatus(created);
        await _workflow.SaveAsync(ct);
        return GetProjectWorkflowQueryHandler.Map(created);
    }
}

public sealed record UpdateWorkflowStatusCommand(
    Guid ProjectId, Guid StatusId, string Name, string Color, double Position,
    StageCategory Category, bool IsInitial, bool IsFinal, string ActorId) : IRequest<WorkflowStatusDto>;

public sealed class UpdateWorkflowStatusCommandHandler
    : IRequestHandler<UpdateWorkflowStatusCommand, WorkflowStatusDto>
{
    private readonly IWorkflowRepository _workflow;
    private readonly IProjectAccessService _access;
    public UpdateWorkflowStatusCommandHandler(IWorkflowRepository workflow, IProjectAccessService access)
        => (_workflow, _access) = (workflow, access);

    public async Task<WorkflowStatusDto> Handle(UpdateWorkflowStatusCommand request, CancellationToken ct)
    {
        await WorkflowInheritanceGuard.EnsureCustomAsync(_workflow, request.ProjectId, ct);
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, ct);
        var status = await _workflow.GetStatusAsync(request.StatusId, ct)
            ?? throw new NaoEncontradoException("Status");
        DomainException.Garantir(status.ProjectId == request.ProjectId, "O status nao pertence ao projeto.");
        DomainException.Garantir(!await _workflow.NameExistsAsync(
            request.ProjectId, request.Name.Trim(), request.StatusId, ct),
            "Ja existe um status com esse nome no projeto.");

        var all = await _workflow.GetStatusesAsync(request.ProjectId, true, ct);
        if (request.IsInitial)
            foreach (var other in all.Where(x => x.Id != status.Id)) other.IsInitial = false;
        else if (status.IsInitial)
            DomainException.Garantir(all.Any(x => x.Id != status.Id && x.IsInitial),
                "Defina outro status inicial antes de remover esta marcacao.");

        status.Update(request.Name, request.Color, request.Position, request.Category,
            request.IsInitial, request.IsFinal);
        await _workflow.SaveAsync(ct);
        return GetProjectWorkflowQueryHandler.Map(status);
    }
}

public sealed record DeleteWorkflowStatusCommand(Guid ProjectId, Guid StatusId, string ActorId) : IRequest;
public sealed class DeleteWorkflowStatusCommandHandler : IRequestHandler<DeleteWorkflowStatusCommand>
{
    private readonly IWorkflowRepository _workflow;
    private readonly IProjectAccessService _access;
    public DeleteWorkflowStatusCommandHandler(IWorkflowRepository workflow, IProjectAccessService access)
        => (_workflow, _access) = (workflow, access);

    public async Task Handle(DeleteWorkflowStatusCommand request, CancellationToken ct)
    {
        await WorkflowInheritanceGuard.EnsureCustomAsync(_workflow, request.ProjectId, ct);
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, ct);
        var status = await _workflow.GetStatusAsync(request.StatusId, ct)
            ?? throw new NaoEncontradoException("Status");
        DomainException.Garantir(status.ProjectId == request.ProjectId, "O status nao pertence ao projeto.");
        DomainException.Garantir(!status.IsInitial, "O status inicial nao pode ser excluido.");
        DomainException.Garantir(!await _workflow.IsStatusInUseAsync(status.Id, ct),
            "O status esta em uso por uma coluna ou tarefa e nao pode ser excluido.");
        _workflow.DeleteStatus(status);
        await _workflow.SaveAsync(ct);
    }
}

public sealed record ReorderWorkflowStatusesCommand(
    Guid ProjectId, IReadOnlyList<Guid> StatusIds, string ActorId) : IRequest;
public sealed class ReorderWorkflowStatusesCommandHandler : IRequestHandler<ReorderWorkflowStatusesCommand>
{
    private readonly IWorkflowRepository _workflow;
    private readonly IProjectAccessService _access;
    public ReorderWorkflowStatusesCommandHandler(IWorkflowRepository workflow, IProjectAccessService access)
        => (_workflow, _access) = (workflow, access);

    public async Task Handle(ReorderWorkflowStatusesCommand request, CancellationToken ct)
    {
        await WorkflowInheritanceGuard.EnsureCustomAsync(_workflow, request.ProjectId, ct);
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, ct);
        var statuses = await _workflow.GetStatusesAsync(request.ProjectId, true, ct);
        DomainException.Garantir(request.StatusIds.Count == statuses.Count
            && request.StatusIds.Distinct().Count() == statuses.Count
            && statuses.All(x => request.StatusIds.Contains(x.Id)), "A ordenacao precisa conter todos os status uma unica vez.");
        for (var index = 0; index < request.StatusIds.Count; index++)
            statuses.Single(x => x.Id == request.StatusIds[index]).Position = index;
        await _workflow.SaveAsync(ct);
    }
}

public sealed record ReplaceWorkflowTransitionsCommand(
    Guid ProjectId, IReadOnlyList<WorkflowTransitionDto> Transitions, string ActorId) : IRequest;
public sealed class ReplaceWorkflowTransitionsCommandHandler : IRequestHandler<ReplaceWorkflowTransitionsCommand>
{
    private readonly IWorkflowRepository _workflow;
    private readonly IProjectAccessService _access;
    public ReplaceWorkflowTransitionsCommandHandler(IWorkflowRepository workflow, IProjectAccessService access)
        => (_workflow, _access) = (workflow, access);

    public async Task Handle(ReplaceWorkflowTransitionsCommand request, CancellationToken ct)
    {
        await WorkflowInheritanceGuard.EnsureCustomAsync(_workflow, request.ProjectId, ct);
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, ct);
        var statusIds = (await _workflow.GetStatusesAsync(request.ProjectId, ct: ct)).Select(x => x.Id).ToHashSet();
        var pairs = request.Transitions.Distinct().ToList();
        DomainException.Garantir(pairs.All(x => x.SourceStatusId != x.TargetStatusId
            && statusIds.Contains(x.SourceStatusId) && statusIds.Contains(x.TargetStatusId)),
            "Toda transicao deve conectar dois status diferentes do projeto.");
        var current = await _workflow.GetTransitionsAsync(request.ProjectId, ct);
        _workflow.ReplaceTransitions(current, pairs.Select(x =>
            WorkflowTransition.Create(x.SourceStatusId, x.TargetStatusId)).ToList());
        await _workflow.SaveAsync(ct);
    }
}

public sealed record UpdateWorkflowStageCommand(
    Guid ProjectId, Guid StageId, string Name, double Position, int? WipLimit,
    Guid WorkflowStatusId, string ActorId) : IRequest;
public sealed class UpdateWorkflowStageCommandHandler : IRequestHandler<UpdateWorkflowStageCommand>
{
    private readonly IWorkflowRepository _workflow;
    private readonly IProjectAccessService _access;
    public UpdateWorkflowStageCommandHandler(IWorkflowRepository workflow, IProjectAccessService access)
        => (_workflow, _access) = (workflow, access);

    public async Task Handle(UpdateWorkflowStageCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, ct);
        var stage = await _workflow.GetStageAsync(request.StageId, ct)
            ?? throw new NaoEncontradoException("Coluna");
        var status = await _workflow.GetStatusAsync(request.WorkflowStatusId, ct)
            ?? throw new NaoEncontradoException("Status");
        DomainException.Garantir(stage.Board.ProjectId == request.ProjectId && status.ProjectId == request.ProjectId,
            "A coluna e o status precisam pertencer ao projeto.");
        DomainException.Garantir(!string.IsNullOrWhiteSpace(request.Name), "Nome da coluna obrigatorio.");
        DomainException.Garantir(request.Position >= 0, "Posicao da coluna invalida.");
        DomainException.Garantir(request.WipLimit is null or > 0, "O limite de WIP deve ser maior que zero.");

        var changedStatus = stage.WorkflowStatusId != status.Id;
        stage.Name = request.Name.Trim();
        stage.Position = request.Position;
        stage.WipLimit = request.WipLimit;
        stage.WorkflowStatusId = status.Id;
        stage.Category = status.Category;
        await _workflow.SaveAsync(ct);
        if (changedStatus)
            await _workflow.SynchronizeStageWorkItemsAsync(stage.Id, status.Id, ct);
    }
}

public sealed record DeleteWorkflowStageCommand(Guid ProjectId, Guid StageId, string ActorId) : IRequest;
public sealed class DeleteWorkflowStageCommandHandler : IRequestHandler<DeleteWorkflowStageCommand>
{
    private readonly IWorkflowRepository _workflow;
    private readonly IProjectAccessService _access;
    public DeleteWorkflowStageCommandHandler(IWorkflowRepository workflow, IProjectAccessService access)
        => (_workflow, _access) = (workflow, access);
    public async Task Handle(DeleteWorkflowStageCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, ct);
        var stage = await _workflow.GetStageAsync(request.StageId, ct)
            ?? throw new NaoEncontradoException("Coluna");
        DomainException.Garantir(stage.Board.ProjectId == request.ProjectId, "A coluna nao pertence ao projeto.");
        DomainException.Garantir(await _workflow.CountActiveItemsInStageAsync(stage.Id, ct: ct) == 0,
            "Mova ou arquive as tarefas antes de excluir a coluna.");
        _workflow.DeleteStage(stage);
        await _workflow.SaveAsync(ct);
    }
}

public sealed record UpdateBoardCardSettingsCommand(
    Guid ProjectId, Guid BoardId, string SettingsJson, string ActorId) : IRequest;
public sealed class UpdateBoardCardSettingsCommandHandler : IRequestHandler<UpdateBoardCardSettingsCommand>
{
    private readonly IWorkflowRepository _workflow;
    private readonly IProjectAccessService _access;
    public UpdateBoardCardSettingsCommandHandler(IWorkflowRepository workflow, IProjectAccessService access)
        => (_workflow, _access) = (workflow, access);
    public async Task Handle(UpdateBoardCardSettingsCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.Member, ct);
        var board = await _workflow.GetBoardAsync(request.BoardId, ct)
            ?? throw new NaoEncontradoException("Quadro");
        DomainException.Garantir(board.ProjectId == request.ProjectId, "O quadro nao pertence ao projeto.");
        DomainException.Garantir(request.SettingsJson.Length <= 8_000, "Configuracao dos cartoes muito extensa.");
        try { using var _ = JsonDocument.Parse(request.SettingsJson); }
        catch (JsonException) { throw new DomainException("Configuracao dos cartoes em formato invalido."); }
        board.CardSettingsJson = request.SettingsJson;
        await _workflow.SaveAsync(ct);
    }
}

internal static class WorkflowInheritanceGuard
{
    public static async Task EnsureCustomAsync(
        IWorkflowRepository workflow, Guid projectId, CancellationToken ct)
        => DomainException.Garantir(
            await workflow.GetProjectInheritanceModeAsync(projectId, ct) == WorkflowInheritanceMode.Custom,
            "O workflow deste projeto e herdado. Altere o template da organizacao ou converta o projeto para personalizado.");
}
