namespace Prisma.Workspace.Application.Interfaces;

public interface IAutomationExecutor
{
    Task ExecuteStageEnteredAsync(Guid workItemId, Guid stageId, string actorId,
        CancellationToken cancellationToken = default);
}
