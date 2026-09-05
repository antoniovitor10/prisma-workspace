using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Domain.Exceptions;

namespace Detran.Kanban.Domain.Entities;

/// <summary>
/// Solicitação de aprovação de entrega de uma tarefa.
/// O ciclo de vida é controlado pelos métodos <see cref="Aprovar"/> e
/// <see cref="Rejeitar"/> — nunca alterando <see cref="Status"/> por fora.
/// </summary>
public class Approval
{
    public Guid Id { get; private set; }

    /// <summary>Tarefa a aprovar.</summary>
    public Guid WorkItemId { get; private set; }
    public WorkItem WorkItem { get; set; } = null!;

    /// <summary>Usuário que solicitou a aprovação.</summary>
    public string RequesterId { get; private set; } = string.Empty;

    /// <summary>Usuário que deve aprovar.</summary>
    public string ApproverId { get; private set; } = string.Empty;

    public ApprovalStatus Status { get; private set; }

    /// <summary>Comentário do aprovador (opcional).</summary>
    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Data/hora da decisão (aprovada/rejeitada).</summary>
    public DateTimeOffset? DecidedAt { get; private set; }

    // Construtor sem parâmetros exigido pelo EF Core.
    private Approval()
    {
    }

    /// <summary>Cria uma solicitação pendente.</summary>
    public static Approval Solicitar(Guid workItemId, string requesterId, string approverId)
    {
        DomainException.Garantir(!string.IsNullOrWhiteSpace(approverId), "Aprovador obrigatório.");
        return new Approval
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItemId,
            RequesterId = requesterId,
            ApproverId = approverId,
            Status = ApprovalStatus.Pendente,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Aprovar(string? note) => Decidir(ApprovalStatus.Aprovada, note);

    public void Rejeitar(string? note) => Decidir(ApprovalStatus.Rejeitada, note);

    public bool EstaPendente => Status == ApprovalStatus.Pendente;

    private void Decidir(ApprovalStatus decisao, string? note)
    {
        DomainException.Garantir(EstaPendente, "Aprovação já decidida.");
        Status = decisao;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        DecidedAt = DateTimeOffset.UtcNow;
    }
}
