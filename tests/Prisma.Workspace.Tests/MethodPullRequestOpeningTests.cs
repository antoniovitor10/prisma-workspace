using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodGit.Repositories;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Xunit;

namespace Prisma.Workspace.Tests;

/// <summary>HU-0007 · T-0027: abrir PR pela App (CA-5 a CA-8 e CA-10), com GitHub e store em memória.</summary>
public sealed class MethodPullRequestOpeningTests
{
    private const string Branch = "task/T-0027-abrir-pr";

    private static (PullRequestTestHarness H, TaskGitLinkDto Link) Arrange(string status = MethodStatusCodes.InDevelopment)
    {
        var h = new PullRequestTestHarness(status);
        return (h, h.Store.AddBranch(h.Task.WorkItemId, Branch));
    }
    private static Task<OpenMethodPullRequestResult> Open(PullRequestTestHarness h, TaskGitLinkDto link, string title = "Corrige X",
        string? body = "Corpo do agente", bool draft = false)
        => h.Opening().OpenAsync(h.ProjectId, "T-0027", new(link.Id, title, body, draft), h.ActorId, default);

    [Fact]
    public void CA5_title_prefix_is_added_once_and_card_line_is_appended_once()
    {
        Assert.Equal("[T-0027] Corrige X", MethodPullRequestText.EnsureTaskPrefix("T-0027", "Corrige X"));
        Assert.Equal("[T-0027] Corrige X", MethodPullRequestText.EnsureTaskPrefix("T-0027", "[T-0027] Corrige X"));
        Assert.Equal("[T-0027] [T-0001] Outra", MethodPullRequestText.EnsureTaskPrefix("T-0027", "[T-0001] Outra"));
        Assert.Equal("Corpo\n\nTask no Prisma: https://x/c", MethodPullRequestText.AppendCardLine("Corpo\n", "https://x/c"));
        Assert.Equal("Task no Prisma: https://x/c", MethodPullRequestText.AppendCardLine(null, "https://x/c"));
        Assert.Equal("Corpo\n\nTask no Prisma: https://x/c", MethodPullRequestText.AppendCardLine("Corpo\n\nTask no Prisma: https://x/c", "https://x/c"));
    }

    [Fact]
    public void CA5_validator_counts_the_prefix_in_the_256_limit()
    {
        var validator = new OpenMethodPullRequestCommandValidator();
        OpenMethodPullRequestCommand Cmd(string title, string? body = "b") => new(Guid.NewGuid(), "T-0027", new(Guid.NewGuid(), title, body), "user-1");
        // "[T-0027] " tem 9 caracteres: 247 + 9 = 256 passa; 248 + 9 = 257 é recusado.
        Assert.True(validator.Validate(Cmd(new string('t', 247))).IsValid);
        Assert.False(validator.Validate(Cmd(new string('t', 248))).IsValid);
        // Título que já começa com o prefixo não ganha outro.
        Assert.True(validator.Validate(Cmd("[T-0027] " + new string('t', 247))).IsValid);
        Assert.False(validator.Validate(Cmd("  ")).IsValid);
        Assert.False(validator.Validate(Cmd("Título", new string('c', 60_001))).IsValid);
        Assert.False(validator.Validate(new OpenMethodPullRequestCommand(Guid.NewGuid(), "T-0027", new(Guid.Empty, "Título", "b"), "user-1")).IsValid);
    }

    [Fact]
    public async Task CA5_CA6_opens_with_prefix_card_line_default_branch_base_and_moves_as_the_person()
    {
        var (h, link) = Arrange();
        h.Store.Repository = h.Store.Repository with { DefaultBranch = "principal" };
        var result = await Open(h, link);
        var created = Assert.Single(h.Provider.Created);
        Assert.Equal(Branch, created.Head); Assert.Equal("principal", created.Base); Assert.False(created.Draft);
        Assert.Equal("[T-0027] Corrige X", created.Title);
        Assert.EndsWith($"\n\nTask no Prisma: https://prisma.example.invalid/projects/{h.ProjectId:D}/backlog?item={h.Task.WorkItemId:D}", created.Body);
        Assert.StartsWith("Corpo do agente", created.Body);
        Assert.Equal(MethodPullRequestOutcomes.Opened, result.Outcome); Assert.True(result.Moved);
        Assert.Equal(7, result.Number); Assert.Equal("https://github.com/qa/metodo/pull/7", result.Url);
        Assert.Equal("PR #7 aberto; o card foi para Em revisão.", result.Message);
        // Vínculo explícito do PR criado, movimento humano para em-revisao e git_pr_opened.
        Assert.Single(h.Store.Observed); Assert.Single(h.Store.Explicit);
        var move = Assert.Single(h.Movement.Requests);
        Assert.Equal(h.Store.Project.StageMappings[MethodStatusCodes.InReview], move.DestinationStageId);
        Assert.Equal("PR #7 aberto pelo Prisma", move.Reason); Assert.Null(move.GitFact);
        Assert.Single(h.Automation.Calls);
        var opened = Assert.Single(h.Store.Opened).Record;
        Assert.Equal((7, false, MethodPullRequestOutcomes.Opened, true), (opened.Number, opened.Draft, opened.Outcome, opened.Moved));
        Assert.Equal("qa/metodo", opened.RepositoryFullName);
    }

    [Fact]
    public async Task CA6_draft_pr_is_linked_but_does_not_move()
    {
        var (h, link) = Arrange();
        var result = await Open(h, link, draft: true);
        Assert.True(Assert.Single(h.Provider.Created).Draft);
        Assert.False(result.Moved); Assert.Empty(h.Movement.Requests);
        Assert.Equal("PR #7 aberto como rascunho e vinculado; o card vai para Em revisão quando o PR sair do rascunho.", result.Message);
        Assert.True(Assert.Single(h.Store.Opened).Record.Draft);
    }

    [Fact]
    public async Task CA6_task_already_in_review_only_records()
    {
        var (h, link) = Arrange(MethodStatusCodes.InReview);
        var result = await Open(h, link);
        Assert.False(result.Moved); Assert.Empty(h.Movement.Requests);
        Assert.Equal("PR #7 aberto e vinculado.", result.Message);
        Assert.Single(h.Store.Explicit); Assert.Single(h.Store.Opened);
    }

    [Theory]
    [InlineData(MethodStatusCodes.Ready, "Pronta")]
    [InlineData(MethodStatusCodes.InTest, "Em teste")]
    [InlineData(MethodStatusCodes.Backlog, "Backlog")]
    public async Task CA6_other_status_opens_links_does_not_move_and_warns(string status, string label)
    {
        var (h, link) = Arrange(status);
        var result = await Open(h, link);
        Assert.False(result.Moved); Assert.Empty(h.Movement.Requests);
        Assert.Equal($"PR #7 aberto e vinculado; o card ficou em {label}.", result.Message);
        Assert.Single(h.Store.Explicit);
    }

    [Fact]
    public async Task CA6_webhook_moved_before_the_link_so_the_endpoint_does_not_move_again()
    {
        var (h, link) = Arrange();
        // O webhook "opened" vinculou pela branch e moveu como sistema antes do vínculo explícito do endpoint.
        h.Store.StatusAfterLink = MethodStatusCodes.InReview;
        var result = await Open(h, link);
        Assert.Empty(h.Movement.Requests); Assert.False(result.Moved);
        Assert.Single(h.Store.Explicit);
        Assert.Equal("PR #7 aberto e vinculado.", result.Message);
    }

    [Fact]
    public async Task CA7_existing_open_pr_is_linked_without_creating()
    {
        var (h, link) = Arrange();
        h.Provider.OpenPullRequests.Add(PullRequestTestProvider.Pr(41, Branch, false));
        var result = await Open(h, link);
        Assert.Empty(h.Provider.Created);
        Assert.Equal(MethodPullRequestOutcomes.LinkedExisting, result.Outcome); Assert.Equal(41, result.Number);
        Assert.StartsWith("Já havia um PR, vinculei o #41", result.Message);
        Assert.Single(h.Store.Explicit);
        Assert.Equal(MethodPullRequestOutcomes.LinkedExisting, Assert.Single(h.Store.Opened).Record.Outcome);
    }

    [Fact]
    public async Task CA7_already_exists_on_creation_falls_back_to_linking()
    {
        var (h, link) = Arrange();
        h.Provider.CreateThenReportExisting = true;
        var result = await Open(h, link);
        Assert.Equal(MethodPullRequestOutcomes.LinkedExisting, result.Outcome);
        Assert.StartsWith($"Já havia um PR, vinculei o #{result.Number}", result.Message);
        Assert.Equal(2, h.Provider.Finds);
        Assert.Single(h.Store.Explicit);
    }

    [Theory]
    [InlineData(MethodRole.Agent)]
    [InlineData(MethodRole.Human)]
    public async Task CA8_method_token_of_any_role_is_refused_before_reading_anything(MethodRole role)
    {
        var (h, link) = Arrange();
        h.Access.Current = h.Token(role);
        var error = await Assert.ThrowsAsync<MethodOperationException>(() => Open(h, link));
        Assert.Equal(403, error.StatusCode); Assert.Equal("interactive_pr_required", error.Code);
        Assert.Equal(0, h.Provider.Finds); Assert.Empty(h.Provider.Created); Assert.Equal(0, h.Store.Writes);
    }

    [Theory]
    [InlineData(PlatformPermission.Edit)]
    [InlineData(PlatformPermission.Comment)]
    [InlineData(PlatformPermission.ChangeStatus)]
    public async Task CA8_person_without_edit_comment_or_change_status_is_refused(PlatformPermission missing)
    {
        var (h, link) = Arrange();
        h.Permissions.Denied.Add(missing);
        await Assert.ThrowsAsync<AcessoNegadoException>(() => Open(h, link));
        Assert.Equal(0, h.Provider.Finds); Assert.Empty(h.Provider.Created); Assert.Equal(0, h.Store.Writes);
    }

    [Fact]
    public async Task Link_must_be_a_pushed_branch_of_an_enabled_app_repository()
    {
        var (h, _) = Arrange();
        async Task Refused(int status, string code, TaskGitLinkDto link)
        {
            var error = await Assert.ThrowsAsync<MethodOperationException>(() => Open(h, link));
            Assert.Equal(status, error.StatusCode); Assert.Equal(code, error.Code);
        }
        await Refused(404, "git_link_not_found", new TaskGitLinkDto(Guid.NewGuid(), h.Store.Repository.Id, h.Task.WorkItemId, null, Branch, "explicit", null, null, null, h.Store.Now));
        await Refused(409, "git_branch_not_pushed", h.Store.AddBranch(h.Task.WorkItemId, "task/T-0027-local", GitBranchStates.Local));
        var remote = h.Store.AddBranch(h.Task.WorkItemId, "task/T-0027-remota");
        h.Store.Repository = h.Store.Repository with { Mode = GitIntegrationModes.Poll };
        await Refused(409, "git_repository_not_app", remote);
        h.Store.Repository = h.Store.Repository with { Mode = GitIntegrationModes.App, Enabled = false };
        await Refused(409, "git_repository_disabled", remote);
        Assert.Equal(0, h.Provider.Finds); Assert.Equal(0, h.Store.Writes);
    }

    [Theory]
    [InlineData(GitPullRequestErrorCodes.PermissionMissing, 409, "A App ainda não tem permissão para abrir PR neste repositório. Aceite a nova permissão da instalação no GitHub ou avise o administrador.")]
    [InlineData(GitPullRequestErrorCodes.HeadMissing, 409, "A branch task/T-0027-abrir-pr não existe mais no GitHub. Faça o push de novo.")]
    [InlineData(GitPullRequestErrorCodes.NothingToCompare, 409, "Não há diferença entre task/T-0027-abrir-pr e main: nada para abrir no PR.")]
    [InlineData(GitPullRequestErrorCodes.DraftUnsupported, 409, "Este repositório não aceita PR em rascunho no plano atual. Desmarque \"abrir como rascunho\".")]
    [InlineData(GitPullRequestErrorCodes.Invalid, 422, "O GitHub recusou o PR (título ou corpo inválido). Revise o texto.")]
    [InlineData(GitPullRequestErrorCodes.Timeout, 503, "O GitHub não respondeu. Tente de novo em instantes.")]
    [InlineData(GitPullRequestErrorCodes.Unavailable, 503, "O GitHub não respondeu. Tente de novo em instantes.")]
    [InlineData(GitPullRequestErrorCodes.RateLimited, 429, "O GitHub limitou as chamadas. Tente de novo depois de 10:30 (horário de Brasília).")]
    public async Task CA10_github_refusal_becomes_portuguese_message_and_nothing_is_written(string code, int status, string message)
    {
        var (h, link) = Arrange();
        h.Provider.CreateFailure = code;
        h.Provider.RetryAt = DateTimeOffset.Parse("2026-11-27T13:30:00Z");
        var error = await Assert.ThrowsAsync<MethodOperationException>(() => Open(h, link));
        Assert.Equal(status, error.StatusCode); Assert.Equal(code, error.Code); Assert.Equal(message, error.Message);
        Assert.Equal(0, h.Store.Writes); Assert.Empty(h.Movement.Requests);
    }

    [Fact]
    public async Task CA10_refusal_while_listing_is_translated_too()
    {
        var (h, link) = Arrange();
        h.Provider.FindFailure = GitPullRequestErrorCodes.PermissionMissing;
        var error = await Assert.ThrowsAsync<MethodOperationException>(() => Open(h, link));
        Assert.Equal(GitPullRequestErrorCodes.PermissionMissing, error.Code);
        Assert.Empty(h.Provider.Created); Assert.Equal(0, h.Store.Writes);
    }

    [Fact]
    public async Task CA10_pr_created_but_link_failed_reports_the_number_and_next_click_links()
    {
        var (h, link) = Arrange();
        h.Store.LinkFailure = new MethodOperationException(409, "git_concurrency", "A tarefa mudou.");
        var error = await Assert.ThrowsAsync<MethodOperationException>(() => Open(h, link));
        Assert.Equal(GitPullRequestErrorCodes.LinkFailed, error.Code);
        Assert.Equal("O PR #7 foi aberto no GitHub, mas não consegui vinculá-lo. Clique de novo para vincular.", error.Message);
        Assert.Empty(h.Movement.Requests); Assert.Empty(h.Store.Opened);
        // Clique seguinte: o PR já existe (RF7) e o vínculo funciona.
        h.Store.LinkFailure = null;
        var result = await Open(h, link);
        Assert.Equal(MethodPullRequestOutcomes.LinkedExisting, result.Outcome); Assert.Equal(7, result.Number);
        Assert.Single(h.Provider.Created);
    }

    [Fact]
    public async Task Move_refused_by_the_policy_keeps_the_pr_linked_and_explains()
    {
        var (h, link) = Arrange();
        h.Movement.Failure = new MethodOperationException(409, "transition_denied", "Transição não permitida.");
        var result = await Open(h, link);
        Assert.False(result.Moved);
        Assert.Equal("PR #7 aberto; o card não foi movido: Transição não permitida.", result.Message);
        Assert.Single(h.Store.Explicit); Assert.Single(h.Store.Opened);
    }
}
