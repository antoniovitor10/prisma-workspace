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
    public async Task ChangeSprintStatus_EncerraSemExigirAtivacaoManual()
    {
        // D84: não existe mais transição manual para Ativa nem exclusividade de sprint
        // ativa. O handler só encerra ou cancela; o estado Ativa vem das datas.
        var sprint = Sprint.Criar(Guid.NewGuid(), Guid.NewGuid(), "Sprint 1",
            new DateOnly(2026, 7, 15), new DateOnly(2026, 7, 25), "Meta");
        var repository = new SprintRepositoryFake(sprint) { HasActive = true };
        var handler = new ChangeSprintStatusCommandHandler(repository, new ProjectAccessFake());

        // Outra sprint ativa no projeto não impede nada.
        await handler.Handle(new ChangeSprintStatusCommand(sprint.Id, SprintStatus.Closed, "actor"), default);
        Assert.NotNull(sprint.CompletedAt);

        // E pedir ativação manual é recusado.
        var outra = Sprint.Criar(Guid.NewGuid(), null, "Sprint 2",
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 10), null);
        var handler2 = new ChangeSprintStatusCommandHandler(
            new SprintRepositoryFake(outra), new ProjectAccessFake());
        await Assert.ThrowsAsync<DomainException>(() => handler2.Handle(
            new ChangeSprintStatusCommand(outra.Id, SprintStatus.Active, "actor"), default));
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

    [Fact]
    public async Task DeleteSprint_DesvinculaTarefasSemAsTocar()
    {
        // SPEC-S-003 v3, itens 17 e 18: excluir a sprint remove só o vínculo. Quadro,
        // coluna, posição e conteúdo da tarefa ficam intactos.
        var sprint = Sprint.Criar(Guid.NewGuid(), null, "Sprint 1",
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 15), null);
        var boardId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var tarefa = new WorkItem
        {
            Id = Guid.NewGuid(), Number = 42, Title = "Tarefa vinculada",
            BoardId = boardId, StageId = stageId, Position = 300, SprintId = sprint.Id,
        };
        sprint.WorkItems.Add(tarefa);

        var repository = new SprintRepositoryFake(sprint);
        var handler = new DeleteSprintCommandHandler(repository, new ProjectAccessFake());

        await handler.Handle(new DeleteSprintCommand(sprint.Id, "actor"), default);

        Assert.True(repository.Removed);
        Assert.Null(tarefa.SprintId);
        // A tarefa continua onde estava.
        Assert.Equal(boardId, tarefa.BoardId);
        Assert.Equal(stageId, tarefa.StageId);
        Assert.Equal(300, tarefa.Position);
        Assert.False(tarefa.IsArchived);
        Assert.Equal("Tarefa vinculada", tarefa.Title);
    }

    [Fact]
    public async Task DeleteSprint_FuncionaComSprintJaEncerrada()
    {
        var sprint = Sprint.Criar(Guid.NewGuid(), null, "Sprint de janeiro",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 30), null);
        var repository = new SprintRepositoryFake(sprint);
        var handler = new DeleteSprintCommandHandler(repository, new ProjectAccessFake());

        await handler.Handle(new DeleteSprintCommand(sprint.Id, "actor"), default);

        Assert.True(repository.Removed);
    }

    private sealed class SprintRepositoryFake(Sprint sprint) : ISprintRepository
    {
        public bool HasActive { get; set; }
        public bool Removed { get; private set; }
        public Task<Sprint?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Sprint?>(id == sprint.Id ? sprint : null);
        public Task RemoveWithUnlinkAsync(Sprint value, CancellationToken cancellationToken = default)
        {
            Removed = true;
            foreach (var item in value.WorkItems) item.SprintId = null;
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<Sprint>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Sprint>>([sprint]);
        public Task AddAsync(Sprint value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task GestaoDeSprint_ExigePermissaoConfiguravel_NaoPapelFixo()
    {
        // SPEC-S-003 v3, item 16: quem gerencia sprint é definido pela permissão
        // configurável ManageSprint, não mais pelo papel fixo ScrumMaster.
        var sprint = Sprint.Criar(Guid.NewGuid(), null, "Sprint 1",
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 15), null);
        var permissoes = new PermissionServiceFake { Permitido = false };
        var handler = new DeleteSprintCommandHandler(
            new SprintRepositoryFake(sprint), new ProjectAccessFake(), permissoes);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(
            new DeleteSprintCommand(sprint.Id, "sem-permissao"), default));

        Assert.Equal(PlatformPermission.ManageSprint, permissoes.UltimaPermissao);
        Assert.Equal(PermissionScope.Project, permissoes.UltimoEscopo);

        // Com a permissão concedida, a mesma pessoa consegue.
        permissoes.Permitido = true;
        var repositorio = new SprintRepositoryFake(sprint);
        await new DeleteSprintCommandHandler(repositorio, new ProjectAccessFake(), permissoes)
            .Handle(new DeleteSprintCommand(sprint.Id, "com-permissao"), default);
        Assert.True(repositorio.Removed);
    }

    private sealed class PermissionServiceFake : IPermissionService
    {
        public bool Permitido { get; set; } = true;
        public PlatformPermission? UltimaPermissao { get; private set; }
        public PermissionScope? UltimoEscopo { get; private set; }

        public Task<bool> HasAsync(string userId, PlatformPermission permission,
            PermissionScope scope = PermissionScope.Organization, Guid? scopeId = null,
            CancellationToken cancellationToken = default) => Task.FromResult(Permitido);

        public Task EnsureAsync(string userId, PlatformPermission permission,
            PermissionScope scope = PermissionScope.Organization, Guid? scopeId = null,
            CancellationToken cancellationToken = default)
        {
            UltimaPermissao = permission;
            UltimoEscopo = scope;
            if (!Permitido) throw new UnauthorizedAccessException("Permissão negada.");
            return Task.CompletedTask;
        }
    }

    private sealed class ProjectAccessFake : IProjectAccessService
    {
        public Task<ProjectRole?> GetRoleAsync(Guid projectId, string userId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectRole?>(ProjectRole.ProjectAdmin);
        public Task EnsureAtLeastAsync(Guid projectId, string userId, ProjectRole minimumRole, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
