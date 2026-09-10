using Prisma.Workspace.Application.Features.Stages.Commands;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Xunit;

namespace Prisma.Workspace.Tests;

public class UpdateStageCommandHandlerTests
{
    private class InMemoryStageRepository : IStageRepository
    {
        public readonly Dictionary<Guid, Stage> Stages = new();

        public Task<Stage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Stages.TryGetValue(id, out var stage);
            return Task.FromResult(stage);
        }

        public Task<IReadOnlyList<Stage>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Stage> list = Stages.Values.Where(s => s.ProjectId == projectId).ToList();
            return Task.FromResult(list);
        }

        public Task<Stage> AddAsync(Stage stage, CancellationToken cancellationToken = default)
        {
            Stages[stage.Id] = stage;
            return Task.FromResult(stage);
        }

        public Task UpdateAsync(Stage stage, CancellationToken cancellationToken = default)
        {
            Stages[stage.Id] = stage;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Stage stage, CancellationToken cancellationToken = default)
        {
            Stages.Remove(stage.Id);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Handle_AlteraNomeECategoriaComSucesso()
    {
        var repo = new InMemoryStageRepository();
        var stageId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var stage = new Stage
        {
            Id = stageId,
            ProjectId = projectId,
            Name = "Ideias",
            Category = StageCategory.Backlog,
            Position = 100,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await repo.AddAsync(stage);

        var handler = new UpdateStageCommandHandler(repo);
        var command = new UpdateStageCommand(stageId, "Novo Nome da Coluna", StageCategory.InProgress);

        await handler.Handle(command, CancellationToken.None);

        var updated = await repo.GetByIdAsync(stageId);
        Assert.NotNull(updated);
        Assert.Equal("Novo Nome da Coluna", updated.Name);
        Assert.Equal(StageCategory.InProgress, updated.Category);
    }

    [Fact]
    public async Task Handle_EtapaInexistente_LancaArgumentException()
    {
        var repo = new InMemoryStageRepository();
        var handler = new UpdateStageCommandHandler(repo);
        var command = new UpdateStageCommand(Guid.NewGuid(), "Nome", StageCategory.InProgress);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
    }
}
