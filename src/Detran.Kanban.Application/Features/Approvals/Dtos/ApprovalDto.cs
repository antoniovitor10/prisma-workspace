using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;

namespace Detran.Kanban.Application.Features.Approvals.Dtos;

/// <summary>
/// Aprovação com nomes resolvidos para exibição.
/// Status segue o contrato antigo: 0 = pendente, 1 = aprovada, 2 = rejeitada.
/// </summary>
public class ApprovalDto
{
    public Guid Id { get; init; }
    public Guid WorkItemId { get; init; }
    public string? WorkItemTitle { get; init; }
    public string? BoardName { get; init; }
    public string RequesterId { get; init; } = string.Empty;
    public string RequesterName { get; init; } = string.Empty;
    public string ApproverId { get; init; } = string.Empty;
    public string ApproverName { get; init; } = string.Empty;
    public int Status { get; init; }
    public string? Note { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? DecidedAt { get; init; }

    public static ApprovalDto From(Approval approval, IReadOnlyDictionary<string, string> nomes)
    {
        string Nome(string id) => nomes.TryGetValue(id, out var nome)
            ? nome
            : UserDisplayName.Resolve(id, null, null, null);
        return new ApprovalDto
        {
            Id = approval.Id,
            WorkItemId = approval.WorkItemId,
            WorkItemTitle = approval.WorkItem?.Title,
            BoardName = approval.WorkItem?.Board?.Name,
            RequesterId = approval.RequesterId,
            RequesterName = Nome(approval.RequesterId),
            ApproverId = approval.ApproverId,
            ApproverName = Nome(approval.ApproverId),
            Status = (int)approval.Status,
            Note = approval.Note,
            CreatedAt = approval.CreatedAt,
            DecidedAt = approval.DecidedAt
        };
    }

    /// <summary>Converte a lista resolvendo os nomes dos envolvidos de uma vez.</summary>
    public static async Task<IReadOnlyList<ApprovalDto>> FromListAsync(
        IReadOnlyList<Approval> approvals,
        Interfaces.IUserDirectory users,
        CancellationToken cancellationToken)
    {
        var ids = approvals
            .SelectMany(a => new[] { a.RequesterId, a.ApproverId })
            .Distinct();
        var nomes = (await users.GetByIdsAsync(ids, includeInactive: true, cancellationToken))
            .ToDictionary(user => user.Id, user => user.DisplayName);
        return approvals.Select(a => From(a, nomes)).ToList();
    }
}
