using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodGit.Repositories;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Xunit;

namespace Prisma.Workspace.Tests;

/// <summary>HU-0007 · T-0029 (CA-9): link da página de comparação do GitHub para repositório em modo consulta, sem gravar nada.</summary>
public sealed class MethodPullRequestCompareUrlTests
{
    private const string Branch = "task/T-0027-consulta";
    private const string Card = "https://prisma.example.invalid/projects/p/backlog?item=i";

    private static (PullRequestTestHarness H, TaskGitLinkDto Link) Arrange(string mode = GitIntegrationModes.Poll, string defaultBranch = "prod")
    {
        var h = new PullRequestTestHarness();
        h.Store.Repository = h.Store.Repository with { Mode = mode, DefaultBranch = defaultBranch, RepositoryFullName = "detranse/mobile-backend",
            AppId = mode == GitIntegrationModes.Poll ? 0 : h.Store.Repository.AppId };
        return (h, h.Store.AddBranch(h.Task.WorkItemId, Branch));
    }

    private static Dictionary<string, string> Query(string url)
        => new Uri(url).Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));

    [Fact]
    public async Task CA9_url_uses_registered_default_branch_quick_pull_task_prefix_and_card_line()
    {
        var (h, link) = Arrange();
        var result = await h.CompareLinks().BuildAsync(h.ProjectId, "T-0027", new(link.Id, "Corrige consulta de veículo", "Resumo do agente"), h.ActorId, default);
        Assert.False(result.Truncated);
        Assert.StartsWith("https://github.com/detranse/mobile-backend/compare/prod...task/T-0027-consulta?quick_pull=1&", result.Url);
        var query = Query(result.Url);
        Assert.Equal("1", query["quick_pull"]);
        Assert.Equal("[T-0027] Corrige consulta de veículo", query["title"]);
        Assert.Equal($"Resumo do agente\n\nTask no Prisma: https://prisma.example.invalid/projects/{h.ProjectId:D}/backlog?item={h.Task.WorkItemId:D}", query["body"]);
        // Nada é gravado: nem vínculo, nem observação, nem evento, nem movimento; nenhuma chamada ao GitHub.
        Assert.Equal(0, h.Store.Writes); Assert.Empty(h.Movement.Requests); Assert.Equal(0, h.Provider.Finds); Assert.Empty(h.Provider.Created);
    }

    [Fact]
    public async Task CA9_title_that_already_starts_with_the_task_id_is_not_duplicated()
    {
        var (h, link) = Arrange();
        var result = await h.CompareLinks().BuildAsync(h.ProjectId, "T-0027", new(link.Id, "[T-0027] Já com ID", null), h.ActorId, default);
        Assert.Equal("[T-0027] Já com ID", Query(result.Url)["title"]);
        Assert.Equal($"Task no Prisma: https://prisma.example.invalid/projects/{h.ProjectId:D}/backlog?item={h.Task.WorkItemId:D}", Query(result.Url)["body"]);
    }

    [Fact]
    public void CA9_long_body_is_cut_with_the_note_and_keeps_the_card_line_under_7500()
    {
        var body = string.Join('\n', Enumerable.Range(1, 2000).Select(i => $"Linha {i} do relatório com acentuação: ção, ã, é."));
        var result = GitPullRequestCompareLinks.Build("detranse/mobile-backend", "prod", Branch, "[T-0027] Título", body, Card);
        Assert.True(result.Truncated);
        Assert.InRange(result.Url.Length, GitPullRequestCompareLinks.MaxUrlLength - 200, GitPullRequestCompareLinks.MaxUrlLength);
        var sent = Query(result.Url)["body"];
        Assert.EndsWith($"\n\n{GitPullRequestCompareLinks.TruncatedNote}\n\nTask no Prisma: {Card}", sent);
        Assert.StartsWith("Linha 1 do relatório", sent);
        // Corpo que cabe não é cortado.
        Assert.False(GitPullRequestCompareLinks.Build("detranse/mobile-backend", "prod", Branch, "[T-0027] Título", "curto", Card).Truncated);
    }

    [Fact]
    public void CA9_cut_never_splits_a_surrogate_pair()
    {
        var body = string.Concat(Enumerable.Repeat("😀", 5000));
        var result = GitPullRequestCompareLinks.Build("qa/r", "main", "b", "[T-0001] t", body, Card);
        Assert.True(result.Truncated);
        var sent = Query(result.Url)["body"];
        Assert.DoesNotContain('�', sent);
        Assert.Equal(0, sent.Count(char.IsHighSurrogate) - sent.Count(char.IsLowSurrogate));
    }

    [Fact]
    public async Task CA9_repository_with_app_is_refused_with_409()
    {
        var (h, link) = Arrange(GitIntegrationModes.App, "main");
        var error = await Assert.ThrowsAsync<MethodOperationException>(() =>
            h.CompareLinks().BuildAsync(h.ProjectId, "T-0027", new(link.Id, "Título", "Corpo"), h.ActorId, default));
        Assert.Equal((409, "git_repository_has_app"), (error.StatusCode, error.Code));
    }

    [Fact]
    public async Task CA9_local_branch_unknown_link_and_person_without_edit_are_refused()
    {
        var (h, _) = Arrange();
        var local = h.Store.AddBranch(h.Task.WorkItemId, "task/T-0027-local", GitBranchStates.Local);
        var notPushed = await Assert.ThrowsAsync<MethodOperationException>(() =>
            h.CompareLinks().BuildAsync(h.ProjectId, "T-0027", new(local.Id, "Título", "Corpo"), h.ActorId, default));
        Assert.Equal("git_branch_not_pushed", notPushed.Code);
        var missing = await Assert.ThrowsAsync<MethodOperationException>(() =>
            h.CompareLinks().BuildAsync(h.ProjectId, "T-0027", new(Guid.NewGuid(), "Título", "Corpo"), h.ActorId, default));
        Assert.Equal((404, "git_link_not_found"), (missing.StatusCode, missing.Code));
        h.Permissions.Denied.Add(PlatformPermission.Edit);
        await Assert.ThrowsAsync<AcessoNegadoException>(() =>
            h.CompareLinks().BuildAsync(h.ProjectId, "T-0027", new(local.Id, "Título", "Corpo"), h.ActorId, default));
    }

    [Fact]
    public void CA9_validator_counts_the_prefix_in_the_256_limit()
    {
        var validator = new BuildMethodPullRequestCompareUrlQueryValidator();
        BuildMethodPullRequestCompareUrlQuery Q(string title) => new(Guid.NewGuid(), "T-0027", new(Guid.NewGuid(), title, "b"), "user-1");
        Assert.True(validator.Validate(Q(new string('t', 247))).IsValid);
        Assert.False(validator.Validate(Q(new string('t', 248))).IsValid);
        Assert.False(validator.Validate(Q("")).IsValid);
    }
}
