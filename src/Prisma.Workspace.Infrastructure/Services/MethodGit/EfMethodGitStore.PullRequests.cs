using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Infrastructure.Services.MethodGit;

/// <summary>HU-0007: rascunho do PR e registro do PR aberto pelo botão, como eventos do histórico (sem tabela nova).</summary>
public sealed partial class EfMethodGitStore
{
    public const string PullRequestDraftSavedEvent = "git_pr_draft_saved";
    public const string PullRequestOpenedEvent = "git_pr_opened";

    public async Task<MethodPullRequestDraftDto?> GetPullRequestDraftAsync(Guid workItemId, CancellationToken ct = default)
    {
        var task = await GetTaskAsync(workItemId, ct);
        if (task is null) return null;
        Scope(task.ProjectId);
        return await LatestDraftAsync(workItemId, ct);
    }

    public async Task<MethodPullRequestDraftDto> SavePullRequestDraftAsync(Guid workItemId, string title, string body, CancellationToken ct = default)
    {
        var actor = access.Current ?? throw Forbidden();
        if (actor.AuthenticationKind is not (MethodAuthenticationKind.UiSession or MethodAuthenticationKind.MethodToken)
            || actor.Role == MethodRole.System) throw Forbidden();
        var task = await GetTaskAsync(workItemId, ct) ?? throw Missing();
        Scope(task.ProjectId);
        if (task.IsArchived) throw Forbidden();
        var current = await LatestDraftAsync(workItemId, ct);
        // Texto igual ao atual: nenhum evento novo (idempotente); devolve o rascunho que já existe.
        if (current is not null && current.Title == title && current.Body == body) return current with { Changed = false };
        var now = _clock.GetUtcNow();
        db.Add(new TaskEvent { Id = Guid.NewGuid(), WorkItemId = workItemId, ActorId = actor.ActorId, Kind = PullRequestDraftSavedEvent, CreatedAt = now,
            Payload = JsonSerializer.Serialize(new { title, body, principalId = actor.PrincipalId, actorName = actor.DisplayName,
                methodRole = actor.Role.ToCode(), authenticationKind = AuthenticationCode(actor.AuthenticationKind),
                channel = actor.Channel, correlationId = actor.CorrelationId }, JsonOptions) });
        await db.SaveChangesAsync(ct);
        return new(title, body, now, actor.DisplayName, true);
    }

    public async Task RecordPullRequestOpenedAsync(Guid workItemId, GitPullRequestOpenedRecord record, CancellationToken ct = default)
    {
        var actor = access.Current ?? throw Forbidden();
        if (!actor.IsInteractiveHuman) throw Forbidden();
        var task = await GetTaskAsync(workItemId, ct) ?? throw Missing();
        db.Add(new TaskEvent { Id = Guid.NewGuid(), WorkItemId = task.WorkItemId, ActorId = actor.ActorId, Kind = PullRequestOpenedEvent, CreatedAt = _clock.GetUtcNow(),
            Payload = JsonSerializer.Serialize(new { integrationId = record.IntegrationId, repositoryFullName = record.RepositoryFullName,
                number = record.Number, url = record.Url, draft = record.Draft, outcome = record.Outcome, moved = record.Moved,
                principalId = actor.PrincipalId, actorName = actor.DisplayName, channel = "ui", correlationId = actor.CorrelationId }, JsonOptions) });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TaskGitTimelineEntryDto>> GetPullRequestTimelineAsync(Guid workItemId, CancellationToken ct = default)
    {
        var task = await GetTaskAsync(workItemId, ct);
        if (task is null) return [];
        Scope(task.ProjectId);
        var rows = await db.TaskEvents.AsNoTracking().Where(x => x.WorkItemId == workItemId
                && (x.Kind == PullRequestDraftSavedEvent || x.Kind == PullRequestOpenedEvent))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var result = new List<TaskGitTimelineEntryDto>();
        foreach (var row in rows)
        {
            if (row.Payload is null) continue;
            try
            {
                using var json = JsonDocument.Parse(row.Payload);
                var p = json.RootElement;
                string? Text(string name) => p.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                // O corpo do rascunho nunca vai para a linha do tempo: só o título.
                if (row.Kind == PullRequestDraftSavedEvent)
                    result.Add(new(row.Kind, row.CreatedAt, Text("actorName"), Text("channel"), Title: Text("title")));
                else
                    result.Add(new(row.Kind, row.CreatedAt, Text("actorName"), Text("channel"), Text("repositoryFullName"), null,
                        p.TryGetProperty("number", out var number) && number.TryGetInt32(out var n) ? n : null, Text("url"),
                        Draft: p.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True));
            }
            catch (JsonException) { }
        }
        return result;
    }

    // O rascunho atual é o último git_pr_draft_saved da task; empate de data desfeito pelo Id.
    private async Task<MethodPullRequestDraftDto?> LatestDraftAsync(Guid workItemId, CancellationToken ct)
    {
        var row = await db.TaskEvents.AsNoTracking().Where(x => x.WorkItemId == workItemId && x.Kind == PullRequestDraftSavedEvent)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (row?.Payload is null) return null;
        using var payload = JsonDocument.Parse(row.Payload);
        var root = payload.RootElement;
        string? Text(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return new(Text("title") ?? string.Empty, Text("body") ?? string.Empty, row.CreatedAt, Text("actorName"));
    }

    private static string AuthenticationCode(MethodAuthenticationKind kind)
        => kind == MethodAuthenticationKind.UiSession ? "uiSession" : "methodToken";
}
