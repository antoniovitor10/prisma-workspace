using System.Text.RegularExpressions;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Interfaces;

namespace Prisma.Workspace.Application.Features.MethodGit.Webhooks;

public sealed partial class GitTaskResolver(IMethodGitStore store)
{
    public async Task<GitTaskResolution> ResolveAsync(GitRepositoryDescriptor repository, int? number,
        string? branch, string? title, CancellationToken cancellationToken = default, bool allowInference = true)
    {
        if (number.HasValue)
        {
            var explicitLink = await store.GetExplicitPullRequestLinkAsync(repository.Id, number.Value, cancellationToken);
            if (explicitLink is not null)
            {
                var task = await store.GetTaskAsync(explicitLink.WorkItemId, cancellationToken);
                return Valid(task, repository) ? new(task, "explicit", null) : new(null, "explicit", "explicit_task_unavailable");
            }
        }
        if (!allowInference) return new(null, "none", "head_repository_requires_explicit_link");
        string? id = null;
        var source = "none";
        if (branch is not null)
        {
            var match = BranchPattern().Match(branch);
            if (match.Success) { id = match.Groups[1].Value; source = "branch"; }
            else if (branch.StartsWith("task/T-", StringComparison.Ordinal)) return new(null, "branch", "invalid_task_branch");
        }
        if (id is null && title is not null)
        {
            var markers = TitlePattern().Matches(title).Select(marker => marker.Groups[1].Value).Distinct(StringComparer.Ordinal).ToArray();
            if (markers.Length > 1) return new(null, "title", "ambiguous_title", true);
            if (markers.Length == 1) { id = markers[0]; source = "title"; }
        }
        if (id is null) return new(null, "none", "task_not_identified");
        if (id.Length > 32) return new(null, source, "invalid_method_task_id");
        var matches = await store.FindTasksAsync(repository.ProjectId, id, cancellationToken);
        if (matches.Count != 1) return new(null, source, matches.Count == 0 ? "task_not_found" : "ambiguous_method_task_id", matches.Count > 1);
        return Valid(matches[0], repository) ? new(matches[0], source, null) : new(null, source, "task_outside_project");
    }
    private static bool Valid(MethodTaskSnapshot? task, GitRepositoryDescriptor repository) => task is not null
        && task.ProjectId == repository.ProjectId && task.OrganizationId == repository.OrganizationId && task.IsTopLevel && !task.IsArchived;
    [GeneratedRegex("^task/(T-[0-9]{4,})-[^\\s]+$", RegexOptions.CultureInvariant)] private static partial Regex BranchPattern();
    [GeneratedRegex("\\[(T-[0-9]{4,})\\]", RegexOptions.CultureInvariant)] private static partial Regex TitlePattern();
}
