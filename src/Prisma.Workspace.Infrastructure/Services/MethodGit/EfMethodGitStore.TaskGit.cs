using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Infrastructure.Services.MethodGit;

public sealed partial class EfMethodGitStore
{
    private static readonly string[] GitEventKinds = ["git_branch_linked", "git_branch_pushed", "git_pr_linked", "git_pr_unlinked"];

    /// <summary>Aba Git (T-0006): uma leitura dos vínculos e observações da task e da linha do tempo do histórico.</summary>
    public async Task<TaskGitViewDto?> GetTaskGitViewAsync(Guid workItemId, CancellationToken ct = default)
    {
        var task = await GetTaskAsync(workItemId, ct);
        if (task is null) return null;
        var links = await Links.AsNoTracking().Include(x => x.Observation).Include(x => x.Integration)
            .Where(x => x.WorkItemId == workItemId && x.ProjectId == task.ProjectId).OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
        var repositories = await Repositories.AsNoTracking().Where(x => x.ProjectId == task.ProjectId && x.Enabled)
            .OrderBy(x => x.RepositoryFullName)
            .Select(x => new TaskGitRepositoryOptionDto(x.Id, x.RepositoryFullName, x.Mode, x.LastPolledAtUtc, x.LastPollStatus, x.DefaultBranch)).ToListAsync(ct);

        var branches = new List<TaskGitBranchDto>();
        foreach (var link in links.Where(x => x.BranchName is not null && x.PullRequestObservationId is null))
            branches.Add(new(link.Id, link.IntegrationId, link.Integration.RepositoryFullName, link.BranchName!, link.BranchState,
                link.LastSeenAtUtc, link.Source, link.CreatedAtUtc, BranchUrl(link.Integration.RepositoryFullName, link.BranchName!)));
        var pullRequests = new List<TaskGitPullRequestDto>();
        foreach (var link in links.Where(x => x.Observation is not null))
        {
            var o = link.Observation!;
            pullRequests.Add(new(link.Id, link.IntegrationId, link.Integration.RepositoryFullName, o.PullRequestNumber, o.Title, o.State,
                o.IsDraft, IsMerged(o), o.ApprovalsCount, o.ChangesRequested, o.Url, o.HeadBranch, o.BaseBranch, o.PullRequestAuthorName,
                link.Source, o.ProviderUpdatedAtUtc, link.CreatedAtUtc));
            // A branch de um PR também é uma branch da task (enviada, já que o PR existe no GitHub).
            if (!branches.Any(b => b.IntegrationId == link.IntegrationId && b.BranchName == o.HeadBranch))
                branches.Add(new(null, link.IntegrationId, link.Integration.RepositoryFullName, o.HeadBranch, GitBranchStates.Remote,
                    o.ObservedAtUtc, link.Source, link.CreatedAtUtc, BranchUrl(link.Integration.RepositoryFullName, o.HeadBranch)));
        }

        var timeline = new List<TaskGitTimelineEntryDto>();
        var events = await db.Set<TaskEvent>().AsNoTracking().Where(x => x.WorkItemId == workItemId
                && (GitEventKinds.Contains(x.Kind) || (x.Payload != null && x.Payload.Contains("\"factType\""))))
            .OrderBy(x => x.CreatedAt).ToListAsync(ct);
        var names = links.GroupBy(x => x.IntegrationId).ToDictionary(g => g.Key, g => g.First().Integration.RepositoryFullName);
        foreach (var row in events)
        {
            var entry = TimelineEntry(row, names);
            if (entry is not null) timeline.Add(entry);
        }
        foreach (var link in links.Where(x => x.Source is "branch" or "title"))
        {
            // Vínculo inferido pelo processador (nome da branch ou título): sem evento próprio no histórico.
            var o = link.Observation;
            timeline.Add(new("git_pr_inferred", link.CreatedAtUtc, null, null, link.Integration.RepositoryFullName,
                o?.HeadBranch ?? link.BranchName, o?.PullRequestNumber, o?.Url));
        }
        foreach (var pr in pullRequests.Where(x => x.State == "closed"))
        {
            // Merge e fechamento observados sem movimento (task não autorizada, guarda etc.) também contam a história.
            var fact = pr.Merged ? MethodGitFactCodes.PullRequestMerged : MethodGitFactCodes.PullRequestClosedWithoutMerge;
            if (timeline.Any(x => x.Kind == "git_fact" && x.FactType == fact && x.PullRequestNumber == pr.Number)) continue;
            timeline.Add(new(pr.Merged ? "git_pr_merged" : "git_pr_closed", pr.UpdatedAtUtc, null, null, pr.RepositoryFullName,
                pr.HeadBranch, pr.Number, pr.Url));
        }
        // CanLink é decidido pela aplicação (permissões da tarefa); a leitura devolve falso.
        return new(workItemId, task.MethodTaskId, false, repositories, branches, pullRequests,
            timeline.OrderBy(x => x.AtUtc).ToArray());
    }

    private static bool IsMerged(GitPullRequestObservation o) => o.State == "closed" && o.MergeSha is not null;

    private static string BranchUrl(string repository, string branch)
        => $"https://github.com/{repository}/tree/{string.Join('/', branch.Split('/').Select(Uri.EscapeDataString))}";

    private static TaskGitTimelineEntryDto? TimelineEntry(TaskEvent row, IReadOnlyDictionary<Guid, string> names)
    {
        try
        {
            using var json = JsonDocument.Parse(row.Payload ?? "{}");
            var p = json.RootElement;
            string? Text(string name) => p.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            int? Number(string name) => p.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n) ? n : null;
            var repository = Text("repositoryFullName");
            if (repository is null && p.TryGetProperty("integrationId", out var integration) && integration.TryGetGuid(out var id))
                repository = names.GetValueOrDefault(id);
            var actorName = Text("actorName");
            var url = Text("url");
            var number = Number("pullRequestNumber") ?? PullRequestNumberFromUrl(url);
            if (Text("factType") is { } factType)
                return new("git_fact", row.CreatedAt, Text("gitActorName") ?? actorName, Text("channel"), repository, null, number,
                    url, factType, Text("fromStatus"), Text("toStatus"));
            if (!GitEventKinds.Contains(row.Kind)) return null;
            return new(row.Kind, row.CreatedAt, row.Kind == "git_branch_pushed" ? Text("gitActorName") ?? actorName : actorName,
                Text("channel"), repository, Text("branch"), number, url);
        }
        catch (JsonException) { return null; }
    }

    private static int? PullRequestNumberFromUrl(string? url)
    {
        if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        return int.TryParse(uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault(), out var n) ? n : null;
    }
}
