using System.Text.Json;
using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Prisma.Workspace.Application.Features.Projects;

public record UpdateProjectCommand(
    Guid ProjectId,
    string Name,
    string? Description,
    string OwnerId,
    DateOnly? StartDate,
    DateOnly? DueDate,
    ProjectStatus Status,
    ProjectMethodology Methodology,
    WorkNature Nature,
    WorkType WorkType,
    string? SettingsJson,
    IReadOnlyList<Guid> TagIds,
    string ActorId) : IRequest;

public class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.OwnerId).NotEmpty().MaximumLength(450);
        RuleFor(x => x.Methodology).IsInEnum();
        RuleFor(x => x.Nature).IsInEnum().NotEqual(WorkNature.Unclassified);
        RuleFor(x => x.WorkType).IsInEnum().NotEqual(WorkType.Unclassified);
        RuleFor(x => x.SettingsJson)
            .Must(BeValidJson).When(x => !string.IsNullOrWhiteSpace(x.SettingsJson))
            .WithMessage("As configuracoes precisam ser um JSON valido.");
        RuleFor(x => x)
            .Must(x => !x.StartDate.HasValue || !x.DueDate.HasValue || x.DueDate >= x.StartDate)
            .WithMessage("O prazo nao pode ser anterior a data de inicio.");
    }

    private static bool BeValidJson(string? value)
    {
        try { JsonDocument.Parse(value!); return true; }
        catch (JsonException) { return false; }
    }
}

public class UpdateProjectCommandHandler : IRequestHandler<UpdateProjectCommand>
{
    private readonly IProjectRepository _projects;
    private readonly IProjectAccessService _access;
    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationContext _organizationContext;
    private readonly ITagRepository _tags;

    public UpdateProjectCommandHandler(
        IProjectRepository projects,
        IProjectAccessService access,
        IOrganizationRepository organizations,
        IOrganizationContext organizationContext,
        ITagRepository tags)
        => (_projects, _access, _organizations, _organizationContext, _tags)
            = (projects, access, organizations, organizationContext, tags);

    public async Task Handle(UpdateProjectCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, ct);
        var project = await _projects.GetByIdWithMembersAsync(request.ProjectId, ct)
            ?? throw new NaoEncontradoException("Projeto");
        var owner = await _organizations.GetMemberAsync(
            _organizationContext.RequireOrganizationId(), request.OwnerId, ct);
        DomainException.Garantir(owner?.IsActive == true,
            "O responsavel precisa ser membro ativo da organizacao.");

        var availableTagIds = (await _tags.GetAllAsync(ct)).Select(x => x.Id).ToHashSet();
        DomainException.Garantir(request.TagIds.All(availableTagIds.Contains),
            "Uma ou mais etiquetas nao pertencem a organizacao atual.");

        var previous = new
        {
            project.Name,
            project.OwnerId,
            project.Status,
            project.Methodology,
            project.Nature,
            project.WorkType,
            project.StartDate,
            project.DueDate
        };

        project.Update(
            request.Name, request.Description, request.OwnerId,
            request.StartDate, request.DueDate, request.Status,
            request.Methodology, request.Nature, request.WorkType, request.SettingsJson);

        var ownerLink = project.Members.FirstOrDefault(x => x.UserId == request.OwnerId);
        if (ownerLink is null)
            project.Members.Add(new ProjectMember
            {
                ProjectId = project.Id,
                UserId = request.OwnerId,
                Role = ProjectRole.ProjectAdmin,
                JoinedAt = DateTimeOffset.UtcNow
            });
        else ownerLink.Role = ProjectRole.ProjectAdmin;

        project.Tags.Clear();
        foreach (var tagId in request.TagIds.Distinct())
            project.Tags.Add(new ProjectTag { ProjectId = project.Id, TagId = tagId });

        _projects.AddEvent(ProjectEvent.Register(
            project.Id, request.ActorId, "updated",
            JsonSerializer.Serialize(new { previous, current = new
            {
                project.Name,
                project.OwnerId,
                project.Status,
                project.Methodology,
                project.Nature,
                project.WorkType,
                project.StartDate,
                project.DueDate
            }})));
        await _projects.SaveAsync(ct);
    }
}

public record SetProjectArchivedCommand(Guid ProjectId, bool Archived, string ActorId) : IRequest;

public class SetProjectArchivedCommandHandler : IRequestHandler<SetProjectArchivedCommand>
{
    private readonly IProjectRepository _projects;
    private readonly IProjectAccessService _access;

    public SetProjectArchivedCommandHandler(IProjectRepository projects, IProjectAccessService access)
        => (_projects, _access) = (projects, access);

    public async Task Handle(SetProjectArchivedCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, ct);
        var project = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NaoEncontradoException("Projeto");
        if (request.Archived) project.Archive(); else project.Reactivate();
        _projects.AddEvent(ProjectEvent.Register(
            project.Id, request.ActorId, request.Archived ? "archived" : "reactivated"));
        await _projects.SaveAsync(ct);
    }
}

public record RemoveProjectMemberCommand(Guid ProjectId, string UserId, string ActorId) : IRequest;

public class RemoveProjectMemberCommandHandler : IRequestHandler<RemoveProjectMemberCommand>
{
    private readonly IProjectRepository _projects;
    private readonly IProjectAccessService _access;

    public RemoveProjectMemberCommandHandler(IProjectRepository projects, IProjectAccessService access)
        => (_projects, _access) = (projects, access);

    public async Task Handle(RemoveProjectMemberCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, ct);
        var project = await _projects.GetByIdWithMembersAsync(request.ProjectId, ct)
            ?? throw new NaoEncontradoException("Projeto");
        DomainException.Garantir(project.OwnerId != request.UserId,
            "O responsavel principal nao pode ser removido do projeto.");
        var member = project.Members.FirstOrDefault(x => x.UserId == request.UserId)
            ?? throw new NaoEncontradoException("Membro do projeto");
        project.Members.Remove(member);
        _projects.AddEvent(ProjectEvent.Register(
            project.Id, request.ActorId, "member_removed",
            JsonSerializer.Serialize(new { request.UserId })));
        await _projects.SaveAsync(ct);
    }
}

public record UpsertProjectCustomFieldCommand(
    Guid ProjectId,
    Guid? FieldId,
    string Name,
    CustomFieldType Type,
    bool IsRequired,
    string? OptionsJson,
    double Position,
    string ActorId) : IRequest<Guid>;

public class UpsertProjectCustomFieldCommandValidator : AbstractValidator<UpsertProjectCustomFieldCommand>
{
    public UpsertProjectCustomFieldCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Position).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OptionsJson)
            .Must(BeValidJson).When(x => !string.IsNullOrWhiteSpace(x.OptionsJson))
            .WithMessage("As opcoes precisam ser um JSON valido.");
        RuleFor(x => x.OptionsJson)
            .NotEmpty().When(x => x.Type is CustomFieldType.SingleSelect or CustomFieldType.MultiSelect)
            .WithMessage("Campos de selecao precisam informar opcoes.");
        RuleFor(x => x.OptionsJson)
            .Must(HaveOptions).When(x => x.Type is CustomFieldType.SingleSelect or CustomFieldType.MultiSelect)
            .WithMessage("Campos de seleção precisam informar uma lista JSON de opções únicas.");
    }

    private static bool BeValidJson(string? value)
    {
        try { JsonDocument.Parse(value!); return true; }
        catch (JsonException) { return false; }
    }

    private static bool HaveOptions(string? value)
    {
        var options = CustomFieldValueRules.ParseOptions(value);
        return options.Count > 0;
    }
}

public class UpsertProjectCustomFieldCommandHandler : IRequestHandler<UpsertProjectCustomFieldCommand, Guid>
{
    private readonly IProjectRepository _projects;
    private readonly IProjectAccessService _access;

    public UpsertProjectCustomFieldCommandHandler(IProjectRepository projects, IProjectAccessService access)
        => (_projects, _access) = (projects, access);

    public async Task<Guid> Handle(UpsertProjectCustomFieldCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, ct);
        var project = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NaoEncontradoException("Projeto");
        var field = request.FieldId.HasValue
            ? project.CustomFields.FirstOrDefault(x => x.Id == request.FieldId)
                ?? throw new NaoEncontradoException("Campo personalizado")
            : ProjectCustomFieldDefinition.Create(
                project.Id, request.Name, request.Type, request.IsRequired,
                request.OptionsJson, request.Position);

        if (!request.FieldId.HasValue)
            _projects.AddCustomField(field);
        else
        {
            field.Name = request.Name.Trim();
            field.Type = request.Type;
            field.IsRequired = request.IsRequired;
            field.OptionsJson = string.IsNullOrWhiteSpace(request.OptionsJson) ? null : request.OptionsJson;
            field.Position = request.Position;
            field.IsActive = true;
        }

        _projects.AddEvent(ProjectEvent.Register(
            project.Id, request.ActorId, "custom_field_saved",
            JsonSerializer.Serialize(new { field.Id, field.Name, field.Type })));
        await _projects.SaveAsync(ct);
        return field.Id;
    }
}

public record DisableProjectCustomFieldCommand(Guid ProjectId, Guid FieldId, string ActorId) : IRequest;

public class DisableProjectCustomFieldCommandHandler : IRequestHandler<DisableProjectCustomFieldCommand>
{
    private readonly IProjectRepository _projects;
    private readonly IProjectAccessService _access;

    public DisableProjectCustomFieldCommandHandler(IProjectRepository projects, IProjectAccessService access)
        => (_projects, _access) = (projects, access);

    public async Task Handle(DisableProjectCustomFieldCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, ct);
        var project = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NaoEncontradoException("Projeto");
        var field = project.CustomFields.FirstOrDefault(x => x.Id == request.FieldId)
            ?? throw new NaoEncontradoException("Campo personalizado");
        field.IsActive = false;
        _projects.AddEvent(ProjectEvent.Register(
            project.Id, request.ActorId, "custom_field_disabled",
            JsonSerializer.Serialize(new { field.Id, field.Name })));
        await _projects.SaveAsync(ct);
    }
}

public record ProjectEventDto(
    Guid Id, string ActorId, string Kind, string? Payload, DateTimeOffset CreatedAt);

public record GetProjectHistoryQuery(Guid ProjectId, string UserId) : IRequest<IReadOnlyList<ProjectEventDto>>;

public class GetProjectHistoryQueryHandler : IRequestHandler<GetProjectHistoryQuery, IReadOnlyList<ProjectEventDto>>
{
    private readonly IProjectRepository _projects;
    private readonly IProjectAccessService _access;

    public GetProjectHistoryQueryHandler(IProjectRepository projects, IProjectAccessService access)
        => (_projects, _access) = (projects, access);

    public async Task<IReadOnlyList<ProjectEventDto>> Handle(GetProjectHistoryQuery request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.UserId, ProjectRole.Viewer, ct);
        var project = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NaoEncontradoException("Projeto");
        return project.Events.OrderByDescending(x => x.CreatedAt)
            .Select(x => new ProjectEventDto(x.Id, x.ActorId, x.Kind, x.Payload, x.CreatedAt))
            .ToList();
    }
}
