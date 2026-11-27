using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prisma.Workspace.Application.Features.MethodDocuments;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodSecurity;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Services.MethodSecurity;

namespace Prisma.Workspace.Infrastructure.Services.MethodDocuments;

/// <summary>
/// Documentos do método (HU-0008, T-0020/T-0021): texto exato com versões imutáveis, ligado ao Épico, à História
/// ou à Tarefa do quadro do método. Segue o padrão das operações do método (<see cref="MethodTaskOperations"/>):
/// identidade só do contexto autenticado, permissões no item antes de gravar, transação serializável.
/// O conteúdo nunca vai para log nem para mensagem de erro.
/// </summary>
public sealed class MethodDocumentOperations(AppDbContext db, IMethodAccessContext access, IMethodGitStore store,
    IMethodProjectOperations projects, IPermissionService permissions, IProjectAccessService projectAccess, IBoardRealtimeNotifier realtime,
    TimeProvider? clock = null, ILogger<MethodDocumentOperations>? logger = null) : IMethodDocumentOperations
{
    public const string AvulsasKey = "AVULSAS";
    internal const string AvulsasContent = "---\nid: AVULSAS\ntitulo: \"Avulsas\"\n---\n\n# Avulsas\n\nTasks avulsas do método (`hu: avulsa`), sem HU própria.\n";
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    private MethodActorIdentity Actor => access.Current
        ?? throw new MethodOperationException(401, "method_identity_required", "Entre novamente para estabelecer sua identidade.");

    public async Task<IReadOnlyList<MethodDocumentSummaryDto>> ListAsync(Guid projectId, DateTimeOffset? since, Guid? workItemId, CancellationToken ct)
    {
        RejectSystem();
        if (Actor.ProjectId is { } scoped && scoped != projectId || !await db.Projects.AsNoTracking().AnyAsync(x => x.Id == projectId, ct))
            return [];
        var project = await projects.SettingsAsync(projectId, ct);
        if (project is null || !project.Enabled) return [];
        var query = db.MethodDocuments.AsNoTracking().Where(x => x.ProjectId == projectId && x.OrganizationId == project.OrganizationId);
        if (since is { } from) query = query.Where(x => x.UpdatedAtUtc > from);
        if (workItemId is { } itemId) query = query.Where(x => x.WorkItemId == itemId);
        var rows = await query.OrderBy(x => x.Kind).ThenBy(x => x.UpdatedAtUtc)
            .Select(x => new DocumentRow(x.Id, x.WorkItemId, x.Kind, x.MethodKey, x.FilePath, x.Status, x.Version, x.ContentHash,
                x.UpdatedByName, x.UpdatedVia, x.UpdatedAtUtc, x.WorkItem.Title))
            .ToListAsync(ct);
        var itemIds = rows.Select(x => x.WorkItemId).Distinct().ToArray();
        var huKeys = await db.MethodDocuments.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.Kind == MethodDocumentKind.Hu && itemIds.Contains(x.WorkItemId))
            .ToDictionaryAsync(x => x.WorkItemId, x => x.MethodKey!, ct);
        var taskIds = await db.WorkItemCustomFieldValues.AsNoTracking()
            .Where(x => x.FieldDefinitionId == project.MethodIdFieldId && itemIds.Contains(x.WorkItemId) && x.Value != null)
            .ToDictionaryAsync(x => x.WorkItemId, x => x.Value!, ct);
        var result = new List<MethodDocumentSummaryDto>();
        foreach (var row in rows)
        {
            if (!await AllowedAsync(projectId, row.WorkItemId, PlatformPermission.View, ct)) continue;
            var key = row.Kind switch
            {
                MethodDocumentKind.Prd => huKeys.TryGetValue(row.WorkItemId, out var hu) ? hu + "/prd" : null,
                MethodDocumentKind.Task => taskIds.GetValueOrDefault(row.WorkItemId),
                _ => row.MethodKey,
            };
            if (key is null) continue;
            result.Add(new(key, KindCode(row.Kind), row.WorkItemId, row.FilePath, TitleFromItem(key, row.Kind, row.ItemTitle),
                row.Status, row.Version, row.ContentHash, row.UpdatedByName, row.UpdatedVia, row.UpdatedAtUtc));
        }
        return result;
    }

    public async Task<MethodDocumentDto> GetAsync(Guid projectId, string key, CancellationToken ct)
    {
        RejectSystem();
        var parsed = Parse(key);
        var project = await ProjectAsync(projectId, ct);
        var (document, _) = await FindAsync(project, parsed, ct);
        if (document is null) throw NotFound(parsed.Key);
        await EnsureAllowedAsync(document.ProjectId, document.WorkItemId, PlatformPermission.View, ct);
        return await DtoAsync(project, parsed, document, ct);
    }

    public async Task<IReadOnlyList<MethodDocumentRevisionDto>> RevisionsAsync(Guid projectId, string key, CancellationToken ct)
    {
        var document = await ReadableAsync(projectId, key, ct);
        return await db.MethodDocumentRevisions.AsNoTracking().Where(x => x.DocumentId == document.Id)
            .OrderByDescending(x => x.Version)
            .Select(x => new MethodDocumentRevisionDto(x.Version, x.ContentHash, x.AuthorName, x.Via, x.CreatedAtUtc, null))
            .ToListAsync(ct);
    }

    public async Task<MethodDocumentRevisionDto> RevisionAsync(Guid projectId, string key, int version, CancellationToken ct)
    {
        var document = await ReadableAsync(projectId, key, ct);
        return await db.MethodDocumentRevisions.AsNoTracking().Where(x => x.DocumentId == document.Id && x.Version == version)
            .Select(x => new MethodDocumentRevisionDto(x.Version, x.ContentHash, x.AuthorName, x.Via, x.CreatedAtUtc, x.Content))
            .SingleOrDefaultAsync(ct)
            ?? throw new MethodOperationException(404, "method_document_revision_not_found", $"Versão {version} de {Parse(key).Key} não encontrada.");
    }

    public Task<MethodDocumentDto> SaveAsync(Guid projectId, string key, SaveMethodDocumentRequest request, CancellationToken ct)
    {
        if (request?.Content is null) throw Invalid("Envie o conteúdo do documento.");
        ValidateFilePath(request.FilePath);
        var content = MethodDocumentText.Normalize(request.Content);
        return WriteAsync(projectId, key, request.BaseVersion, _ => content, request.FilePath, request.ParentKey, false, ct);
    }

    public Task<MethodDocumentDto> UpdateHeaderAsync(Guid projectId, string key, UpdateMethodDocumentHeaderRequest request, CancellationToken ct)
    {
        var parsed = Parse(key);
        if (parsed.Kind == MethodDocumentKind.Prd) throw Invalid("O cabeçalho do PRD não é editável na tela.");
        if (request.Title is null && request.Status is null) throw Invalid("Informe o título ou o status.");
        var title = request.Title?.Trim();
        if (title is not null && (title.Length is 0 or > 300 || title.Any(char.IsControl)))
            throw Invalid("Informe um título de até 300 caracteres, em uma linha.");
        if (request.Status is not null && (parsed.Kind == MethodDocumentKind.Task || !MethodDocumentText.PlanningStatuses.Contains(request.Status)))
            throw Invalid("Status inválido: use aberta, planejada, concluida ou cancelada (só em HU e épico).");
        return WriteAsync(projectId, key, request.BaseVersion, current =>
        {
            if (current is null) throw NotFound(parsed.Key);
            try { return MethodDocumentText.RewriteHeader(current.Content, title, request.Status); }
            catch (ArgumentException) { throw Invalid($"O documento {parsed.Key} não tem cabeçalho YAML."); }
        }, null, null, true, ct);
    }

    public async Task<MethodTaskParentDto> RelinkTaskAsync(Guid projectId, string taskId, RelinkMethodTaskRequest request, CancellationToken ct)
    {
        RejectSystem();
        var task = Parse(taskId);
        var hu = Parse(request?.HuKey ?? string.Empty);
        if (task.Kind != MethodDocumentKind.Task || hu.Kind != MethodDocumentKind.Hu)
            throw Invalid("Informe a task (T-0001) e a História (HU-0001 ou AVULSAS).");
        var result = await AtomicAsync(async () =>
        {
            var project = await ProjectAsync(projectId, ct);
            var snapshot = await TaskAsync(project, task.Key, ct);
            var story = await db.MethodDocuments.SingleOrDefaultAsync(x => x.ProjectId == projectId
                && x.Kind == MethodDocumentKind.Hu && x.MethodKey == hu.MethodKey, ct) ?? throw NotFound(hu.Key);
            var item = await db.WorkItems.SingleAsync(x => x.Id == snapshot.WorkItemId, ct);
            await EnsureAllowedAsync(projectId, item.Id, PlatformPermission.Edit, ct);
            if (!await MethodEligibility.IsMethodStoryAsync(db, story.WorkItemId, project.BoardId, ct))
                throw Invalid($"{hu.Key} não é uma História do quadro do método.");
            if (item.ParentId != story.WorkItemId)
            {
                var previous = item.ParentId;
                item.ParentId = story.WorkItemId;
                item.UpdatedAt = _clock.GetUtcNow();
                db.TaskEvents.Add(TaskEvent.Registrar(item.Id, Actor.ActorId, "method_task_relinked",
                    JsonSerializer.Serialize(new { huKey = hu.Key, previousParentId = previous, parentId = story.WorkItemId })));
                await db.SaveChangesAsync(ct);
            }
            return new MethodTaskParentDto(task.Key, item.Id, hu.Key, story.WorkItemId);
        }, ct);
        await NotifyAsync(result.WorkItemId, "workItemUpdated", ct);
        return result;
    }

    /// <summary>História "Avulsas" do projeto (D11): criada uma vez, sem arquivo, com texto curto e fixo.</summary>
    internal static async Task<MethodDocument> EnsureAvulsasAsync(AppDbContext db, MethodProjectSnapshot project,
        MethodActorIdentity actor, DateTimeOffset now, CancellationToken ct)
    {
        var existing = db.MethodDocuments.Local.SingleOrDefault(x => x.ProjectId == project.ProjectId && x.MethodKey == AvulsasKey)
            ?? await db.MethodDocuments.SingleOrDefaultAsync(x => x.ProjectId == project.ProjectId
                && x.Kind == MethodDocumentKind.Hu && x.MethodKey == AvulsasKey, ct);
        if (existing is not null) return existing;
        var story = NewPlanningItem(project, actor, WorkItemKind.UserStory, null, "Avulsas", now);
        db.Add(story);
        var document = new MethodDocument { Id = Guid.NewGuid(), OrganizationId = project.OrganizationId, ProjectId = project.ProjectId,
            WorkItemId = story.Id, Kind = MethodDocumentKind.Hu, MethodKey = AvulsasKey };
        db.Add(document);
        Apply(db, document, AvulsasContent, null, actor, now);
        db.TaskEvents.Add(TaskEvent.Registrar(story.Id, actor.ActorId, "created", JsonSerializer.Serialize(new { title = story.Title })));
        return document;
    }

    private async Task<MethodDocumentDto> WriteAsync(Guid projectId, string key, int baseVersion,
        Func<MethodDocument?, string> contentOf, string? filePath, string? parentKey, bool headerOnly, CancellationToken ct)
    {
        RejectSystem();
        var parsed = Parse(key);
        var (dto, workItemId, created) = await AtomicAsync(async () =>
        {
            var project = await ProjectAsync(projectId, ct);
            var (document, snapshot) = await FindAsync(project, parsed, ct, tracked: true);
            if (document is null && headerOnly) throw NotFound(parsed.Key);
            if ((document?.Version ?? 0) != baseVersion)
                throw new MethodOperationException(409, "method_document_version_conflict",
                    $"O documento {parsed.Key} mudou desde que você abriu. Recarregue para ver a versão nova.");
            var content = contentOf(document);
            var header = MethodDocumentText.ParseHeader(content);
            var status = header.GetValueOrDefault("status");
            if (parsed.Kind is MethodDocumentKind.Epic or MethodDocumentKind.Hu && !string.IsNullOrEmpty(status)
                && !MethodDocumentText.PlanningStatuses.Contains(status))
                throw Invalid($"Status inválido no cabeçalho de {parsed.Key}: use aberta, planejada, concluida ou cancelada.");
            var now = _clock.GetUtcNow();
            var newItem = false;
            WorkItem item;
            if (document is null)
            {
                (item, newItem) = await TargetItemAsync(project, parsed, header, parentKey, snapshot, now, ct);
                document = new MethodDocument { Id = Guid.NewGuid(), OrganizationId = project.OrganizationId, ProjectId = project.ProjectId,
                    WorkItemId = item.Id, Kind = parsed.Kind, MethodKey = parsed.Kind is MethodDocumentKind.Epic or MethodDocumentKind.Hu ? parsed.MethodKey : null };
                db.Add(document);
            }
            else
            {
                item = await db.WorkItems.SingleAsync(x => x.Id == document.WorkItemId, ct);
                await EnsureAllowedAsync(projectId, item.Id, PlatformPermission.Edit, ct);
                if (parsed.Kind == MethodDocumentKind.Hu && parsed.MethodKey != AvulsasKey)
                    await ReparentStoryAsync(project, item, parentKey ?? header.GetValueOrDefault("epico"), ct);
            }
            EnsureNotLocked(parsed, snapshot);
            var changed = document.Version == 0 || document.Content != content || filePath is not null && filePath != document.FilePath;
            if (changed)
            {
                Apply(db, document, content, filePath, Actor, now);
                var title = header.GetValueOrDefault("titulo");
                if (parsed.MethodKey == AvulsasKey) item.Title = "Avulsas";
                else if (parsed.Kind != MethodDocumentKind.Prd && !string.IsNullOrWhiteSpace(title))
                    item.Title = Truncate($"{parsed.Key} · {title.Trim()}", 500);
                item.UpdatedAt = now;
                db.TaskEvents.Add(TaskEvent.Registrar(item.Id, Actor.ActorId, "method_document_updated", JsonSerializer.Serialize(new
                {
                    documentKind = KindCode(parsed.Kind), key = parsed.Key, version = document.Version, via = Via(Actor),
                })));
            }
            if (changed || db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(ct);
            return (await DtoAsync(project, parsed, document, ct), item.Id, newItem);
        }, ct);
        await NotifyAsync(workItemId, created ? "workItemCreated" : "workItemUpdated", ct);
        return dto;
    }

    private async Task<(WorkItem Item, bool Created)> TargetItemAsync(MethodProjectSnapshot project, ParsedKey parsed,
        IReadOnlyDictionary<string, string> header, string? parentKey, MethodTaskSnapshot? snapshot, DateTimeOffset now, CancellationToken ct)
    {
        switch (parsed.Kind)
        {
            case MethodDocumentKind.Epic:
            case MethodDocumentKind.Hu:
            {
                await EnsureAllowedAsync(project.ProjectId, null, PlatformPermission.Create, ct);
                Guid? parentId = null;
                var epicKey = parsed.Kind == MethodDocumentKind.Hu && parsed.MethodKey != AvulsasKey
                    ? parentKey ?? header.GetValueOrDefault("epico") : null;
                if (!string.IsNullOrWhiteSpace(epicKey)) parentId = (await EpicAsync(project, epicKey.Trim(), ct)).WorkItemId;
                var title = parsed.MethodKey == AvulsasKey ? "Avulsas" : header.GetValueOrDefault("titulo") is { Length: > 0 } t
                    ? $"{parsed.Key} · {t.Trim()}" : parsed.Key;
                var item = NewPlanningItem(project, Actor, parsed.Kind == MethodDocumentKind.Epic ? WorkItemKind.Epic : WorkItemKind.UserStory,
                    parentId, Truncate(title, 500), now);
                db.Add(item);
                db.TaskEvents.Add(TaskEvent.Registrar(item.Id, Actor.ActorId, "created", JsonSerializer.Serialize(new { title = item.Title })));
                return (item, true);
            }
            case MethodDocumentKind.Prd:
            {
                var story = await db.MethodDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == project.ProjectId
                    && x.Kind == MethodDocumentKind.Hu && x.MethodKey == parsed.MethodKey, ct) ?? throw NotFound(parsed.MethodKey!);
                var item = await db.WorkItems.SingleAsync(x => x.Id == story.WorkItemId, ct);
                await EnsureAllowedAsync(project.ProjectId, item.Id, PlatformPermission.Edit, ct);
                return (item, false);
            }
            default:
            {
                var item = await db.WorkItems.SingleAsync(x => x.Id == snapshot!.WorkItemId, ct);
                await EnsureAllowedAsync(project.ProjectId, item.Id, PlatformPermission.Edit, ct);
                return (item, false);
            }
        }
    }

    private async Task ReparentStoryAsync(MethodProjectSnapshot project, WorkItem story, string? epicKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(epicKey)) return; // sem épico declarado: não desfaz o vínculo existente
        var epic = await EpicAsync(project, epicKey.Trim(), ct);
        if (story.ParentId != epic.WorkItemId) story.ParentId = epic.WorkItemId;
    }

    private async Task<MethodDocument> EpicAsync(MethodProjectSnapshot project, string epicKey, CancellationToken ct)
    {
        var parsed = Parse(epicKey);
        if (parsed.Kind != MethodDocumentKind.Epic) throw Invalid("O épico deve estar no formato EPIC-0001.");
        return await db.MethodDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == project.ProjectId
            && x.Kind == MethodDocumentKind.Epic && x.MethodKey == parsed.MethodKey, ct)
            ?? throw new MethodOperationException(400, "invalid_method_document", $"O épico {parsed.Key} ainda não está no Prisma. Envie o épico antes da HU.");
    }

    private static WorkItem NewPlanningItem(MethodProjectSnapshot project, MethodActorIdentity actor, WorkItemKind kind,
        Guid? parentId, string title, DateTimeOffset now)
        => new()
        {
            Id = Guid.NewGuid(), BoardId = project.BoardId, StageId = null, ParentId = parentId, Kind = kind, Title = title,
            Position = 100, CreatedBy = actor.ActorId, CreatedAt = now, UpdatedAt = now,
        };

    private static void Apply(AppDbContext db, MethodDocument document, string content, string? filePath,
        MethodActorIdentity actor, DateTimeOffset now)
    {
        document.Content = content;
        document.ContentHash = MethodDocumentText.Hash(content);
        document.Version += 1;
        if (filePath is not null) document.FilePath = filePath;
        document.Status = document.Kind is MethodDocumentKind.Epic or MethodDocumentKind.Hu
            ? MethodDocumentText.ParseHeader(content).GetValueOrDefault("status") is { Length: > 0 } status ? status : null
            : null;
        document.UpdatedByUserId = actor.AuthenticationKind == MethodAuthenticationKind.UiSession ? actor.ActorId : null;
        document.UpdatedByName = Truncate(actor.DisplayName, 200);
        document.UpdatedVia = Via(actor);
        document.UpdatedAtUtc = now;
        db.MethodDocumentRevisions.Add(new MethodDocumentRevision
        {
            Id = Guid.NewGuid(), OrganizationId = document.OrganizationId, ProjectId = document.ProjectId, DocumentId = document.Id,
            Version = document.Version, Content = content, ContentHash = document.ContentHash,
            AuthorUserId = document.UpdatedByUserId, AuthorName = document.UpdatedByName, Via = document.UpdatedVia, CreatedAtUtc = now,
        });
    }

    /// <summary>
    /// Mesmas regras das tasks (CA-11): o token do método já combina teto, escopo e projeto no avaliador; a sessão humana
    /// exige a permissão no item e no projeto e o acesso ao projeto (leitor para ver, membro para gravar).
    /// </summary>
    private async Task<bool> AllowedAsync(Guid projectId, Guid? workItemId, PlatformPermission permission, CancellationToken ct)
    {
        if (workItemId is { } itemId && !await permissions.HasAsync(Actor.ActorId, permission, PermissionScope.WorkItem, itemId, ct)) return false;
        if (Actor.AuthenticationKind != MethodAuthenticationKind.UiSession)
            return workItemId is not null || await permissions.HasAsync(Actor.ActorId, permission, PermissionScope.Project, projectId, ct);
        if (!await permissions.HasAsync(Actor.ActorId, permission, PermissionScope.Project, projectId, ct)) return false;
        var role = await projectAccess.GetRoleAsync(projectId, Actor.ActorId, ct);
        return role is not null && role.Value >= (permission == PlatformPermission.View ? ProjectRole.Viewer : ProjectRole.Member);
    }

    private async Task EnsureAllowedAsync(Guid projectId, Guid? workItemId, PlatformPermission permission, CancellationToken ct)
    {
        if (!await AllowedAsync(projectId, workItemId, permission, ct))
            throw new MethodOperationException(403, "method_permission", "Você não tem permissão para esta operação no documento.");
    }

    private void EnsureNotLocked(ParsedKey parsed, MethodTaskSnapshot? snapshot)
    {
        if (parsed.Kind == MethodDocumentKind.Task && Actor.Role == MethodRole.Human
            && snapshot?.StatusCode == MethodStatusCodes.InDevelopment)
            throw new MethodOperationException(409, "method_document_locked",
                "O agente está executando esta task; o corpo volta a ser editável quando ela sair de em-desenvolvimento.");
    }

    private async Task<MethodDocument> ReadableAsync(Guid projectId, string key, CancellationToken ct)
    {
        RejectSystem();
        var parsed = Parse(key);
        var project = await ProjectAsync(projectId, ct);
        var (document, _) = await FindAsync(project, parsed, ct);
        if (document is null) throw NotFound(parsed.Key);
        await EnsureAllowedAsync(document.ProjectId, document.WorkItemId, PlatformPermission.View, ct);
        return document;
    }

    private async Task<(MethodDocument? Document, MethodTaskSnapshot? Task)> FindAsync(MethodProjectSnapshot project, ParsedKey parsed,
        CancellationToken ct, bool tracked = false)
    {
        var documents = tracked ? db.MethodDocuments : db.MethodDocuments.AsNoTracking();
        documents = documents.Where(x => x.ProjectId == project.ProjectId && x.OrganizationId == project.OrganizationId);
        switch (parsed.Kind)
        {
            case MethodDocumentKind.Epic:
            case MethodDocumentKind.Hu:
                return (await documents.SingleOrDefaultAsync(x => x.Kind == parsed.Kind && x.MethodKey == parsed.MethodKey, ct), null);
            case MethodDocumentKind.Prd:
            {
                var storyId = await db.MethodDocuments.AsNoTracking().Where(x => x.ProjectId == project.ProjectId
                    && x.Kind == MethodDocumentKind.Hu && x.MethodKey == parsed.MethodKey).Select(x => (Guid?)x.WorkItemId).SingleOrDefaultAsync(ct);
                if (storyId is null) return (null, null);
                return (await documents.SingleOrDefaultAsync(x => x.WorkItemId == storyId && x.Kind == MethodDocumentKind.Prd, ct), null);
            }
            default:
            {
                var snapshot = await TaskAsync(project, parsed.Key, ct);
                return (await documents.SingleOrDefaultAsync(x => x.WorkItemId == snapshot.WorkItemId && x.Kind == MethodDocumentKind.Task, ct), snapshot);
            }
        }
    }

    private async Task<MethodTaskSnapshot> TaskAsync(MethodProjectSnapshot project, string id, CancellationToken ct)
    {
        var candidates = await store.FindTasksAsync(project.ProjectId, id, ct);
        if (candidates.Count > 1) throw new MethodOperationException(409, "ambiguous_method_id", "ID do método duplicado no projeto. Resolva a duplicidade antes.");
        if (candidates.Count == 0) throw new MethodOperationException(404, "task_not_found", "Task não encontrada neste projeto.");
        var snapshot = await store.GetTaskAsync(candidates[0].WorkItemId, ct);
        if (snapshot is null || snapshot.ProjectId != project.ProjectId || !snapshot.IsMethodEligible || snapshot.IsArchived)
            throw new MethodOperationException(409, "method_task_unavailable", "Task arquivada, fora do quadro ou sem identidade única válida.");
        return snapshot;
    }

    private async Task<MethodDocumentDto> DtoAsync(MethodProjectSnapshot project, ParsedKey parsed, MethodDocument document, CancellationToken ct)
    {
        string? parentKey = null;
        var parentId = await db.WorkItems.AsNoTracking().Where(x => x.Id == document.WorkItemId).Select(x => x.ParentId).SingleOrDefaultAsync(ct);
        if (parentId is not null && document.Kind is MethodDocumentKind.Hu or MethodDocumentKind.Task)
            parentKey = await db.MethodDocuments.AsNoTracking().Where(x => x.WorkItemId == parentId
                    && x.Kind == (document.Kind == MethodDocumentKind.Hu ? MethodDocumentKind.Epic : MethodDocumentKind.Hu))
                .Select(x => x.MethodKey).SingleOrDefaultAsync(ct);
        var locked = false;
        if (document.Kind == MethodDocumentKind.Task)
            locked = (await store.GetTaskAsync(document.WorkItemId, ct))?.StatusCode == MethodStatusCodes.InDevelopment;
        var canEdit = await AllowedAsync(document.ProjectId, document.WorkItemId, PlatformPermission.Edit, ct)
            && !(locked && Actor.Role == MethodRole.Human);
        var title = MethodDocumentText.ParseHeader(document.Content).GetValueOrDefault("titulo");
        return new(parsed.Key, KindCode(document.Kind), document.WorkItemId, document.FilePath, string.IsNullOrWhiteSpace(title) ? null : title,
            document.Status, document.Version, document.ContentHash, document.UpdatedByName, document.UpdatedVia, document.UpdatedAtUtc,
            document.Content, parentKey, locked, canEdit);
    }

    private async Task<MethodProjectSnapshot> ProjectAsync(Guid projectId, CancellationToken ct)
    {
        if (Actor.ProjectId is { } scoped && scoped != projectId || !await db.Projects.AsNoTracking().AnyAsync(x => x.Id == projectId, ct))
            throw new MethodOperationException(404, "project_not_found", "Projeto não encontrado.");
        var project = await projects.SettingsAsync(projectId, ct)
            ?? throw new MethodOperationException(409, "method_not_provisioned", "Configure explicitamente o método neste projeto.");
        if (!project.Enabled) throw new MethodOperationException(403, "method_disabled", "O método está pausado ou possui configuração inválida. Pare e avise.");
        return project;
    }

    private void RejectSystem()
    {
        if (Actor.Role == MethodRole.System || Actor.AuthenticationKind == MethodAuthenticationKind.IntegrationWorker)
            throw new MethodOperationException(403, "method_role", "O papel sistema não lê nem grava documentos do método.");
    }

    private async Task<T> AtomicAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null) return await operation();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            try
            {
                var result = await operation();
                if (transaction is not null) await transaction.CommitAsync(ct);
                return result;
            }
            catch (DbUpdateException) when (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
                // Duas gravações concorrentes do mesmo documento: a segunda perde pela versão/índice único.
                throw new MethodOperationException(409, "method_document_version_conflict", "O documento mudou durante a gravação. Recarregue e tente de novo.");
            }
            catch { if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
        });
    }

    private async Task NotifyAsync(Guid itemId, string change, CancellationToken ct)
    {
        try
        {
            var boardId = await db.WorkItems.Where(x => x.Id == itemId).Select(x => x.BoardId).SingleAsync(ct);
            await realtime.BoardChangedAsync(boardId, change, itemId, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        { logger?.LogWarning("Documento do item {WorkItemId} salvo; atualização em tempo real pendente.", itemId); }
    }

    internal sealed record ParsedKey(MethodDocumentKind Kind, string Key, string? MethodKey);
    private sealed record DocumentRow(Guid Id, Guid WorkItemId, MethodDocumentKind Kind, string? MethodKey, string? FilePath, string? Status,
        int Version, string ContentHash, string UpdatedByName, string UpdatedVia, DateTimeOffset UpdatedAtUtc, string ItemTitle);

    internal static ParsedKey Parse(string key)
    {
        key = (key ?? string.Empty).Trim();
        if (key.EndsWith("/prd", StringComparison.Ordinal))
        {
            var hu = key[..^4];
            if (!IsId(hu, "HU-")) throw Invalid("Chave de PRD inválida: use HU-0001/prd.");
            return new(MethodDocumentKind.Prd, hu + "/prd", hu);
        }
        if (key == AvulsasKey) return new(MethodDocumentKind.Hu, key, key);
        if (IsId(key, "EPIC-")) return new(MethodDocumentKind.Epic, key, key);
        if (IsId(key, "HU-")) return new(MethodDocumentKind.Hu, key, key);
        if (IsId(key, "T-")) return new(MethodDocumentKind.Task, key, null);
        throw Invalid("Chave de documento inválida: use EPIC-0001, HU-0001, HU-0001/prd, AVULSAS ou T-0001.");
    }

    private static bool IsId(string value, string prefix)
        => value.Length <= 32 && value.StartsWith(prefix, StringComparison.Ordinal) && value.Length >= prefix.Length + 4
            && value[prefix.Length..].All(char.IsAsciiDigit);

    private static void ValidateFilePath(string? path)
    {
        if (path is null) return;
        if (path.Length > 400 || !path.StartsWith("docs/trabalho/", StringComparison.Ordinal) || !path.EndsWith(".md", StringComparison.Ordinal)
            || path.Contains("..", StringComparison.Ordinal) || path.Contains('\\') || path.Any(char.IsControl))
            throw Invalid("Caminho do arquivo inválido: use docs/trabalho/....md, relativo e sem '..'.");
    }

    private static string? TitleFromItem(string key, MethodDocumentKind kind, string itemTitle)
    {
        if (kind == MethodDocumentKind.Prd) return null;
        var prefix = key + " · ";
        return itemTitle.StartsWith(prefix, StringComparison.Ordinal) ? itemTitle[prefix.Length..] : itemTitle;
    }

    internal static string KindCode(MethodDocumentKind kind) => kind switch
    {
        MethodDocumentKind.Epic => "epic", MethodDocumentKind.Hu => "hu", MethodDocumentKind.Prd => "prd", _ => "task",
    };

    private static string Via(MethodActorIdentity actor)
        => actor.AuthenticationKind == MethodAuthenticationKind.UiSession ? MethodChannels.Ui : MethodChannels.Mcp;

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
    private static MethodOperationException Invalid(string message) => new(400, "invalid_method_document", message);
    private static MethodOperationException NotFound(string key) => new(404, "method_document_not_found", $"Documento {key} não encontrado neste projeto.");
}
