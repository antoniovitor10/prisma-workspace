using Detran.Kanban.Domain.Entities;

namespace Detran.Kanban.Application.Interfaces;

/// <summary>
/// Repositório de aprovações de entrega.
/// </summary>
public interface IApprovalRepository
{
    /// <summary>Aprovação pelo id, desde que o usuário seja o aprovador.</summary>
    Task<Approval?> GetByIdForApproverAsync(Guid id, string approverId, CancellationToken cancellationToken = default);

    /// <summary>Aprovações de uma tarefa (mais recente primeiro), com WorkItem carregado.</summary>
    Task<IReadOnlyList<Approval>> GetByWorkItemAsync(Guid workItemId, CancellationToken cancellationToken = default);

    /// <summary>Aprovações onde o usuário é o aprovador (pendentes primeiro).</summary>
    Task<IReadOnlyList<Approval>> GetByApproverAsync(string approverId, int limite, CancellationToken cancellationToken = default);

    Task<bool> HasPendingAsync(Guid workItemId, CancellationToken cancellationToken = default);

    Task AddAsync(Approval approval, CancellationToken cancellationToken = default);

    /// <summary>Persiste alterações feitas em uma entidade já rastreada.</summary>
    Task SaveAsync(CancellationToken cancellationToken = default);
}
