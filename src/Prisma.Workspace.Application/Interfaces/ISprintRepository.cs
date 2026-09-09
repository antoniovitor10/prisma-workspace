using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Application.Interfaces;

public interface ISprintRepository
{
    Task<IReadOnlyList<Sprint>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Sprint?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>
    /// Exclui a sprint e desvincula suas tarefas na mesma transação. As tarefas nunca são
    /// excluídas, arquivadas nem movidas: só perdem o <c>SprintId</c> (SPEC-S-003 v3).
    /// </summary>
    Task RemoveWithUnlinkAsync(Sprint sprint, CancellationToken cancellationToken = default);
    Task AddAsync(Sprint sprint, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
