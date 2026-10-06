using Prisma.Workspace.Application.Features.Projects;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Tests;

public class ProjectFeatureTests
{
    [Fact]
    public void ProjectValidators_RejectMethodologiesOutsideD20()
    {
        var invalidMethodology = (ProjectMethodology)5;

        var createResult = new CreateProjectCommandValidator().Validate(new CreateProjectCommand(
            "VALID", "Projeto valido", null, "owner-1", WorkNature.Project, WorkType.Development, invalidMethodology));
        var updateResult = new UpdateProjectCommandValidator().Validate(new UpdateProjectCommand(
            Guid.NewGuid(), "Projeto valido", null, "owner-1", null, null,
            ProjectStatus.Active, invalidMethodology, WorkNature.Project, WorkType.Development, null, [], "actor-1"));

        Assert.Contains(createResult.Errors, error => error.PropertyName == nameof(CreateProjectCommand.Methodology));
        Assert.Contains(updateResult.Errors, error => error.PropertyName == nameof(UpdateProjectCommand.Methodology));
    }

    [Theory]
    [InlineData(ProjectMethodology.Kanban)]
    [InlineData(ProjectMethodology.Scrum)]
    [InlineData(ProjectMethodology.Scrumban)]
    [InlineData(ProjectMethodology.SimpleList)]
    public void ProjectValidators_AcceptEveryMethodologyDefinedByD20(ProjectMethodology methodology)
    {
        var result = new CreateProjectCommandValidator().Validate(new CreateProjectCommand(
            "VALID", "Projeto valido", null, "owner-1", WorkNature.Project, WorkType.Development, methodology));

        Assert.DoesNotContain(result.Errors, error => error.PropertyName == nameof(CreateProjectCommand.Methodology));
    }

    [Fact]
    public async Task CreateProject_Kanban_DoesNotCreateSprintAutomatically()
    {
        var organizationId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"kanban-project-{Guid.NewGuid()}")
            .Options;
        await using var context = new AppDbContext(options, new OrganizationContextStub(organizationId));
        var handler = new CreateProjectCommandHandler(new ProjectRepository(context));

        var projectId = await handler.Handle(new CreateProjectCommand(
            "KANBAN", "Fluxo continuo", null, "owner-1", WorkNature.Project, WorkType.Development, ProjectMethodology.Kanban), default);

        var project = await context.Projects
            .Include(x => x.Sprints)
            .SingleAsync(x => x.Id == projectId);
        Assert.Equal(ProjectMethodology.Kanban, project.Methodology);
        Assert.Empty(project.Sprints);
        Assert.False(await context.Sprints.AnyAsync(x => x.ProjectId == projectId));
    }

    [Fact]
    public async Task ProjectList_ExposesStoredSettingsWithoutLosingThem()
    {
        var project = Project.Criar(
            "PORTAL", "Portal de servicos", "owner-1", WorkNature.Improvement, WorkType.DataBi,
            methodology: ProjectMethodology.Scrumban);
        project.SettingsJson = "{\"defaultView\":\"board\"}";
        var handler = new GetProjectsQueryHandler(new ProjectRepositoryStub(project));

        var result = await handler.Handle(new GetProjectsQuery("owner-1"), default);

        var dto = Assert.Single(result);
        Assert.Equal(project.SettingsJson, dto.SettingsJson);
        Assert.Equal(ProjectMethodology.Scrumban, dto.Methodology);
        Assert.Equal(WorkNature.Improvement, dto.Nature);
        Assert.Equal(WorkType.DataBi, dto.WorkType);
    }

    [Fact]
    public async Task ProjectList_ExposesOnlyActiveTeamsForSprintSelection()
    {
        var project = Project.Criar(
            "TEAM", "Projeto com equipes", "owner-1", WorkNature.Project, WorkType.Development);
        var active = Team.Criar("Equipe ativa");
        var inactive = Team.Criar("Equipe arquivada");
        inactive.DefinirAtiva(false);
        project.Teams.Add(new ProjectTeam { ProjectId = project.Id, TeamId = active.Id, Team = active });
        project.Teams.Add(new ProjectTeam { ProjectId = project.Id, TeamId = inactive.Id, Team = inactive });

        var handler = new GetProjectsQueryHandler(new ProjectRepositoryStub(project));
        var result = await handler.Handle(new GetProjectsQuery("owner-1"), default);

        Assert.Equal(["Equipe ativa"], Assert.Single(result).Teams.Select(team => team.Name));
    }

    [Fact]
    public void ProjectValidators_RejectMissingClassification()
    {
        var result = new CreateProjectCommandValidator().Validate(new CreateProjectCommand(
            "VALID", "Projeto valido", null, "owner-1", WorkNature.Unclassified, WorkType.Unclassified));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateProjectCommand.Nature));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateProjectCommand.WorkType));
    }

    [Fact]
    public void WorkItemCatalog_ContainsEveryInitialRequestedKindAndOrigin()
    {
        Assert.Contains(WorkItemKind.Task, Enum.GetValues<WorkItemKind>());
        Assert.Contains(WorkItemKind.Subtask, Enum.GetValues<WorkItemKind>());
        Assert.Contains(WorkItemKind.Epic, Enum.GetValues<WorkItemKind>());
        Assert.Contains(WorkItemKind.UserStory, Enum.GetValues<WorkItemKind>());
        Assert.Contains(WorkItemKind.Bug, Enum.GetValues<WorkItemKind>());
        Assert.Contains(WorkItemKind.Improvement, Enum.GetValues<WorkItemKind>());
        Assert.Contains(WorkItemKind.TechnicalDebt, Enum.GetValues<WorkItemKind>());
        Assert.Contains(WorkItemKind.Request, Enum.GetValues<WorkItemKind>());
        Assert.Contains(WorkItemKind.Incident, Enum.GetValues<WorkItemKind>());

        Assert.Equal(5, Enum.GetValues<WorkItemOrigin>().Length);
        Assert.Contains(WorkItemOrigin.Internal, Enum.GetValues<WorkItemOrigin>());
        Assert.Contains(WorkItemOrigin.ExternalPortal, Enum.GetValues<WorkItemOrigin>());
        Assert.Contains(WorkItemOrigin.Form, Enum.GetValues<WorkItemOrigin>());
        Assert.Contains(WorkItemOrigin.Integration, Enum.GetValues<WorkItemOrigin>());
        Assert.Contains(WorkItemOrigin.Import, Enum.GetValues<WorkItemOrigin>());
    }

    [Fact]
    public async Task ProjectOwner_CannotReceiveARoleThatDoesNotChangeEffectiveAccess()
    {
        var organizationId = Guid.NewGuid();
        var project = Project.Criar(
            "OWNER", "Projeto com responsável", "owner-1", WorkNature.Project, WorkType.Development);
        project.OrganizationId = organizationId;
        var handler = new AddProjectMemberCommandHandler(
            new ProjectRepositoryStub(project), new ProjectAccessStub(),
            new OrganizationRepositoryStub(OrganizationMember.Create(
                organizationId, "owner-1", OrganizationRole.TeamMember)),
            new OrganizationContextStub(organizationId));

        var error = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new AddProjectMemberCommand(project.Id, "owner-1", ProjectRole.Viewer, "owner-1"), default));

        Assert.Contains("responsável principal", error.Message);
        Assert.Equal(ProjectRole.ProjectAdmin, Assert.Single(project.Members).Role);
    }

    [Fact]
    public async Task ProjectCapabilities_ResolveManageSprintInProjectScope()
    {
        var projectId = Guid.NewGuid();
        var permissions = new PermissionServiceStub { Allowed = false };
        var handler = new GetProjectCapabilitiesQueryHandler(
            new ProjectAccessStub(ProjectRole.Member), permissions);

        var denied = await handler.Handle(new GetProjectCapabilitiesQuery(projectId, "member-1"), default);
        Assert.False(denied.CanManageSprint);
        Assert.Equal(PlatformPermission.ManageSprint, permissions.LastPermission);
        Assert.Equal(PermissionScope.Project, permissions.LastScope);
        Assert.Equal(projectId, permissions.LastScopeId);

        permissions.Allowed = true;
        var allowed = await handler.Handle(new GetProjectCapabilitiesQuery(projectId, "member-1"), default);
        Assert.True(allowed.CanManageSprint);
    }

    private sealed class ProjectRepositoryStub(Project project) : IProjectRepository
    {
        public Task<IReadOnlyList<Project>> GetForUserAsync(
            string userId,
            bool includeArchived = false,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Project>>([project]);

        public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Project?>(project.Id == id ? project : null);

        public Task<Project?> GetByIdWithMembersAsync(Guid id, CancellationToken cancellationToken = default)
            => GetByIdAsync(id, cancellationToken);

        public Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(project.Key == key);

        public Task AddAsync(Project value, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void AddCustomField(ProjectCustomFieldDefinition field) { }
        public void AddEvent(ProjectEvent projectEvent) { }
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class OrganizationContextStub(Guid organizationId) : IOrganizationContext
    {
        public Guid? OrganizationId { get; } = organizationId;
    }

    private sealed class ProjectAccessStub(ProjectRole? role = ProjectRole.ProjectAdmin) : IProjectAccessService
    {
        public Task<ProjectRole?> GetRoleAsync(Guid projectId, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(role);

        public Task EnsureAtLeastAsync(Guid projectId, string userId, ProjectRole minimumRole,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class PermissionServiceStub : IPermissionService
    {
        public bool Allowed { get; set; }
        public PlatformPermission? LastPermission { get; private set; }
        public PermissionScope? LastScope { get; private set; }
        public Guid? LastScopeId { get; private set; }

        public Task<bool> HasAsync(string userId, PlatformPermission permission,
            PermissionScope scope = PermissionScope.Organization, Guid? scopeId = null,
            CancellationToken cancellationToken = default)
        {
            LastPermission = permission;
            LastScope = scope;
            LastScopeId = scopeId;
            return Task.FromResult(Allowed);
        }

        public Task EnsureAsync(string userId, PlatformPermission permission,
            PermissionScope scope = PermissionScope.Organization, Guid? scopeId = null,
            CancellationToken cancellationToken = default) => Allowed
                ? Task.CompletedTask
                : Task.FromException(new UnauthorizedAccessException());
    }

    private sealed class OrganizationRepositoryStub(OrganizationMember member) : IOrganizationRepository
    {
        public Task<IReadOnlyList<Organization>> GetForUserAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Organization>>([]);
        public Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Organization?>(null);
        public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public Task AddAsync(Organization organization, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<IReadOnlyList<OrganizationMember>> GetMembersAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OrganizationMember>>([member]);
        public Task<OrganizationMember?> GetMemberAsync(Guid organizationId, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<OrganizationMember?>(member.UserId == userId ? member : null);
        public Task<int> CountActiveAdministratorsAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => Task.FromResult(1);
        public Task AddInvitationAsync(OrganizationInvitation invitation, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<OrganizationInvitation?> GetInvitationByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
            => Task.FromResult<OrganizationInvitation?>(null);
        public Task<IReadOnlyList<PermissionGrant>> GetGrantsAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PermissionGrant>>([]);
        public Task<PermissionGrant?> GetGrantAsync(Guid organizationId, string userId, PermissionScope scope,
            Guid? scopeId, PlatformPermission permission, CancellationToken cancellationToken = default)
            => Task.FromResult<PermissionGrant?>(null);
        public Task AddGrantAsync(PermissionGrant grant, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task RemoveGrantAsync(PermissionGrant grant, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
