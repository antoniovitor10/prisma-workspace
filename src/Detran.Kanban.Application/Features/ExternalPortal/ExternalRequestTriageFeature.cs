using Detran.Kanban.Application.Common.Exceptions;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Application.Features.Sla;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Detran.Kanban.Application.Features.ExternalPortal;

public record ApplyExternalRequestTriageCommand(
    string Protocol,
    ExternalRequestTriageAction Action,
    string? Reason,
    string? Message,
    string? Category,
    Priority? Priority,
    string? ResponsibleId,
    Guid? TeamId,
    Guid? ProjectId,
    Guid? BoardId,
    Guid? StageId,
    Guid? RelatedWorkItemId,
    long? RelatedWorkItemNumber,
    string ActorId,
    string ActorName) : IRequest<ExternalRequestDto>;

public class ApplyExternalRequestTriageCommandValidator
    : AbstractValidator<ApplyExternalRequestTriageCommand>
{
    public ApplyExternalRequestTriageCommandValidator()
    {
        RuleFor(x => x.Protocol).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Reason).MaximumLength(1000);
        RuleFor(x => x.Message).MaximumLength(4000);
        RuleFor(x => x.Category).MaximumLength(120);
    }
}

public class ApplyExternalRequestTriageCommandHandler
    : IRequestHandler<ApplyExternalRequestTriageCommand, ExternalRequestDto>
{
    private readonly IExternalPortalRepository _portals;
    private readonly IProjectRepository _projects;
    private readonly IProjectAccessService _access;
    private readonly IUserDirectory _users;
    private readonly IPortalEmailSender _emailSender;

    public ApplyExternalRequestTriageCommandHandler(
        IExternalPortalRepository portals,
        IProjectRepository projects,
        IProjectAccessService access,
        IUserDirectory users,
        IPortalEmailSender emailSender)
        => (_portals, _projects, _access, _users, _emailSender)
            = (portals, projects, access, users, emailSender);

    public async Task<ExternalRequestDto> Handle(
        ApplyExternalRequestTriageCommand request, CancellationToken ct)
    {
        DomainException.Garantir(request.Action != ExternalRequestTriageAction.Submitted,
            "A ação de envio é registrada automaticamente.");
        var externalRequest = await _portals.GetRequestByProtocolAsync(request.Protocol.Trim(), ct)
            ?? throw new NaoEncontradoException("Solicitação");
        var item = externalRequest.WorkItem;
        var currentProjectId = item.Board.ProjectId!.Value;
        await _access.EnsureAtLeastAsync(currentProjectId, request.ActorId, ProjectRole.Member, ct);
        var currentProject = await _projects.GetByIdWithMembersAsync(currentProjectId, ct)
            ?? throw new NaoEncontradoException("Projeto");

        var description = await ApplyActionAsync(
            request, externalRequest, item, currentProject, ct);
        var now = DateTimeOffset.UtcNow;
        externalRequest.UpdatedAt = now;
        item.UpdatedAt = now;
        var eventData = ExternalFormSerialization.Serialize(new
        {
            request.Reason,
            request.Message,
            request.Category,
            request.Priority,
            request.ResponsibleId,
            request.TeamId,
            request.ProjectId,
            request.BoardId,
            request.StageId,
            request.RelatedWorkItemId,
            request.RelatedWorkItemNumber
        });
        var triageEvent = new ExternalRequestTriageEvent
        {
            Id = Guid.NewGuid(), ExternalRequestId = externalRequest.Id,
            Action = request.Action, ActorId = request.ActorId,
            ActorName = string.IsNullOrWhiteSpace(request.ActorName) ? "Atendente" : request.ActorName.Trim(),
            Description = description, DataJson = eventData, CreatedAt = now
        };
        _portals.AddTriageEvent(triageEvent);
        _portals.AddTaskEvent(TaskEvent.Registrar(
            item.Id, request.ActorId, $"external_request_{request.Action.ToString().ToLowerInvariant()}", eventData));
        await _portals.SaveAsync(ct);

        if (request.Action == ExternalRequestTriageAction.InformationRequested
            && !string.IsNullOrWhiteSpace(request.Message))
        {
            var trackingPath = $"/portal/{externalRequest.ExternalPortal.PublicSlug}/acompanhar?protocol={Uri.EscapeDataString(externalRequest.Protocol)}";
            try
            {
                await _emailSender.SendInformationRequestedAsync(
                    externalRequest.RequesterEmail, externalRequest.ExternalPortal.Project.Name,
                    externalRequest.Protocol, request.Message, trackingPath, ct);
            }
            catch
            {
                // A auditoria e a mensagem pública já foram persistidas; SMTP não desfaz a ação.
            }
        }

        return ExternalPortalMapper.MapInternal(externalRequest);
    }

    private async Task<string> ApplyActionAsync(
        ApplyExternalRequestTriageCommand request,
        ExternalRequest externalRequest,
        WorkItem item,
        Project currentProject,
        CancellationToken ct)
    {
        switch (request.Action)
        {
            case ExternalRequestTriageAction.Accepted:
                externalRequest.TriageStatus = ExternalRequestTriageStatus.Accepted;
                if (item.IsArchived) item.Reactivate();
                return "Solicitação aceita na triagem.";

            case ExternalRequestTriageAction.Rejected:
                DomainException.Garantir(!string.IsNullOrWhiteSpace(request.Reason),
                    "Informe a justificativa da recusa.");
                externalRequest.TriageStatus = ExternalRequestTriageStatus.Rejected;
                item.Archive();
                return $"Solicitação recusada: {request.Reason!.Trim()}";

            case ExternalRequestTriageAction.InformationRequested:
                DomainException.Garantir(!string.IsNullOrWhiteSpace(request.Message),
                    "Escreva quais informações são necessárias.");
                externalRequest.TriageStatus = ExternalRequestTriageStatus.WaitingForInformation;
                var publicMessage = AddExternalRequestReplyCommandHandler.NewMessage(
                    externalRequest, ExternalRequestMessageAuthor.Agent,
                    request.ActorName, request.ActorId, request.Message!);
                _portals.AddMessage(publicMessage);
                SlaCalculator.MarkFirstResponse(externalRequest, publicMessage.CreatedAt);
                SlaCalculator.Pause(externalRequest, publicMessage.CreatedAt);
                return "Mais informações foram solicitadas ao solicitante.";

            case ExternalRequestTriageAction.CategoryChanged:
                DomainException.Garantir(!string.IsNullOrWhiteSpace(request.Category),
                    "Informe a nova categoria.");
                externalRequest.Category = request.Category!.Trim();
                return $"Categoria alterada para {externalRequest.Category}.";

            case ExternalRequestTriageAction.PriorityChanged:
                DomainException.Garantir(request.Priority.HasValue, "Informe a nova prioridade.");
                item.Priority = request.Priority!.Value;
                return $"Prioridade alterada para {item.Priority}.";

            case ExternalRequestTriageAction.ResponsibleChanged:
                if (!string.IsNullOrWhiteSpace(request.ResponsibleId))
                {
                    DomainException.Garantir(currentProject.Members.Any(x => x.UserId == request.ResponsibleId)
                        || currentProject.OwnerId == request.ResponsibleId,
                        "O responsável precisa ser membro do projeto.");
                    DomainException.Garantir(await _users.GetByIdAsync(request.ResponsibleId, ct) is not null,
                        "Responsável não encontrado.");
                }
                item.ResponsibleId = string.IsNullOrWhiteSpace(request.ResponsibleId)
                    ? null : request.ResponsibleId;
                return item.ResponsibleId is null
                    ? "Responsável removido." : "Responsável definido na triagem.";

            case ExternalRequestTriageAction.TeamChanged:
                DomainException.Garantir(!request.TeamId.HasValue
                    || currentProject.Teams.Any(x => x.TeamId == request.TeamId),
                    "A equipe não pertence ao projeto.");
                item.TeamId = request.TeamId;
                return item.TeamId.HasValue ? "Equipe responsável alterada." : "Equipe responsável removida.";

            case ExternalRequestTriageAction.ProjectChanged:
                return await MoveToProjectAsync(request, externalRequest, item, ct);

            case ExternalRequestTriageAction.SentToBacklog:
                item.StageId = null;
                item.Stage = null;
                item.SprintId = null;
                item.Sprint = null;
                var initial = currentProject.WorkflowStatuses.OrderBy(x => x.Position)
                    .FirstOrDefault(x => x.IsInitial);
                item.WorkflowStatusId = initial?.Id;
                item.WorkflowStatus = initial;
                externalRequest.TriageStatus = ExternalRequestTriageStatus.Routed;
                return "Solicitação enviada ao Product Backlog.";

            case ExternalRequestTriageAction.SentToKanban:
                var stage = request.StageId.HasValue
                    ? item.Board.Stages.FirstOrDefault(x => x.Id == request.StageId)
                    : item.Board.Stages.OrderBy(x => x.Position).FirstOrDefault();
                DomainException.Garantir(stage is not null, "Selecione uma coluna válida do Kanban.");
                item.StageId = stage!.Id;
                item.Stage = stage;
                item.WorkflowStatusId = stage.WorkflowStatusId;
                item.WorkflowStatus = currentProject.WorkflowStatuses.FirstOrDefault(x => x.Id == stage.WorkflowStatusId);
                externalRequest.TriageStatus = ExternalRequestTriageStatus.Routed;
                return $"Solicitação enviada ao Kanban na coluna {stage.Name}.";

            case ExternalRequestTriageAction.MarkedDuplicate:
                await AddLinkAsync(request, item, WorkItemLinkType.Duplicate, ct);
                externalRequest.TriageStatus = ExternalRequestTriageStatus.Duplicate;
                return "Solicitação marcada como duplicada e vinculada à tarefa informada.";

            case ExternalRequestTriageAction.LinkedWorkItem:
                await AddLinkAsync(request, item, WorkItemLinkType.Related, ct);
                return "Solicitação vinculada a outra tarefa.";

            default:
                throw new DomainException("Ação de triagem inválida.");
        }
    }

    private async Task<string> MoveToProjectAsync(
        ApplyExternalRequestTriageCommand request,
        ExternalRequest externalRequest,
        WorkItem item,
        CancellationToken ct)
    {
        DomainException.Garantir(request.ProjectId.HasValue && request.BoardId.HasValue,
            "Informe o projeto e o quadro de destino.");
        await _access.EnsureAtLeastAsync(request.ProjectId!.Value, request.ActorId, ProjectRole.Member, ct);
        var project = await _projects.GetByIdWithMembersAsync(request.ProjectId.Value, ct)
            ?? throw new NaoEncontradoException("Projeto de destino");
        DomainException.Garantir(!project.IsArchived, "O projeto de destino está arquivado.");
        var board = project.Boards.FirstOrDefault(x => x.Id == request.BoardId)
            ?? throw new DomainException("O quadro de destino não pertence ao projeto.");
        var initialStatus = project.WorkflowStatuses.OrderBy(x => x.Position)
            .FirstOrDefault(x => x.IsInitial);

        item.BoardId = board.Id;
        item.Board = board;
        item.StageId = null;
        item.Stage = null;
        item.SprintId = null;
        item.Sprint = null;
        item.TeamId = board.TeamId;
        item.WorkflowStatusId = initialStatus?.Id;
        item.WorkflowStatus = initialStatus;
        item.ResponsibleId = null;
        externalRequest.TriageStatus = ExternalRequestTriageStatus.Routed;
        return $"Solicitação relacionada ao projeto {project.Key} — {project.Name}.";
    }

    private async Task AddLinkAsync(
        ApplyExternalRequestTriageCommand request,
        WorkItem source,
        WorkItemLinkType type,
        CancellationToken ct)
    {
        DomainException.Garantir(request.RelatedWorkItemId.HasValue || request.RelatedWorkItemNumber.HasValue,
            "Informe a tarefa que será vinculada.");
        var target = request.RelatedWorkItemId.HasValue
            ? await _portals.GetWorkItemAsync(request.RelatedWorkItemId.Value, ct)
            : await _portals.GetWorkItemByNumberAsync(request.RelatedWorkItemNumber!.Value, ct);
        target = target
            ?? throw new NaoEncontradoException("Tarefa relacionada");
        await _access.EnsureAtLeastAsync(target.Board.ProjectId!.Value,
            request.ActorId, ProjectRole.Viewer, ct);
        DomainException.Garantir(!await _portals.WorkItemLinkExistsAsync(
                source.Id, target.Id, type, ct),
            "Este vínculo já existe.");
        _portals.AddWorkItemLink(WorkItemLink.Create(source.Id, target.Id, type, request.ActorId));
    }
}
