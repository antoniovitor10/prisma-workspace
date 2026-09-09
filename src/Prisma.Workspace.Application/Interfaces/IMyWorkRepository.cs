using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Application.Interfaces;

public interface IMyWorkRepository
{
    Task<IReadOnlyList<WorkItem>> GetTasksAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<Comment>> GetRecentCommentsAsync(
        string userId, string? userEmail, int limit, CancellationToken ct = default);

    /// <summary>
    /// Projetos ativos em que a pessoa participa, como dona ou membro. Projeto arquivado
    /// ou fora do estado Ativo não entra (SPEC-MY-WORK-HUB).
    /// </summary>
    Task<IReadOnlyList<Project>> GetActiveProjectsAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Sprints dos projetos da pessoa, com as tarefas necessárias para calcular progresso.
    /// O estado é derivado das datas pelo próprio agregado (D84); a filtragem por
    /// "em curso" acontece na camada de aplicação.
    /// </summary>
    Task<IReadOnlyList<Sprint>> GetSprintsAsync(string userId, CancellationToken ct = default);
}
