using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Application.Interfaces;

/// <summary>
/// Repositório de Stage (coluna do fluxo do projeto).
/// </summary>
public interface IStageRepository
{
    Task<Stage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Stage>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Stage>> GetByBoardIdAsync(Guid boardId, CancellationToken cancellationToken = default);
    Task<Stage> AddAsync(Stage stage, CancellationToken cancellationToken = default);
    Task UpdateAsync(Stage stage, CancellationToken cancellationToken = default);
    Task DeleteAsync(Stage stage, CancellationToken cancellationToken = default);
}
