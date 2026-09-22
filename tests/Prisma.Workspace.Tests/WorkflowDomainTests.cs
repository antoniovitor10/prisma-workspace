using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Application.Features.Workflow;
using Prisma.Workspace.Application.Interfaces;

namespace Prisma.Workspace.Tests;

public class WorkflowDomainTests
{
    [Fact]
    public void CreateStatus_NormalizesColorAndKeepsSemanticFlags()
    {
        var projectId = Guid.NewGuid();
        var status = WorkflowStatus.Create(
            projectId, " Em análise ", "#3b82f6", 2,
            StageCategory.InProgress, true, false);

        Assert.Equal(projectId, status.ProjectId);
        Assert.Equal("Em análise", status.Name);
        Assert.Equal("#3B82F6", status.Color);
        Assert.True(status.IsInitial);
        Assert.False(status.IsFinal);
    }

    [Theory]
    [InlineData("blue")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    public void CreateStatus_RejectsInvalidHexColor(string color)
        => Assert.Throws<DomainException>(() => WorkflowStatus.Create(
            Guid.NewGuid(), "Novo", color, 0,
            StageCategory.Ready, false, false));

    [Fact]
    public void Transition_RejectsSelfReference()
    {
        var statusId = Guid.NewGuid();
        Assert.Throws<DomainException>(() => WorkflowTransition.Create(statusId, statusId));
    }

    [Fact]
    public async Task MoveGuard_RejectsTransitionNotConfigured()
    {
        var item = new WorkItem { Id = Guid.NewGuid(), WorkflowStatusId = Guid.NewGuid() };
        var stage = new Stage { Id = Guid.NewGuid(), Name = "Revisão", WorkflowStatusId = Guid.NewGuid() };
        var repository = new WorkflowRepositoryStub { TransitionAllowed = false };

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            WorkflowMoveGuard.EnsureAllowedAsync(item, stage, repository, default));

        Assert.Contains("transicao", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MoveGuard_NaoBloqueiaPorQuantidadeDeCartoes()
    {
        // D83 removeu o limite de WIP: nenhuma quantidade de cartoes impede a movimentacao.
        var item = new WorkItem { Id = Guid.NewGuid() };
        var stage = new Stage { Id = Guid.NewGuid(), Name = "Em andamento" };
        var repository = new WorkflowRepositoryStub { ActiveItems = 999 };

        await WorkflowMoveGuard.EnsureAllowedAsync(item, stage, repository, default);
    }

    private sealed class WorkflowRepositoryStub : IWorkflowRepository
    {
        public int ActiveItems { get; init; }
        public bool TransitionAllowed { get; init; } = true;
        public Task<int> CountActiveItemsInStageAsync(Guid stageId, Guid? excludingWorkItemId = null, CancellationToken ct = default) => Task.FromResult(ActiveItems);
        public Task<bool> IsTransitionAllowedAsync(Guid sourceStatusId, Guid targetStatusId, CancellationToken ct = default) => Task.FromResult(TransitionAllowed);
        public Task<IReadOnlyList<WorkflowStatus>> GetStatusesAsync(Guid projectId, bool tracking = false, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<WorkflowStatus?> GetStatusAsync(Guid statusId, CancellationToken ct = default)
            => Task.FromResult<WorkflowStatus?>(new WorkflowStatus { Id = statusId, IsActive = true });
        public Task<bool> NameExistsAsync(Guid projectId, string name, Guid? excludingId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> IsStatusInUseAsync(Guid statusId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<WorkflowTransition>> GetTransitionsAsync(Guid projectId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Board?> GetBoardAsync(Guid boardId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Stage?> GetStageAsync(Guid stageId, CancellationToken ct = default) => throw new NotSupportedException();
        public void AddStatus(WorkflowStatus status) => throw new NotSupportedException();
        public void DeleteStatus(WorkflowStatus status) => throw new NotSupportedException();
        public void ReplaceTransitions(IReadOnlyCollection<WorkflowTransition> current, IReadOnlyCollection<WorkflowTransition> replacements) => throw new NotSupportedException();
        public void DeleteStage(Stage stage) => throw new NotSupportedException();
        public Task SynchronizeStageWorkItemsAsync(Guid stageId, Guid? workflowStatusId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }
}
