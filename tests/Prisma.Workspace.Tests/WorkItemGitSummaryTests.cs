using Prisma.Workspace.Application.Features.WorkItems.Queries;
using Xunit;

namespace Prisma.Workspace.Tests;

/// <summary>T-0005 (HU-0002): resumo Git do cartão, RF1 a RF3, D4 e D5.</summary>
public sealed class WorkItemGitSummaryTests
{
    private static readonly Guid Item = Guid.NewGuid();
    private static readonly Guid Integration = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static WorkItemGitLinkRow Pr(int number, string branch, string state, DateTimeOffset at, bool merged = false, bool draft = false,
        int approvals = 0, bool changes = false, Guid? integration = null, string repo = "detranse/canais-mobile")
        => new(Item, integration ?? Integration, repo, null, "remote", at, number, $"https://github.com/{repo}/pull/{number}", branch,
            state, draft, merged, approvals, changes, at);
    private static WorkItemGitLinkRow Branch(string name, string state, DateTimeOffset at, Guid? integration = null, string repo = "detranse/canais-mobile")
        => new(Item, integration ?? Integration, repo, name, state, at);

    [Fact]
    public void CA4_closed_without_merge_is_skipped_for_the_open_pull_request_even_when_newer()
    {
        var summary = WorkItemGitSummary.Select([
            Pr(2, "feat/b", "open", T0),
            Pr(1, "feat/a", "closed", T0.AddHours(1)),
        ])!;
        Assert.Equal(2, summary.PrNumber); Assert.Equal("open", summary.PrState); Assert.Equal("feat/b", summary.Branch);
    }

    [Fact]
    public void CA4_same_branch_reopened_as_new_pull_request_shows_the_active_one()
    {
        var summary = WorkItemGitSummary.Select([
            Pr(1, "feat/a", "closed", T0.AddHours(2)),
            Pr(3, "feat/a", "open", T0, approvals: 1),
            Branch("feat/a", "local", T0.AddMinutes(-5)),
        ])!;
        Assert.Equal(3, summary.PrNumber); Assert.Equal("approved", summary.PrState);
        // Com PR, a branch já está no GitHub, mesmo que o vínculo de branch ainda diga "local".
        Assert.Equal("remote", summary.BranchState);
    }

    [Fact]
    public void RF3_without_any_active_link_the_newest_is_shown()
    {
        var summary = WorkItemGitSummary.Select([
            Pr(1, "feat/a", "closed", T0),
            Pr(2, "feat/b", "closed", T0.AddHours(1)),
        ])!;
        Assert.Equal(2, summary.PrNumber); Assert.Equal("closed", summary.PrState);
    }

    [Fact]
    public void RF1_newest_active_unit_wins_and_a_local_branch_link_is_reported_as_local()
    {
        var summary = WorkItemGitSummary.Select([
            Pr(4, "feat/old", "closed", T0, merged: true),
            Branch("feat/novo", "local", T0.AddHours(3)),
        ])!;
        Assert.Equal("feat/novo", summary.Branch); Assert.Equal("local", summary.BranchState);
        Assert.Null(summary.PrNumber); Assert.Null(summary.PrState); Assert.Null(summary.PrUrl);
    }

    [Fact]
    public void D5_repository_badge_uses_the_short_name_and_keeps_the_full_name()
    {
        var other = Guid.NewGuid();
        var summary = WorkItemGitSummary.Select([Branch("main-feature", "remote", T0, other, "nordevs/prisma-workspace")])!;
        Assert.Equal("prisma-workspace", summary.RepoName); Assert.Equal("nordevs/prisma-workspace", summary.RepoFullName);
        Assert.Null(WorkItemGitSummary.Select([]));
    }

    [Theory]
    [InlineData("open", true, false, 2, true, "draft")]
    [InlineData("open", false, false, 1, false, "approved")]
    [InlineData("open", false, false, 1, true, "changes_requested")]
    [InlineData("open", false, false, 0, false, "open")]
    [InlineData("closed", false, true, 0, false, "merged")]
    [InlineData("closed", true, false, 3, true, "closed")]
    public void CA2_pull_request_state_for_each_combination(string state, bool draft, bool merged, int approvals, bool changes, string expected)
        => Assert.Equal(expected, WorkItemGitSummary.PrState(state, draft, merged, approvals, changes));

    [Fact]
    public void Summaries_are_grouped_per_work_item()
    {
        var second = Guid.NewGuid();
        var result = WorkItemGitSummary.Summarize([
            Pr(1, "feat/a", "open", T0),
            Branch("feat/z", "local", T0) with { WorkItemId = second },
        ]);
        Assert.Equal(2, result.Count); Assert.Equal(1, result[Item].PrNumber); Assert.Equal("feat/z", result[second].Branch);
    }
}
