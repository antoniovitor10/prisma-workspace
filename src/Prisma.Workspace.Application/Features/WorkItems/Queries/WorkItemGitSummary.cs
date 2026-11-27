using Prisma.Workspace.Application.Features.WorkItems.Dtos;

namespace Prisma.Workspace.Application.Features.WorkItems.Queries;

/// <summary>Linha mínima de um vínculo Git de uma tarefa, já com a observação do PR (quando houver).</summary>
public sealed record WorkItemGitLinkRow(
    Guid WorkItemId, Guid IntegrationId, string RepositoryFullName, string? BranchName, string BranchState,
    DateTimeOffset CreatedAtUtc, int? PrNumber = null, string? PrUrl = null, string? PrHeadBranch = null,
    string? PrState = null, bool PrIsDraft = false, bool PrMerged = false, int PrApprovals = 0,
    bool PrChangesRequested = false, DateTimeOffset? PrUpdatedAtUtc = null,
    Guid? LinkId = null, string? IntegrationMode = null, string? DefaultBranch = null, string? MethodTaskId = null);

/// <summary>
/// Regras do resumo Git do cartão (HU-0002, RF1 a RF3, D4 e D5).
/// Vínculos da mesma branch na mesma integração formam uma unidade (a branch e os PRs dela);
/// o cartão mostra a unidade mais recente ativa, isto é, cujo PR não foi fechado sem merge;
/// sem nenhuma ativa, a mais recente.
/// </summary>
public static class WorkItemGitSummary
{
    public static class PrStates
    {
        public const string Open = "open";
        public const string Draft = "draft";
        public const string Approved = "approved";
        public const string ChangesRequested = "changes_requested";
        public const string Merged = "merged";
        public const string Closed = "closed";
    }

    /// <summary>Estado exibido do PR: rascunho e revisões só valem com o PR aberto (D4: aprovação na head atual, sem mudança pedida).</summary>
    public static string PrState(string state, bool isDraft, bool merged, int approvals, bool changesRequested)
    {
        if (state == "closed") return merged ? PrStates.Merged : PrStates.Closed;
        if (isDraft) return PrStates.Draft;
        if (changesRequested) return PrStates.ChangesRequested;
        return approvals > 0 ? PrStates.Approved : PrStates.Open;
    }

    /// <summary>Nome curto do repositório (D5): a parte depois da barra.</summary>
    public static string ShortName(string fullName)
    {
        var slash = fullName.LastIndexOf('/');
        return slash >= 0 && slash < fullName.Length - 1 ? fullName[(slash + 1)..] : fullName;
    }

    public static IReadOnlyDictionary<Guid, WorkItemGitSummaryDto> Summarize(IEnumerable<WorkItemGitLinkRow> rows)
        => rows.GroupBy(row => row.WorkItemId).ToDictionary(group => group.Key, group => Select(group)!);

    public static WorkItemGitSummaryDto? Select(IEnumerable<WorkItemGitLinkRow> links)
    {
        var units = links
            .GroupBy(link => (link.IntegrationId, Branch: link.PrNumber.HasValue ? link.PrHeadBranch ?? link.BranchName : link.BranchName))
            .Select(unit =>
            {
                var branchLink = unit.Where(link => !link.PrNumber.HasValue).OrderByDescending(link => link.CreatedAtUtc).FirstOrDefault();
                // Dentro da unidade, prefere o PR que não foi fechado sem merge, depois o mais novo.
                var pr = unit.Where(link => link.PrNumber.HasValue)
                    .OrderBy(link => IsClosedWithoutMerge(link) ? 1 : 0)
                    .ThenByDescending(link => link.CreatedAtUtc).ThenByDescending(link => link.PrUpdatedAtUtc).ThenByDescending(link => link.PrNumber)
                    .FirstOrDefault();
                var latest = unit.Max(link => link.CreatedAtUtc);
                return (Latest: latest, Active: pr is null || !IsClosedWithoutMerge(pr), Branch: branchLink, Pr: pr, Any: unit.First());
            })
            .OrderByDescending(unit => unit.Active).ThenByDescending(unit => unit.Latest)
            .ToList();
        if (units.Count == 0) return null;
        var chosen = units[0];
        var fullName = chosen.Any.RepositoryFullName;
        var branch = chosen.Branch?.BranchName ?? chosen.Pr?.PrHeadBranch ?? chosen.Any.BranchName;
        // Um PR só existe no GitHub: com PR, a branch já foi enviada.
        var branchState = chosen.Pr is not null ? "remote" : chosen.Branch?.BranchState ?? "remote";
        return new WorkItemGitSummaryDto(ShortName(fullName), fullName, branch, branchState,
            chosen.Pr?.PrNumber, chosen.Pr is null ? null : PrState(chosen.Pr.PrState ?? "open", chosen.Pr.PrIsDraft, chosen.Pr.PrMerged,
                chosen.Pr.PrApprovals, chosen.Pr.PrChangesRequested), chosen.Pr?.PrUrl,
            // HU-0007: o botão Abrir PR do card usa o vínculo da branch, o modo e a principal cadastrada do repositório.
            chosen.Branch?.LinkId, chosen.Any.IntegrationMode, chosen.Any.DefaultBranch, chosen.Any.MethodTaskId);
    }

    private static bool IsClosedWithoutMerge(WorkItemGitLinkRow link) => link.PrState == "closed" && !link.PrMerged;
}
