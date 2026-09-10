using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Application.Interfaces;

/// <summary>
/// Repositório genérico de WorkItem (tarefa/subtarefa).
/// </summary>
public interface IWorkItemRepository
{
    Task<WorkItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WorkItem?> GetForMoveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkItem>> GetByBoardIdAsync(Guid boardId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tarefas do projeto inteiro, qualquer que seja o quadro de origem. O fluxo pertence
    /// ao projeto desde a <c>SPEC-BOARD-AS-VIEW</c>, então o Kanban do projeto precisa
    /// abrir sem depender de haver quadro escolhido.
    /// </summary>
    Task<IReadOnlyList<WorkItem>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkItem>> GetSubItemsAsync(Guid parentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkItemAssignee>> GetAssigneesAsync(Guid workItemId, CancellationToken cancellationToken = default);
    Task<WorkItem> AddAsync(WorkItem workItem, CancellationToken cancellationToken = default);
    Task UpdateAsync(WorkItem workItem, CancellationToken cancellationToken = default);
    Task UpdateWithEventAsync(WorkItem workItem, TaskEvent taskEvent, CancellationToken cancellationToken = default);
    Task DeleteAsync(WorkItem workItem, CancellationToken cancellationToken = default);
    Task AddAssigneeAsync(Guid workItemId, string userId, CancellationToken cancellationToken = default);

    /// <summary>Tarefas (nao subtarefas) atribuidas ao usuario, ordenadas pela prioridade pessoal.</summary>
    Task<IReadOnlyList<WorkItem>> GetAssignedToUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna itens cujo único quadro é <paramref name="boardId"/> (home sem outros placements
    /// OU placement exclusivo neste quadro). Usado antes de excluir um quadro.
    /// </summary>
    Task<IReadOnlyList<WorkItem>> GetExclusiveToBoardAsync(Guid boardId, CancellationToken cancellationToken = default);

    /// <summary>Reordena a fila pessoal do usuario (PersonalPriority = indice + 1).</summary>
    Task UpdatePersonalPrioritiesAsync(string userId, IReadOnlyList<Guid> orderedWorkItemIds, CancellationToken cancellationToken = default);
    Task RemoveAssigneeAsync(Guid workItemId, string userId, CancellationToken cancellationToken = default);
}
