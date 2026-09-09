using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Application.Interfaces;

public interface IBacklogRepository
{
    /// <summary>
    /// Backlog do projeto. Por padrão traz só o trabalho ativo; <paramref name="includeArchived"/>
    /// inclui as tarefas arquivadas, que de outro modo somem de toda listagem e ficam
    /// inalcançáveis para restaurar.
    /// </summary>
    Task<IReadOnlyList<WorkItem>> GetProjectBacklogAsync(
        Guid projectId, bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkItem>> GetTrackedByIdsAsync(Guid projectId, IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
