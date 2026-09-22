namespace Prisma.Workspace.Application.Interfaces;

public interface IBoardStructureRepository
{
    Task UpdateStageAsync(Guid stageId, string name, Prisma.Workspace.Domain.Enums.StageCategory? category, string? color, bool confirmCategoryChange, string actorId, CancellationToken ct);
    Task ReorderAsync(Guid boardId, IReadOnlyList<Guid> stageIds, CancellationToken ct);
    Task DeleteStageAsync(Guid stageId, Guid? destinationStageId, string actorId, CancellationToken ct);
    Task DeleteBoardAsync(Guid boardId, Guid? destinationBoardId, Guid? destinationStageId, string actorId, CancellationToken ct);
    Task TransferAsync(Guid workItemId, Guid destinationBoardId, Guid destinationStageId, string actorId, CancellationToken ct);
}
