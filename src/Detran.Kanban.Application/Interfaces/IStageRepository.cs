using Detran.Kanban.Domain.Entities;

namespace Detran.Kanban.Application.Interfaces;

/// <summary>
/// Repositório de Stage (etapa/coluna do kanban).
/// </summary>
public interface IStageRepository
{
    Task<Stage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Stage>> GetByBoardIdAsync(Guid boardId, CancellationToken cancellationToken = default);
    Task<Stage> AddAsync(Stage stage, CancellationToken cancellationToken = default);
    Task UpdateAsync(Stage stage, CancellationToken cancellationToken = default);
    Task DeleteAsync(Stage stage, CancellationToken cancellationToken = default);
}
