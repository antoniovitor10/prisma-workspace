using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodGit.Repositories;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Services.MethodGit;
using Xunit;
using GuardFixture = Prisma.Workspace.Tests.MethodGenericWriteGuardTests.GuardFixture;

namespace Prisma.Workspace.Tests;

/// <summary>HU-0007 · T-0026 (CA-2): rascunho do PR gravado pelo agente e lido pela API, sem tabela nova.</summary>
public sealed class MethodPullRequestDraftTests
{
    private static SaveMethodPullRequestDraftCommand Command(string title, string? body = "corpo")
        => new(Guid.NewGuid(), "T-0026", new(title, body), "user-1");

    [Fact]
    public void CA2_validator_refuses_empty_title_title_over_256_and_body_over_60000()
    {
        var validator = new SaveMethodPullRequestDraftCommandValidator();
        Assert.False(validator.Validate(Command("")).IsValid);
        Assert.False(validator.Validate(Command("   ")).IsValid);
        Assert.False(validator.Validate(Command(new string('t', 257))).IsValid);
        Assert.False(validator.Validate(Command("Título", new string('c', 60_001))).IsValid);
        Assert.True(validator.Validate(Command(new string('t', 256), new string('c', 60_000))).IsValid);
        Assert.True(validator.Validate(Command("Título", null)).IsValid);
        var error = Assert.Single(validator.Validate(Command(new string('t', 257))).Errors);
        Assert.Contains("256", error.ErrorMessage);
    }

    [Fact]
    public async Task CA2_handler_refuses_who_cannot_edit_and_does_not_call_the_store()
    {
        var h = new PullRequestTestHarness();
        h.Access.Current = h.Token(MethodRole.Agent);
        h.Permissions.Denied.Add(PlatformPermission.Edit);
        await Assert.ThrowsAsync<AcessoNegadoException>(() => h.Drafts().SaveAsync(h.ProjectId, "T-0027", new("Título", "Corpo"), h.ActorId, default));
        Assert.Empty(h.Store.SavedDrafts);
    }

    [Fact]
    public async Task CA2_system_principal_cannot_save_draft()
    {
        var h = new PullRequestTestHarness();
        h.Access.Current = h.Token(MethodRole.System);
        var error = await Assert.ThrowsAsync<MethodOperationException>(() => h.Drafts().SaveAsync(h.ProjectId, "T-0027", new("Título", "Corpo"), h.ActorId, default));
        Assert.Equal(403, error.StatusCode); Assert.Equal("git_pr_draft_permission", error.Code);
        Assert.Empty(h.Store.SavedDrafts);
    }

    [Fact]
    public async Task CA2_agent_and_ui_save_trimmed_title_and_empty_body_when_missing()
    {
        var h = new PullRequestTestHarness();
        h.Access.Current = h.Token(MethodRole.Agent);
        await h.Drafts().SaveAsync(h.ProjectId, "T-0027", new("  Título do agente  ", null), h.ActorId, default);
        h.Access.Current = h.Ui();
        await h.Drafts().SaveAsync(h.ProjectId, "T-0027", new("Título da pessoa", "Corpo"), h.ActorId, default);
        Assert.Equal(new[] { ("Título do agente", ""), ("Título da pessoa", "Corpo") }, h.Store.SavedDrafts.Select(x => (x.Title, x.Body)));
    }

    [Fact]
    public async Task CA2_get_without_draft_is_404_pr_draft_not_found()
    {
        var h = new PullRequestTestHarness();
        var error = await Assert.ThrowsAsync<MethodOperationException>(() => h.Drafts().GetAsync(h.ProjectId, "T-0027", h.ActorId, default));
        Assert.Equal(404, error.StatusCode); Assert.Equal("pr_draft_not_found", error.Code);
    }

    [Fact]
    public async Task CA2_token_of_another_project_is_refused()
    {
        var h = new PullRequestTestHarness();
        h.Access.Current = h.Token(MethodRole.Agent) with { ProjectId = Guid.NewGuid() };
        var error = await Assert.ThrowsAsync<MethodOperationException>(() => h.Drafts().SaveAsync(h.ProjectId, "T-0027", new("Título", "Corpo"), h.ActorId, default));
        Assert.Equal(403, error.StatusCode);
        Assert.Empty(h.Store.SavedDrafts);
    }

    /// <summary>Store real (EF em memória) com token de agente emitido e PermissionService real.</summary>
    [Fact]
    public async Task CA2_issued_agent_token_saves_versions_and_same_text_creates_no_event()
    {
        await using var f = await GuardFixture.CreateAsync(MethodStatusCodes.InDevelopment);
        var agent = await f.TokenAsync("agente");
        await using (var scope = f.Scope(agent))
        {
            var first = await scope.Store.SavePullRequestDraftAsync(f.ItemId, "Primeiro título", "Primeiro corpo");
            Assert.True(first.Changed);
            f.Seed.Clock.Advance(TimeSpan.FromSeconds(1));
            var same = await scope.Store.SavePullRequestDraftAsync(f.ItemId, "Primeiro título", "Primeiro corpo");
            Assert.False(same.Changed);
            f.Seed.Clock.Advance(TimeSpan.FromSeconds(1));
            await scope.Store.SavePullRequestDraftAsync(f.ItemId, "Segundo título", "Segundo corpo");
        }
        await using (var scope = f.Scope(f.Ui))
        {
            var current = await scope.Store.GetPullRequestDraftAsync(f.ItemId);
            Assert.NotNull(current);
            Assert.Equal("Segundo título", current!.Title); Assert.Equal("Segundo corpo", current.Body);
            Assert.Equal(agent.DisplayName, current.ActorName);
            var events = await scope.Db.TaskEvents.AsNoTracking().Where(x => x.WorkItemId == f.ItemId && x.Kind == EfMethodGitStore.PullRequestDraftSavedEvent)
                .OrderBy(x => x.CreatedAt).ToListAsync();
            Assert.Equal(2, events.Count);
            using var payload = JsonDocument.Parse(events[0].Payload!);
            Assert.Equal("Primeiro título", payload.RootElement.GetProperty("title").GetString());
            Assert.Equal("agente", payload.RootElement.GetProperty("methodRole").GetString());
            Assert.Equal("methodToken", payload.RootElement.GetProperty("authenticationKind").GetString());
            Assert.Equal(agent.ActorId, events[0].ActorId);
            // Sem token no payload: nenhuma propriedade carrega a credencial.
            Assert.DoesNotContain("pmt_", events[0].Payload!);
            Assert.DoesNotContain(payload.RootElement.EnumerateObject(), p => p.Name.Contains("token", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task CA2_task_without_draft_returns_null_from_store()
    {
        await using var f = await GuardFixture.CreateAsync(MethodStatusCodes.InDevelopment);
        await using var scope = f.Scope(f.Ui);
        Assert.Null(await scope.Store.GetPullRequestDraftAsync(f.ItemId));
    }
}
