using Detran.Kanban.Application.Common.Exceptions;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Domain.Exceptions;
using MediatR;

namespace Detran.Kanban.Application.Features.Workflow;

public sealed record OrganizationWorkflowStatusInput(
    string Key, string Name, string Color, double Position, StageCategory Category,
    bool IsInitial, bool IsFinal, bool IsActive = true);
public sealed record OrganizationWorkflowTransitionInput(string SourceKey, string TargetKey);
public sealed record OrganizationWorkflowTemplateInput(
    string Name, bool IsDefault, IReadOnlyList<OrganizationWorkflowStatusInput> Statuses,
    IReadOnlyList<OrganizationWorkflowTransitionInput> Transitions, string? RowVersion = null);

public sealed record OrganizationWorkflowStatusDto(
    Guid Id, string Key, string Name, string Color, double Position, StageCategory Category,
    bool IsInitial, bool IsFinal, bool IsActive);
public sealed record OrganizationWorkflowTemplateDto(
    Guid Id, Guid OrganizationId, string Name, bool IsDefault, bool IsActive, long Version,
    string RowVersion, IReadOnlyList<OrganizationWorkflowStatusDto> Statuses,
    IReadOnlyList<OrganizationWorkflowTransitionInput> Transitions);

public sealed record GetOrganizationWorkflowTemplatesQuery(Guid OrganizationId, string ActorId)
    : IRequest<IReadOnlyList<OrganizationWorkflowTemplateDto>>;
public sealed record CreateOrganizationWorkflowTemplateCommand(
    Guid OrganizationId, OrganizationWorkflowTemplateInput Input, string ActorId)
    : IRequest<OrganizationWorkflowTemplateDto>;
public sealed record UpdateOrganizationWorkflowTemplateCommand(
    Guid OrganizationId, Guid TemplateId, OrganizationWorkflowTemplateInput Input, string ActorId)
    : IRequest<OrganizationWorkflowTemplateDto>;
public sealed record DisableOrganizationWorkflowTemplateCommand(
    Guid OrganizationId, Guid TemplateId, string ActorId) : IRequest;
public sealed record SetProjectWorkflowInheritanceCommand(
    Guid ProjectId, WorkflowInheritanceMode Mode, Guid? WorkflowTemplateId, string ActorId) : IRequest;

public sealed class GetOrganizationWorkflowTemplatesQueryHandler
    : IRequestHandler<GetOrganizationWorkflowTemplatesQuery, IReadOnlyList<OrganizationWorkflowTemplateDto>>
{
    private readonly IOrganizationWorkflowRepository _repository;
    private readonly IPermissionService _permissions;
    public GetOrganizationWorkflowTemplatesQueryHandler(
        IOrganizationWorkflowRepository repository, IPermissionService permissions)
        => (_repository, _permissions) = (repository, permissions);

    public async Task<IReadOnlyList<OrganizationWorkflowTemplateDto>> Handle(
        GetOrganizationWorkflowTemplatesQuery request, CancellationToken ct)
    {
        await _permissions.EnsureAsync(request.ActorId, PlatformPermission.View,
            PermissionScope.Organization, request.OrganizationId, ct);
        return (await _repository.GetTemplatesAsync(request.OrganizationId, ct))
            .Select(WorkflowTemplateProjection.Map).ToList();
    }
}

public sealed class CreateOrganizationWorkflowTemplateCommandHandler
    : IRequestHandler<CreateOrganizationWorkflowTemplateCommand, OrganizationWorkflowTemplateDto>
{
    private readonly IOrganizationWorkflowRepository _repository;
    private readonly IPermissionService _permissions;
    public CreateOrganizationWorkflowTemplateCommandHandler(
        IOrganizationWorkflowRepository repository, IPermissionService permissions)
        => (_repository, _permissions) = (repository, permissions);

    public async Task<OrganizationWorkflowTemplateDto> Handle(
        CreateOrganizationWorkflowTemplateCommand request, CancellationToken ct)
    {
        await WorkflowTemplateProjection.EnsureAdministratorAsync(
            _permissions, request.ActorId, request.OrganizationId, ct);
        WorkflowTemplateProjection.Validate(request.Input);
        var template = OrganizationWorkflowTemplate.Create(
            request.OrganizationId, request.Input.Name, request.Input.IsDefault);
        WorkflowTemplateProjection.ReplaceDefinition(template, request.Input, _repository);
        if (template.IsDefault)
            foreach (var other in await _repository.GetOtherDefaultsAsync(request.OrganizationId, null, ct))
                other.IsDefault = false;
        _repository.Add(template);
        await _repository.SaveAsync(ct);
        return WorkflowTemplateProjection.Map(template);
    }
}

public sealed class UpdateOrganizationWorkflowTemplateCommandHandler
    : IRequestHandler<UpdateOrganizationWorkflowTemplateCommand, OrganizationWorkflowTemplateDto>
{
    private readonly IOrganizationWorkflowRepository _repository;
    private readonly IPermissionService _permissions;
    public UpdateOrganizationWorkflowTemplateCommandHandler(
        IOrganizationWorkflowRepository repository, IPermissionService permissions)
        => (_repository, _permissions) = (repository, permissions);

    public async Task<OrganizationWorkflowTemplateDto> Handle(
        UpdateOrganizationWorkflowTemplateCommand request, CancellationToken ct)
    {
        await WorkflowTemplateProjection.EnsureAdministratorAsync(
            _permissions, request.ActorId, request.OrganizationId, ct);
        WorkflowTemplateProjection.Validate(request.Input);
        var template = await _repository.GetTemplateAsync(
            request.OrganizationId, request.TemplateId, true, ct)
            ?? throw new NaoEncontradoException("Template de workflow");
        WorkflowTemplateProjection.EnsureVersion(template, request.Input.RowVersion);
        template.Update(request.Input.Name, request.Input.IsDefault);
        WorkflowTemplateProjection.ReplaceDefinition(template, request.Input, _repository);
        if (template.IsDefault)
            foreach (var other in await _repository.GetOtherDefaultsAsync(request.OrganizationId, template.Id, ct))
                other.IsDefault = false;
        foreach (var project in template.Projects)
            WorkflowTemplateProjection.SynchronizeProject(project, template, _repository, attachCustom: false);
        await _repository.SaveAsync(ct);
        return WorkflowTemplateProjection.Map(template);
    }
}

public sealed class DisableOrganizationWorkflowTemplateCommandHandler
    : IRequestHandler<DisableOrganizationWorkflowTemplateCommand>
{
    private readonly IOrganizationWorkflowRepository _repository;
    private readonly IPermissionService _permissions;
    public DisableOrganizationWorkflowTemplateCommandHandler(
        IOrganizationWorkflowRepository repository, IPermissionService permissions)
        => (_repository, _permissions) = (repository, permissions);

    public async Task Handle(DisableOrganizationWorkflowTemplateCommand request, CancellationToken ct)
    {
        await WorkflowTemplateProjection.EnsureAdministratorAsync(
            _permissions, request.ActorId, request.OrganizationId, ct);
        var template = await _repository.GetTemplateAsync(
            request.OrganizationId, request.TemplateId, true, ct)
            ?? throw new NaoEncontradoException("Template de workflow");
        DomainException.Garantir(template.Projects.Count == 0,
            "O template possui projetos herdados e nao pode ser desativado.");
        template.IsActive = false;
        template.IsDefault = false;
        template.Version++;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.SaveAsync(ct);
    }
}

public sealed class SetProjectWorkflowInheritanceCommandHandler
    : IRequestHandler<SetProjectWorkflowInheritanceCommand>
{
    private readonly IOrganizationWorkflowRepository _repository;
    private readonly IProjectAccessService _access;
    public SetProjectWorkflowInheritanceCommandHandler(
        IOrganizationWorkflowRepository repository, IProjectAccessService access)
        => (_repository, _access) = (repository, access);

    public async Task Handle(SetProjectWorkflowInheritanceCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, ct);
        var project = await _repository.GetProjectGraphAsync(request.ProjectId, ct)
            ?? throw new NaoEncontradoException("Projeto");
        if (request.Mode == WorkflowInheritanceMode.Custom)
        {
            project.WorkflowInheritanceMode = WorkflowInheritanceMode.Custom;
            project.WorkflowTemplateId = null;
            project.WorkflowTemplateVersion = null;
            project.WorkflowTemplate = null;
            foreach (var status in project.WorkflowStatuses)
                status.OrganizationWorkflowStatusId = null;
        }
        else
        {
            DomainException.Garantir(request.WorkflowTemplateId.HasValue, "Selecione um template.");
            var template = await _repository.GetTemplateAsync(
                project.OrganizationId, request.WorkflowTemplateId.Value, true, ct)
                ?? throw new NaoEncontradoException("Template de workflow");
            DomainException.Garantir(template.IsActive, "O template selecionado esta inativo.");
            project.WorkflowInheritanceMode = WorkflowInheritanceMode.Inherited;
            project.WorkflowTemplateId = template.Id;
            project.WorkflowTemplate = template;
            WorkflowTemplateProjection.SynchronizeProject(project, template, _repository, attachCustom: true);
        }
        project.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.SaveAsync(ct);
    }
}

public static class WorkflowTemplateProjection
{
    public static Task EnsureAdministratorAsync(
        IPermissionService permissions, string actorId, Guid organizationId, CancellationToken ct)
        => permissions.EnsureAsync(actorId, PlatformPermission.AdministerOrganization,
            PermissionScope.Organization, organizationId, ct);

    public static void Validate(OrganizationWorkflowTemplateInput input)
    {
        DomainException.Garantir(!string.IsNullOrWhiteSpace(input.Name), "Nome do template obrigatorio.");
        DomainException.Garantir(input.Statuses.Count > 0, "O template precisa de status.");
        var active = input.Statuses.Where(x => x.IsActive).ToList();
        DomainException.Garantir(active.Count > 0, "O template precisa de status ativo.");
        DomainException.Garantir(active.Count(x => x.IsInitial) == 1,
            "O template precisa de exatamente um status inicial ativo.");
        DomainException.Garantir(input.Statuses.Select(x => x.Key.Trim().ToUpperInvariant()).Distinct().Count()
            == input.Statuses.Count, "As chaves de status devem ser unicas.");
        var keys = active.Select(x => x.Key.Trim().ToUpperInvariant()).ToHashSet();
        foreach (var transition in input.Transitions)
        {
            var source = transition.SourceKey.Trim().ToUpperInvariant();
            var target = transition.TargetKey.Trim().ToUpperInvariant();
            DomainException.Garantir(source != target, "Uma transicao nao pode apontar para o mesmo status.");
            DomainException.Garantir(keys.Contains(source) && keys.Contains(target),
                "Toda transicao deve usar status ativos do template.");
        }
    }

    public static void EnsureVersion(OrganizationWorkflowTemplate template, string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded) || template.RowVersion.Length == 0) return;
        DomainException.Garantir(Convert.FromBase64String(encoded).SequenceEqual(template.RowVersion),
            "O template foi alterado por outro usuario. Recarregue e tente novamente.");
    }

    public static void ReplaceDefinition(
        OrganizationWorkflowTemplate template, OrganizationWorkflowTemplateInput input,
        IOrganizationWorkflowRepository repository)
    {
        var existing = template.Statuses.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        var requestedKeys = input.Statuses.Select(x => x.Key.Trim().ToUpperInvariant()).ToHashSet();
        foreach (var definition in input.Statuses)
        {
            var key = definition.Key.Trim().ToUpperInvariant();
            var validated = WorkflowStatus.Create(Guid.NewGuid(), definition.Name, definition.Color,
                definition.Position, definition.Category, definition.IsInitial, definition.IsFinal);
            if (!existing.TryGetValue(key, out var status))
            {
                status = OrganizationWorkflowStatus.Create(template.Id, key, validated.Name,
                    validated.Color, validated.Position, definition.Category,
                    definition.IsInitial, definition.IsFinal, definition.IsActive);
                template.Statuses.Add(status);
                existing[key] = status;
            }
            else
            {
                status.Name = validated.Name; status.Color = validated.Color;
                status.Position = validated.Position; status.Category = definition.Category;
                status.IsInitial = definition.IsInitial; status.IsFinal = definition.IsFinal;
                status.IsActive = definition.IsActive;
            }
        }
        foreach (var removed in template.Statuses.Where(x => !requestedKeys.Contains(x.Key)))
            removed.IsActive = false;

        repository.RemoveTransitions(template.Transitions.ToList());
        template.Transitions.Clear();
        foreach (var transition in input.Transitions.Distinct())
        {
            var source = existing[transition.SourceKey.Trim().ToUpperInvariant()];
            var target = existing[transition.TargetKey.Trim().ToUpperInvariant()];
            var entity = OrganizationWorkflowTransition.Create(template.Id, source.Id, target.Id);
            template.Transitions.Add(entity);
        }
    }

    public static void ApplyToNewProject(Project project, OrganizationWorkflowTemplate template)
    {
        project.OrganizationId = template.OrganizationId;
        project.WorkflowInheritanceMode = WorkflowInheritanceMode.Inherited;
        project.WorkflowTemplateId = template.Id;
        project.WorkflowTemplateVersion = template.Version;
        var local = new Dictionary<Guid, WorkflowStatus>();
        foreach (var definition in template.Statuses.OrderBy(x => x.Position))
        {
            var status = WorkflowStatus.Create(project.Id, definition.Name, definition.Color,
                definition.Position, definition.Category, definition.IsInitial, definition.IsFinal);
            status.OrganizationWorkflowStatusId = definition.Id;
            status.IsActive = definition.IsActive;
            project.WorkflowStatuses.Add(status);
            local[definition.Id] = status;
        }
        foreach (var transition in template.Transitions)
            local[transition.SourceStatusId].OutgoingTransitions.Add(
                WorkflowTransition.Create(local[transition.SourceStatusId].Id, local[transition.TargetStatusId].Id));
    }

    public static void SynchronizeProject(
        Project project, OrganizationWorkflowTemplate template,
        IOrganizationWorkflowRepository repository, bool attachCustom)
    {
        var bySource = project.WorkflowStatuses
            .Where(x => x.OrganizationWorkflowStatusId.HasValue)
            .ToDictionary(x => x.OrganizationWorkflowStatusId!.Value);
        var unbound = project.WorkflowStatuses.Where(x => !x.OrganizationWorkflowStatusId.HasValue).ToList();
        foreach (var definition in template.Statuses.OrderBy(x => x.Position))
        {
            if (!bySource.TryGetValue(definition.Id, out var local))
            {
                local = attachCustom
                    ? unbound.FirstOrDefault(x => x.Name.Equals(definition.Name, StringComparison.OrdinalIgnoreCase)
                        && x.Category == definition.Category)
                    : null;
                if (local is null)
                {
                    local = WorkflowStatus.Create(project.Id, definition.Name, definition.Color,
                        definition.Position, definition.Category, definition.IsInitial, definition.IsFinal);
                    project.WorkflowStatuses.Add(local);
                    repository.AddProjectStatus(local);
                }
                else unbound.Remove(local);
                local.OrganizationWorkflowStatusId = definition.Id;
                bySource[definition.Id] = local;
            }
            local.Update(definition.Name, definition.Color, definition.Position,
                definition.Category, definition.IsInitial, definition.IsFinal);
            local.IsActive = definition.IsActive;
        }
        foreach (var obsolete in bySource.Where(x => template.Statuses.All(s => s.Id != x.Key)).Select(x => x.Value))
            obsolete.IsActive = false;
        if (attachCustom)
            foreach (var legacy in unbound) legacy.IsActive = false;

        var current = project.WorkflowStatuses.SelectMany(x => x.OutgoingTransitions).ToList();
        var desired = template.Transitions
            .Where(x => bySource.ContainsKey(x.SourceStatusId) && bySource.ContainsKey(x.TargetStatusId))
            .Select(x => (SourceStatusId: bySource[x.SourceStatusId].Id,
                TargetStatusId: bySource[x.TargetStatusId].Id))
            .ToList();
        var desiredKeys = desired.ToHashSet();
        var currentKeys = current.Select(x => (x.SourceStatusId, x.TargetStatusId)).ToHashSet();

        // Preserva transicoes que ja representam o template. Remover e inserir a mesma
        // chave composta no mesmo SaveChanges fazia o EF emitir uma exclusao concorrente
        // para um registro que tambem estava sendo recriado, resultando em 409 permanente.
        repository.RemoveProjectTransitions(current.Where(x =>
            !desiredKeys.Contains((x.SourceStatusId, x.TargetStatusId))));
        repository.AddProjectTransitions(desired
            .Where(x => !currentKeys.Contains(x))
            .Select(x => WorkflowTransition.Create(x.SourceStatusId, x.TargetStatusId)));
        project.WorkflowTemplateVersion = template.Version;
    }

    public static OrganizationWorkflowTemplateDto Map(OrganizationWorkflowTemplate template)
    {
        var byId = template.Statuses.ToDictionary(x => x.Id);
        return new OrganizationWorkflowTemplateDto(
            template.Id, template.OrganizationId, template.Name, template.IsDefault, template.IsActive,
            template.Version, template.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(template.RowVersion),
            template.Statuses.OrderBy(x => x.Position).Select(x => new OrganizationWorkflowStatusDto(
                x.Id, x.Key, x.Name, x.Color, x.Position, x.Category, x.IsInitial, x.IsFinal, x.IsActive)).ToList(),
            template.Transitions.Where(x => byId.ContainsKey(x.SourceStatusId) && byId.ContainsKey(x.TargetStatusId))
                .Select(x => new OrganizationWorkflowTransitionInput(byId[x.SourceStatusId].Key, byId[x.TargetStatusId].Key)).ToList());
    }
}
