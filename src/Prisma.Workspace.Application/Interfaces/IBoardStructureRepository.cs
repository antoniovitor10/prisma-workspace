namespace Prisma.Workspace.Application.Interfaces;

public record BoardStageImpactDto(Guid StageId, string Name, int TotalItems, int ChangedItems,
    int OpenDescendants, string SnapshotToken);

public interface IBoardStructureRepository
{
    Task<BoardStageImpactDto> GetStageImpactAsync(Guid stageId, Prisma.Workspace.Domain.Enums.StageCategory category, CancellationToken ct);
    Task UpdateStageAsync(Guid stageId, string name, Prisma.Workspace.Domain.Enums.StageCategory? category, string? color, bool confirmCategoryChange, string actorId, CancellationToken ct, bool confirmDescendants = false, string? impactToken = null);
    Task ReorderAsync(Guid boardId, IReadOnlyList<Guid> stageIds, CancellationToken ct);
    Task DeleteStageAsync(Guid stageId, Guid? destinationStageId, string actorId, CancellationToken ct);
    Task DeleteBoardAsync(Guid boardId, Guid? destinationBoardId, Guid? destinationStageId, string actorId, CancellationToken ct);
    Task TransferAsync(Guid workItemId, Guid destinationBoardId, Guid destinationStageId, string actorId, CancellationToken ct);
}
