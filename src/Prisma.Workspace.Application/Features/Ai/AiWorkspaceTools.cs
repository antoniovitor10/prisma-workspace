using System.Text.Json;
using System.Text.RegularExpressions;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Features.Ai;

public sealed class AiWorkspaceTools(IAiWorkspaceReadRepository reads, IProjectAccessService projects,
    IWorkItemAccessService items, IPermissionService permissions, IGlobalSearchRepository search) : IAiWorkspaceTools, IAiRetrieval
{
    private static readonly Regex References = new(@"\[(T:\d+|P:[\w-]+)\]", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static Guid? Id(JsonElement input, string key) => input.ValueKind == JsonValueKind.Object && input.TryGetProperty(key, out var v) && Guid.TryParse(v.ToString(), out var id) ? id : null;
    public static string? Value(JsonElement input, string key) => input.ValueKind == JsonValueKind.Object && input.TryGetProperty(key, out var v) ? v.ToString() : null;

    private async Task<bool> VisibleAsync(Guid id, string user, CancellationToken ct)
    {
        try
        {
            var row = await reads.WorkItemScopeAsync(id, ct);
            if (row is null || !await ProjectVisibleAsync(row.ProjectId, user, ct)) return false;
            await items.EnsureAsync(id, user, PlatformPermission.View, cancellationToken: ct); return true;
        }
        catch (Application.Common.Exceptions.AcessoNegadoException) { return false; }
        catch (Application.Common.Exceptions.NaoEncontradoException) { return false; }
    }
    private async Task<bool> ProjectVisibleAsync(Guid id, string user, CancellationToken ct)
        => await projects.GetRoleAsync(id, user, ct) is not null
            && await permissions.HasAsync(user, PlatformPermission.View, PermissionScope.Project, id, ct)
            && await reads.ProjectAvailableAsync(id, ct);

    public async Task ValidateContextAsync(JsonElement context, string userId, CancellationToken ct)
    {
        foreach (var name in new[] { "projectId", "boardId", "workItemId" })
            if (context.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null && !Guid.TryParse(value.ToString(), out _)) throw new AiException(400, "Contexto da rota inválido.");
        if (Id(context, "projectId") is { } p && !await ProjectVisibleAsync(p, userId, ct)) throw new AiException(403, "Projeto não disponível.");
        if (Id(context, "boardId") is { } b)
        {
            var board = await reads.BoardProjectAsync(b, ct);
            if (board is null || !await ProjectVisibleAsync(board.Value, userId, ct)
                || (Id(context, "projectId") is { } parent && parent != board.Value)) throw new AiException(403, "Quadro não disponível.");
        }
        if (Id(context, "workItemId") is { } w)
        {
            var row = await reads.WorkItemScopeAsync(w, ct);
            if (row is null || !await VisibleAsync(w, userId, ct)
                || (Id(context, "projectId") is { } pId && row.ProjectId != pId)
                || (Id(context, "boardId") is { } bId && row.BoardId != bId)) throw new AiException(403, "Tarefa não disponível.");
        }
    }

    public async Task<object> SearchAsync(string query, string userId, CancellationToken ct)
    {
        if (query.Length is < 2 or > 100) throw new AiException(400, "Busca deve ter de 2 a 100 caracteres.");
        var hits = await search.SearchAsync(query, 20, userId, ct);
        var result = new List<GlobalSearchHit>();
        foreach (var hit in hits)
        {
            if (hit.Kind == "tasks" && Guid.TryParse(hit.Id, out var id) && !await VisibleAsync(id, userId, ct)) continue;
            if (hit.Kind == "projects" && Guid.TryParse(hit.Id, out var pid) && !await ProjectVisibleAsync(pid, userId, ct)) continue;
            // As outras classes de resultado não têm autorização de escopo suficiente para entrar no prompt.
            if (hit.Kind is "tasks" or "projects") result.Add(hit);
        }
        return result.Take(40).ToArray();
    }

    public async Task<object> ExecuteAsync(string name, JsonElement a, string userId, JsonElement context, CancellationToken ct)
    {
        if (name == "search_workspace")
        {
            var hits = (GlobalSearchHit[])await SearchAsync(Value(a, "query") ?? string.Empty, userId, ct);
            return Id(context, "projectId") is { } routeId
                ? hits.Where(x => x.Kind == "projects" ? x.Id == routeId.ToString() : x.Path.StartsWith($"/projects/{routeId}/", StringComparison.Ordinal)).ToArray() : hits;
        }
        if (name == "get_project_overview" && Id(a, "id") is { } overviewProject)
        {
            if (Id(context, "projectId") is { } current && overviewProject != current) throw new AiException(403, "Consulta fora do projeto atual.");
            using var changed = JsonDocument.Parse(JsonSerializer.Serialize(new { projectId = overviewProject }));
            // O filtro é aplicado antes de selecionar a amostra, evitando contar tarefas de outros projetos.
            context = changed.RootElement.Clone();
        }
        var projectId = Id(a, "projectId") ?? Id(context, "projectId");
        if (Id(context, "projectId") is { } routeProject && projectId != routeProject) throw new AiException(403, "Consulta fora do projeto atual.");
        if (projectId is { } project && !await ProjectVisibleAsync(project, userId, ct)) throw new AiException(403, "Projeto não disponível.");
        if (name == "get_work_item")
        {
            var id = Id(a, "id") ?? throw new AiException(400, "Informe a tarefa.");
            if (!await VisibleAsync(id, userId, ct)) return new { unavailable = true };
            var row = await reads.WorkItemDetailAsync(id, ct);
            if (row is null || (projectId.HasValue && row.Board.ProjectId != projectId)) return new { unavailable = true };
            var children = await reads.ChildrenAsync(id, ct);
            var visibleChildren = new List<object>();
            foreach (var child in children) if (await VisibleAsync(child.Id, userId, ct)) visibleChildren.Add(new { child.Id, child.Number, child.Title });
            return new { row.Id, row.Number, row.Title, row.Description, row.AcceptanceCriteria, row.DueDate, row.Priority,
                subtasks = visibleChildren, checklist = row.ChecklistItems.Take(100).Select(x => new { x.Text, x.Done }),
                comments = row.Comments.OrderByDescending(x => x.CreatedAt).Take(20).Select(x => new { x.Content, x.CreatedAt }),
                attachments = row.Attachments.Take(30).Select(x => new { x.Id, x.FileName }) };
        }
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var filters = new AiWorkItemFilters(projectId, Id(a, "stageId"), Id(context, "boardId"), Value(a, "responsibleId"),
            int.TryParse(Value(a, "priority"), out var priority) ? priority : null,
            bool.TryParse(Value(a, "overdue"), out var overdue) && overdue,
            DateOnly.TryParse(Value(a, "dueFrom"), out var from) ? from : null,
            DateOnly.TryParse(Value(a, "dueTo"), out var to) ? to : null,
            DateTimeOffset.TryParse(Value(a, "updatedSince"), out var since) ? since : null,
            name == "get_my_work" ? userId : null);
        var candidates = await reads.CandidatesAsync(filters, ct);
        var visible = new List<AiWorkItemCandidate>();
        foreach (var row in candidates) if (await VisibleAsync(row.Id, userId, ct)) visible.Add(row);
        if (name == "get_project_overview")
        {
            var id = Id(a, "id") ?? projectId ?? throw new AiException(400, "Informe o projeto.");
            if (projectId.HasValue && id != projectId || !await ProjectVisibleAsync(id, userId, ct)) return new { unavailable = true };
            var scoped = visible.Where(x => x.ProjectId == id).ToList();
            var sprint = await reads.ActiveSprintAsync(id, today, ct);
            return new { columns = scoped.GroupBy(x => x.Stage).Select(x => new { name = x.Key, count = x.Count() }),
                overdue = scoped.Count(x => x.DueDate < today && x.CompletedAt == null), unassigned = scoped.Count(x => x.ResponsibleId == null), sprint, truncated = candidates.Count == 500 };
        }
        if (name == "get_recent_activity")
        {
            var ids = visible.Select(x => x.Id).ToArray();
            var sinceDate = DateTimeOffset.TryParse(Value(a, "since"), out var parsed) ? parsed : DateTimeOffset.UtcNow.AddDays(-7);
            return await reads.RecentActivityAsync(ids, sinceDate, ct);
        }
        if (name is "list_work_items" or "get_my_work") return visible.Take(100).ToArray();
        throw new AiException(400, "Ferramenta desconhecida ou não permitida.");
    }

    public async Task<IReadOnlyList<AiSource>> ResolveSourcesAsync(string text, string userId, CancellationToken ct)
    {
        var sources = new List<AiSource>();
        foreach (var reference in References.Matches(text).Select(x => x.Groups[1].Value).Distinct().Take(100))
        {
            if (reference.StartsWith("T:") && long.TryParse(reference[2..], out var number))
            {
                var row = await reads.TaskSourceAsync(number, ct);
                if (row is not null && await VisibleAsync(row.Id, userId, ct)) sources.Add(new(reference, row.Title, $"/projects/{row.ProjectId}/backlog?item={row.Id}"));
            }
            else if (reference.StartsWith("P:"))
            {
                var row = await reads.ProjectSourceAsync(reference[2..], ct);
                if (row is not null && await ProjectVisibleAsync(row.Id, userId, ct)) sources.Add(new(reference, row.Title, $"/projects/{row.Id}/backlog"));
            }
        }
        return sources;
    }
}
