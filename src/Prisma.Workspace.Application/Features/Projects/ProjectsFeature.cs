using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Application.Features.Workflow;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using FluentValidation;
using MediatR;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Prisma.Workspace.Application.Features.Projects;

public record ProjectBoardDto(Guid Id, string Name, Guid? TeamId);
public record ProjectTeamDto(Guid Id, string Name);
public record ProjectMemberDto(string UserId, ProjectRole Role);
public record ProjectTagDto(Guid Id, string Name, string Color);
public record ProjectCustomFieldDto(
    Guid Id, string Name, CustomFieldType Type, bool IsRequired,
    string? OptionsJson, double Position, bool IsActive);
public record ProjectDto(
    Guid Id, string Key, string Name, string? Description, Guid? ClientId,
    string OwnerId, DateOnly? StartDate, DateOnly? DueDate,
    ProjectStatus Status, ProjectMethodology Methodology, WorkNature Nature, WorkType WorkType, bool IsArchived,
    string? SettingsJson,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    IReadOnlyList<ProjectBoardDto> Boards, IReadOnlyList<ProjectTeamDto> Teams,
    IReadOnlyList<ProjectMemberDto>? Members = null,
    IReadOnlyList<ProjectTagDto>? Tags = null,
    IReadOnlyList<ProjectCustomFieldDto>? CustomFields = null,
    Guid? DefaultBoardId = null);

public record GetProjectsQuery(string UserId, bool IncludeArchived = false) : IRequest<IReadOnlyList<ProjectDto>>;

public class GetProjectsQueryHandler : IRequestHandler<GetProjectsQuery, IReadOnlyList<ProjectDto>>
{
    private readonly IProjectRepository _projects;
    public GetProjectsQueryHandler(IProjectRepository projects) => _projects = projects;

    public async Task<IReadOnlyList<ProjectDto>> Handle(GetProjectsQuery request, CancellationToken ct)
        => (await _projects.GetForUserAsync(request.UserId, request.IncludeArchived, ct)).Select(Map).ToList();

    internal static ProjectDto Map(Project p)
    {
        var orderedBoards = p.Boards.OrderBy(b => b.Name).ToList();
        return new(
            p.Id, p.Key, p.Name, p.Description, p.ClientId,
            p.OwnerId, p.StartDate, p.DueDate, p.Status, p.Methodology, p.Nature, p.WorkType, p.IsArchived,
            p.SettingsJson,
            p.CreatedAt, p.UpdatedAt,
            orderedBoards.Select(b => new ProjectBoardDto(b.Id, b.Name, b.TeamId)).ToList(),
            p.Teams.OrderBy(t => t.Team.Name).Select(t => new ProjectTeamDto(t.TeamId, t.Team.Name)).ToList(),
            Tags: p.Tags.OrderBy(t => t.Tag.Name)
                .Select(t => new ProjectTagDto(t.TagId, t.Tag.Name, t.Tag.Color)).ToList(),
            CustomFields: p.CustomFields.OrderBy(x => x.Position)
                .Select(x => new ProjectCustomFieldDto(
                    x.Id, x.Name, x.Type, x.IsRequired, x.OptionsJson, x.Position, x.IsActive)).ToList(),
            DefaultBoardId: p.DefaultBoardId ?? orderedBoards.FirstOrDefault()?.Id);
    }
}

public record GetProjectQuery(Guid ProjectId, string UserId) : IRequest<ProjectDto>;

public class GetProjectQueryHandler : IRequestHandler<GetProjectQuery, ProjectDto>
{
    private readonly IProjectRepository _projects;
    private readonly IProjectAccessService _access;
    public GetProjectQueryHandler(IProjectRepository projects, IProjectAccessService access)
        => (_projects, _access) = (projects, access);

    public async Task<ProjectDto> Handle(GetProjectQuery request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.UserId, ProjectRole.Viewer, ct);
        var p = await _projects.GetByIdWithMembersAsync(request.ProjectId, ct)
            ?? throw new NaoEncontradoException("Projeto");
        var dto = GetProjectsQueryHandler.Map(p);
        return dto with { Members = p.Members.Select(m => new ProjectMemberDto(m.UserId, m.Role)).ToList() };
    }
}

public record CreateProjectCommand(
    string? Key,
    string Name,
    string? Description,
    string OwnerId,
    WorkNature Nature,
    WorkType WorkType,
    ProjectMethodology Methodology = ProjectMethodology.Kanban,
    DateOnly? StartDate = null,
    DateOnly? DueDate = null,
    Guid? WorkflowTemplateId = null) : IRequest<Guid>;

public class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        // Chave é opcional: quando vazia, o handler gera uma a partir do nome.
        RuleFor(x => x.Key!).MaximumLength(20).Matches("^[A-Za-z][A-Za-z0-9-]*$")
            .When(x => !string.IsNullOrWhiteSpace(x.Key));
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Methodology).IsInEnum();
        RuleFor(x => x.Nature).IsInEnum().NotEqual(WorkNature.Unclassified);
        RuleFor(x => x.WorkType).IsInEnum().NotEqual(WorkType.Unclassified);
        RuleFor(x => x.DueDate)
            .GreaterThanOrEqualTo(x => x.StartDate!.Value)
            .When(x => x.StartDate.HasValue && x.DueDate.HasValue)
            .WithMessage("O prazo nao pode ser anterior a data de inicio.");
    }
}

public class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, Guid>
{
    private readonly IProjectRepository _projects;
    private readonly IPermissionService? _permissions;
    private readonly IOrganizationWorkflowRepository? _organizationWorkflows;
    public CreateProjectCommandHandler(
        IProjectRepository projects, IPermissionService? permissions = null,
        IOrganizationWorkflowRepository? organizationWorkflows = null)
        => (_projects, _permissions, _organizationWorkflows) = (projects, permissions, organizationWorkflows);
    public async Task<Guid> Handle(CreateProjectCommand request, CancellationToken ct)
    {
        if (_permissions is not null)
            await _permissions.EnsureAsync(request.OwnerId, PlatformPermission.Create,
                PermissionScope.Project, cancellationToken: ct);
        string key;
        if (string.IsNullOrWhiteSpace(request.Key))
        {
            key = await GerarChaveDisponivelAsync(request.Name, ct);
        }
        else
        {
            key = request.Key.Trim().ToUpperInvariant();
            DomainException.Garantir(!await _projects.KeyExistsAsync(key, ct), "Já existe um projeto com esta chave.");
        }
        var project = Project.Criar(
            key, request.Name, request.OwnerId, request.Nature, request.WorkType, request.Description,
            request.Methodology, request.StartDate, request.DueDate);
        if (_organizationWorkflows is not null)
        {
            var template = request.WorkflowTemplateId.HasValue
                ? await _organizationWorkflows.GetCurrentTemplateAsync(request.WorkflowTemplateId.Value, ct)
                    ?? throw new NaoEncontradoException("Template de workflow")
                : await _organizationWorkflows.GetDefaultAsync(ct);
            if (template is not null)
                WorkflowTemplateProjection.ApplyToNewProject(project, template);
        }
        project.Events.Add(ProjectEvent.Register(
            project.Id, request.OwnerId, "created",
            JsonSerializer.Serialize(new { project.Name, project.Key, project.Methodology, project.Nature, project.WorkType })));
        await _projects.AddAsync(project, ct);
        return project.Id;
    }

    /// <summary>
    /// Gera uma chave curta e legível a partir do nome (iniciais das palavras significativas)
    /// e garante unicidade acrescentando um sufixo numérico quando necessário.
    /// Ex.: "Gestão de Infrações e Processos CNH" -> "GIPC".
    /// </summary>
    private async Task<string> GerarChaveDisponivelAsync(string nome, CancellationToken ct)
    {
        var stopwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "de", "da", "do", "das", "dos", "e", "em", "para", "por", "a", "o", "as", "os", "com", "the", "of", "and" };

        static string SemAcentos(string valor)
        {
            var decomposto = valor.Normalize(NormalizationForm.FormD);
            var filtrado = new string(decomposto
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray());
            return filtrado.Normalize(NormalizationForm.FormC);
        }

        var palavras = SemAcentos(nome)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !stopwords.Contains(p))
            .ToList();

        var iniciais = new string(palavras
            .Select(p => p.FirstOrDefault(char.IsLetterOrDigit))
            .Where(c => c != default)
            .Take(6)
            .ToArray()).ToUpperInvariant();

        // Nome de uma palavra só (ou iniciais curtas demais): usa as primeiras letras da palavra.
        if (iniciais.Length < 2 && palavras.Count > 0)
        {
            var letras = new string(palavras[0].Where(char.IsLetterOrDigit).Take(4).ToArray()).ToUpperInvariant();
            if (letras.Length >= iniciais.Length) iniciais = letras;
        }

        if (iniciais.Length == 0 || !char.IsLetter(iniciais[0])) iniciais = "PROJ";

        if (!await _projects.KeyExistsAsync(iniciais, ct)) return iniciais;
        for (var sufixo = 2; ; sufixo++)
        {
            var candidata = $"{iniciais}{sufixo}";
            if (!await _projects.KeyExistsAsync(candidata, ct)) return candidata;
        }
    }
}

public record AddProjectMemberCommand(Guid ProjectId, string UserId, ProjectRole Role, string ActorId) : IRequest;
public class AddProjectMemberCommandHandler : IRequestHandler<AddProjectMemberCommand>
{
    private readonly IProjectRepository _projects;
    private readonly IProjectAccessService _access;
    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationContext _organizationContext;
    public AddProjectMemberCommandHandler(
        IProjectRepository projects, IProjectAccessService access,
        IOrganizationRepository organizations, IOrganizationContext organizationContext)
        => (_projects, _access, _organizations, _organizationContext)
            = (projects, access, organizations, organizationContext);
    public async Task Handle(AddProjectMemberCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, ct);
        var project = await _projects.GetByIdWithMembersAsync(request.ProjectId, ct)
            ?? throw new NaoEncontradoException("Projeto");
        var organizationMember = await _organizations.GetMemberAsync(
            _organizationContext.RequireOrganizationId(), request.UserId, ct);
        DomainException.Garantir(organizationMember?.IsActive == true,
            "Somente membros ativos da organização podem entrar no projeto.");
        var current = project.Members.FirstOrDefault(x => x.UserId == request.UserId);
        if (current is null)
            project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = request.UserId, Role = request.Role, JoinedAt = DateTimeOffset.UtcNow });
        else current.Role = request.Role;
        _projects.AddEvent(ProjectEvent.Register(
            project.Id, request.ActorId, "member_updated",
            JsonSerializer.Serialize(new { request.UserId, request.Role })));
        await _projects.SaveAsync(ct);
    }
}

public record RemoveProjectTeamCommand(Guid ProjectId, Guid TeamId, string ActorId) : IRequest;
public class RemoveProjectTeamCommandHandler : IRequestHandler<RemoveProjectTeamCommand>
{
    private readonly IProjectRepository _projects;
    private readonly IProjectAccessService _access;
    public RemoveProjectTeamCommandHandler(IProjectRepository projects, IProjectAccessService access)
        => (_projects, _access) = (projects, access);

    public async Task Handle(RemoveProjectTeamCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, ct);
        var project = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NaoEncontradoException("Projeto");
        var link = project.Teams.FirstOrDefault(x => x.TeamId == request.TeamId)
            ?? throw new NaoEncontradoException("Equipe do projeto");
        project.Teams.Remove(link);
        _projects.AddEvent(ProjectEvent.Register(
            project.Id, request.ActorId, "team_removed",
            JsonSerializer.Serialize(new { request.TeamId })));
        await _projects.SaveAsync(ct);
    }
}

public record AddProjectTeamCommand(Guid ProjectId, Guid TeamId, string ActorId) : IRequest;
public class AddProjectTeamCommandHandler : IRequestHandler<AddProjectTeamCommand>
{
    private readonly IProjectRepository _projects;
    private readonly IProjectAccessService _access;
    private readonly ITeamRepository _teams;
    public AddProjectTeamCommandHandler(IProjectRepository projects, IProjectAccessService access, ITeamRepository teams)
        => (_projects, _access, _teams) = (projects, access, teams);
    public async Task Handle(AddProjectTeamCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, ct);
        var project = await _projects.GetByIdAsync(request.ProjectId, ct) ?? throw new NaoEncontradoException("Projeto");
        _ = await _teams.GetByIdAsync(request.TeamId, ct) ?? throw new NaoEncontradoException("Equipe");
        if (project.Teams.All(x => x.TeamId != request.TeamId))
        {
            project.Teams.Add(new ProjectTeam { ProjectId = project.Id, TeamId = request.TeamId, AddedAt = DateTimeOffset.UtcNow });
            _projects.AddEvent(ProjectEvent.Register(
                project.Id, request.ActorId, "team_added",
                JsonSerializer.Serialize(new { request.TeamId })));
            await _projects.SaveAsync(ct);
        }
    }
}
