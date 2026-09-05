using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Application.Interfaces;

/// <summary>
/// Repositório para consulta de históricos de transição de etapas (StageHistory).
/// </summary>
public interface IStageHistoryRepository
{
    Task<IReadOnlyList<StageHistory>> GetByBoardIdAsync(Guid boardId, CancellationToken cancellationToken = default);
}
