using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Application.Interfaces;

/// <summary>
/// Repositório para consulta de históricos de transição de etapas (StageHistory).
/// </summary>
public interface IStageHistoryRepository
{
    /// <summary>
    /// Históricos dos cards do quadro: só os itens que o quadro mostra
    /// (<c>WorkItemBoardVisibility</c>), base do lead time por etapa.
    /// </summary>
    Task<IReadOnlyList<StageHistory>> GetByBoardIdAsync(Guid boardId, CancellationToken cancellationToken = default);
}
