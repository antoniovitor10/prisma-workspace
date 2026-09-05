using Prisma.Workspace.Application.Features.Projects;
using Prisma.Workspace.Application.Features.Sprints;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;

namespace Prisma.Workspace.Tests;

public class ScrumApplicationTests
{
    [Fact]
    public async Task CreateProject_AllowsUniqueKey_AndRejectsDuplicate()
    {
        var repository = new ProjectRepositoryFake { KeyExists = false };
        var handler = new CreateProjectCommandHandler(repository);

        var id = await handler.Handle(new CreateProjectCommand("TI", "Tecnologia", null, "owner", WorkNature.Project, WorkType.Development), default);

        Assert.Equal(repository.Added!.Id, id);

        repository.KeyExists = true;
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new CreateProjectCommand("TI", "Duplicado", null, "owner", WorkNature.Project, WorkType.Development), default));
    }

    [Fact]
    public async Task ChangeSprintStatus_ActivatesOnlyWhenProjectHasNoActiveSprint()
    {
        var sprint = Sprint.Criar(Guid.NewGuid(), Guid.NewGuid(), "Sprint 1",
            new DateOnly(2026, 7, 15), new DateOnly(2026, 7, 25), "Meta");
        var repository = new SprintRepositoryFake(sprint) { HasActive = false };
        var handler = new ChangeSprintStatusCommandHandler(repository, new ProjectAccessFake());

        await handler.Handle(new ChangeSprintStatusCommand(sprint.Id, SprintStatus.Active, "actor"), default);

        Assert.Equal(SprintStatus.Active, sprint.Status);

        sprint.Status = SprintStatus.Planned;
        repository.HasActive = true;
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new ChangeSprintStatusCommand(sprint.Id, SprintStatus.Active, "actor"), default));
    }

    private sealed class ProjectRepositoryFake : IProjectRepository
    {
        public bool KeyExists { get; set; }
        public Project? Added { get; private set; }
        public Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(KeyExists);
        public Task AddAsync(Project project, CancellationToken cancellationToken = default) { Added = project; return Task.CompletedTask; }
        public Task<IReadOnlyList<Project>> GetForUserAsync(string userId, bool includeArchived = false, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Project>>([]);
        public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Project?>(null);
        public Task<Project?> GetByIdWithMembersAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Project?>(null);
        public void AddCustomField(ProjectCustomFieldDefinition field) => Added?.CustomFields.Add(field);
        public void AddEvent(ProjectEvent projectEvent) => Added?.Events.Add(projectEvent);
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class SprintRepositoryFake(Sprint sprint) : ISprintRepository
    {
        public bool HasActive { get; set; }
        public Task<Sprint?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Sprint?>(id == sprint.Id ? sprint : null);
        public Task<bool> HasActiveAsync(Guid projectId, Guid? excludingId = null, CancellationToken cancellationToken = default) => Task.FromResult(HasActive);
        public Task<IReadOnlyList<Sprint>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Sprint>>([sprint]);
        public Task AddAsync(Sprint value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ProjectAccessFake : IProjectAccessService
    {
        public Task<ProjectRole?> GetRoleAsync(Guid projectId, string userId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectRole?>(ProjectRole.ProjectAdmin);
        public Task EnsureAtLeastAsync(Guid projectId, string userId, ProjectRole minimumRole, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
