using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodSecurity;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;

namespace Prisma.Workspace.Infrastructure.Services.MethodSecurity;

public sealed class MethodTaskOperations(AppDbContext db, IMethodAccessContext access, IMethodGitStore store,
    IMethodProjectOperations projects, IMethodMoveService movement, IPermissionService permissions,
    IBoardRealtimeNotifier realtime, TimeProvider? clock = null, ILogger<MethodTaskOperations>? logger = null,
    IAutomationExecutor? automation = null) : IMethodTaskOperations
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private MethodActorIdentity Actor => access.Current
        ?? throw new MethodOperationException(401, "method_identity_required", "Entre novamente para estabelecer sua identidade.");

    public async Task<IReadOnlyList<MethodTaskDto>> ListAsync(Guid projectId, string? status, bool next, CancellationToken ct)
    {
        var project = await ProjectAsync(projectId, ct);
        if (status is not null && !MethodStatusCodes.IsValid(status)) throw Invalid("Status do método inválido.");
        var items = await Items(project).OrderBy(x => x.Position).ThenBy(x => x.Id).ToListAsync(ct);
        var result = new List<MethodTaskDto>();
        foreach (var item in items)
        {
            var snapshot = await store.GetTaskAsync(item.Id, ct);
            if (snapshot is null || snapshot.ProjectId != projectId || status is not null && snapshot.StatusCode != status) continue;
            if (!await permissions.HasAsync(Actor.ActorId, PlatformPermission.View, PermissionScope.WorkItem, item.Id, ct)) continue;
            var dto = await DtoAsync(project, item, snapshot, ct);
            if (next && (dto.Status != MethodStatusCodes.InDevelopment || !dto.Authorized
                || !await DependenciesCompletedAsync(item.Id, project, ct))) continue;
            result.Add(dto);
        }
        return result;
    }

    public async Task<MethodTaskDto> GetAsync(Guid projectId, string id, CancellationToken ct)
    {
        var project = await ProjectAsync(projectId, ct);
        var (item, state) = await TaskAsync(project, id, ct);
        return await DtoAsync(project, item, state, ct);
    }

    public async Task<MethodTaskDto> CreateAsync(Guid projectId, CreateMethodTaskRequest request, CancellationToken ct)
    {
        ValidateId(request.Id); ValidateContent(request.Title, request.Hu, request.FilePath, request.Covers, request.Risk);
        if (!MethodStatusCodes.IsValid(request.Status)) throw Invalid("Status do método inválido.");
        var created = await AtomicAsync(async () =>
        {
            var project = await ProjectAsync(projectId, ct);
            await permissions.EnsureAsync(Actor.ActorId, PlatformPermission.Create, PermissionScope.Project, projectId, ct);
            if (await db.WorkItemCustomFieldValues.IgnoreQueryFilters().AnyAsync(x => x.FieldDefinitionId == project.MethodIdFieldId
                && x.Value == request.Id && x.WorkItem.Board.ProjectId == projectId
                && x.WorkItem.Board.OrganizationId == project.OrganizationId, ct))
                throw new MethodOperationException(409, "ambiguous_method_id", "ID do método já utilizado no projeto, inclusive por card arquivado ou em outro quadro.");
            var item = new WorkItem { Id = Guid.NewGuid(), BoardId = project.BoardId,
                StageId = project.StageMappings[request.Status], ParentId = null,
                Title = CardTitle(request.Id, request.Title), Position = 100, CreatedBy = Actor.ActorId,
                CreatedAt = _clock.GetUtcNow(), UpdatedAt = _clock.GetUtcNow() };
            var stage = await db.Stages.SingleAsync(x => x.Id == item.StageId, ct);
            item.WorkflowStatusId = stage.WorkflowStatusId;
            item.CompletedAt = stage.Category == StageCategory.Done ? _clock.GetUtcNow() : null;
            if (stage.Category == StageCategory.InProgress) item.StartDate = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
            db.Add(item);
            Field(item, project.MethodIdFieldId, request.Id);
            await ContentAsync(project, item, request.Title, request.Hu, request.FilePath, request.Covers, request.Risk, ct);
            db.TaskEvents.Add(TaskEvent.Registrar(item.Id, Actor.ActorId, "created", JsonSerializer.Serialize(new { title = item.Title })));
            await db.SaveChangesAsync(ct);
            var snapshot = await store.GetTaskAsync(item.Id, ct)
                ?? throw new MethodOperationException(409, "method_task_unavailable", "A task não recebeu identidade única válida.");
            return await DtoAsync(project, item, snapshot, ct);
        }, ct);
        await NotifyAsync(created.WorkItemId, "workItemCreated", ct);
        return created;
    }

    public async Task<MethodTaskDto> UpdateAsync(Guid projectId, string id, UpdateMethodTaskRequest request, CancellationToken ct)
    {
        ValidateContent(request.Title, request.Hu, request.FilePath, request.Covers, request.Risk);
        var updated = await AtomicAsync(async () =>
        {
            var project = await ProjectAsync(projectId, ct);
            var (item, _) = await TaskAsync(project, id, ct);
            await permissions.EnsureAsync(Actor.ActorId, PlatformPermission.Edit, PermissionScope.WorkItem, item.Id, ct);
            await ContentAsync(project, item, request.Title, request.Hu, request.FilePath, request.Covers, request.Risk, ct);
            item.UpdatedAt = _clock.GetUtcNow();
            db.TaskEvents.Add(TaskEvent.Registrar(item.Id, Actor.ActorId, "updated", JsonSerializer.Serialize(new { title = item.Title })));
            await db.SaveChangesAsync(ct);
            return await DtoAsync(project, item, (await store.GetTaskAsync(item.Id, ct))!, ct);
        }, ct);
        await NotifyAsync(updated.WorkItemId, "workItemUpdated", ct);
        return updated;
    }

    public async Task<MethodTaskDto> MoveAsync(Guid projectId, string id, MoveMethodTaskRequest request, CancellationToken ct)
    {
        var project = await ProjectAsync(projectId, ct);
        if (!project.StageMappings.TryGetValue(request.NewStatus, out var stage)) throw Invalid("Status de destino inválido.");
        var (item, _) = await TaskAsync(project, id, ct);
        var result = await movement.MoveAsync(new(item.Id, stage, request.Reason, item.Position), ct);
        if (result.Applied && automation is not null)
            await automation.ExecuteStageEnteredAsync(item.Id, stage, Actor.ActorId, ct);
        return await GetAsync(projectId, id, ct);
    }

    public async Task<MethodCommentDto> CommentAsync(Guid projectId, string id, MethodCommentRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Trim().Length > 2000) throw Invalid("Escreva comentário de até 2.000 caracteres.");
        var project = await ProjectAsync(projectId, ct);
        var (item, _) = await TaskAsync(project, id, ct);
        await permissions.EnsureAsync(Actor.ActorId, PlatformPermission.Comment, PermissionScope.WorkItem, item.Id, ct);
        var comment = new Comment { Id = Guid.NewGuid(), WorkItemId = item.Id, Content = request.Text.Trim(),
            UserId = Actor.ActorId, UserName = Actor.DisplayName, CreatedAt = _clock.GetUtcNow() };
        db.Add(comment);
        db.TaskEvents.Add(TaskEvent.Registrar(item.Id, Actor.ActorId, "comment_added", JsonSerializer.Serialize(new { commentId = comment.Id, text = comment.Content })));
        await db.SaveChangesAsync(ct);
        await NotifyAsync(item.Id, "workItemUpdated", ct);
        return new(comment.Id, item.Id, comment.Content, Actor.DisplayName, comment.CreatedAt);
    }

    public async Task<ResumeMethodTaskApprovalDto> ApproveResumeAsync(Guid projectId, Guid itemId, ResumeMethodTaskRequest request, CancellationToken ct)
    {
        if (!Actor.IsInteractiveHuman) throw new MethodOperationException(403, "interactive_resume_required", "Liberar retomada exige a pessoa autenticada na UI; tokens não concedem essa autorização.");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000) throw Invalid("Informe o motivo da liberação, com até 1.000 caracteres.");
        var result = await AtomicAsync(async () =>
        {
            var project = await ProjectAsync(projectId, ct);
            await permissions.EnsureAsync(Actor.ActorId, PlatformPermission.ChangeStatus, PermissionScope.WorkItem, itemId, ct);
            var snapshot = await store.GetTaskAsync(itemId, ct);
            if (snapshot is null || snapshot.ProjectId != projectId || snapshot.StatusCode != MethodStatusCodes.Blocked
                || snapshot.CycleId != request.CycleId || snapshot.BlockingEventId != request.BlockingEventId
                || snapshot.BlockingPrincipalId is null || snapshot.BlockingOriginStatusCode != MethodStatusCodes.InDevelopment
                || snapshot.ActiveAuthorizationEventId is null)
                throw new MethodOperationException(409, "stale_block", "O bloqueio não pertence à execução autorizada atual. Atualize a task antes de liberar.");
            var state = await db.Set<MethodTaskState>().SingleAsync(x => x.ProjectId == projectId && x.WorkItemId == itemId, ct);
            if (snapshot.ResumeApprovalEventId is { } existing)
                return new ResumeMethodTaskApprovalDto(existing, itemId, request.BlockingEventId, state.CycleId);
            var taskEvent = TaskEvent.Registrar(itemId, Actor.ActorId, "method_resume_approved", JsonSerializer.Serialize(new
            {
                projectId, methodTaskId = snapshot.MethodTaskId, cycleId = state.CycleId,
                blockingEventId = state.BlockingEventId, reason = request.Reason.Trim()
            }));
            var approval = new MethodAuthorizationEvent { Id = Guid.NewGuid(), OrganizationId = project.OrganizationId,
                ProjectId = projectId, WorkItemId = itemId, CycleId = state.CycleId, EventKind = "ResumeAllowed",
                RelatedEventId = request.BlockingEventId, ActorUserId = Actor.ActorId, ActorName = Actor.DisplayName,
                TaskEventId = taskEvent.Id, Reason = request.Reason.Trim(), CreatedAtUtc = _clock.GetUtcNow() };
            db.Add(taskEvent); db.Add(approval); state.ResumeApprovalEventId = approval.Id;
            await db.SaveChangesAsync(ct);
            return new ResumeMethodTaskApprovalDto(approval.Id, itemId, request.BlockingEventId, state.CycleId);
        }, ct);
        await NotifyAsync(itemId, "workItemUpdated", ct);
        return result;
    }

    private async Task<MethodProjectSnapshot> ProjectAsync(Guid projectId, CancellationToken ct)
    {
        var project = await projects.SettingsAsync(projectId, ct)
            ?? throw new MethodOperationException(409, "method_not_provisioned", "Configure explicitamente o método neste projeto.");
        if (!project.Enabled) throw new MethodOperationException(403, "method_disabled", "O método está pausado ou possui configuração inválida. Pare e avise.");
        return project;
    }

    private IQueryable<WorkItem> Items(MethodProjectSnapshot project) => db.WorkItems
        .Include(x => x.CustomFieldValues).Include(x => x.WorkItemTags).ThenInclude(x => x.Tag)
        .Where(x => x.BoardId == project.BoardId && x.Board.ProjectId == project.ProjectId
            && x.Board.OrganizationId == project.OrganizationId && x.ParentId == null && !x.IsArchived);

    private async Task<(WorkItem, MethodTaskSnapshot)> TaskAsync(MethodProjectSnapshot project, string id, CancellationToken ct)
    {
        ValidateId(id);
        var candidates = await store.FindTasksAsync(project.ProjectId, id, ct);
        if (candidates.Count > 1) throw new MethodOperationException(409, "ambiguous_method_id", "ID do método duplicado no projeto, inclusive em cards arquivados. Resolva a duplicidade sem renumerar automaticamente.");
        if (candidates.Count == 0) throw new MethodOperationException(404, "task_not_found", "Task não encontrada neste projeto.");
        var snapshot = await store.GetTaskAsync(candidates[0].WorkItemId, ct)
            ?? throw new MethodOperationException(409, "method_task_unavailable", "Task arquivada, fora do quadro ou sem identidade única válida.");
        var item = await Items(project).SingleOrDefaultAsync(x => x.Id == snapshot.WorkItemId, ct)
            ?? throw new MethodOperationException(404, "task_not_found", "Task não encontrada no quadro do método.");
        await permissions.EnsureAsync(Actor.ActorId, PlatformPermission.View, PermissionScope.WorkItem, item.Id, ct);
        return (item, snapshot);
    }

    private async Task<MethodTaskDto> DtoAsync(MethodProjectSnapshot project, WorkItem item, MethodTaskSnapshot state, CancellationToken ct)
    {
        string? Value(string name) => project.Fields?.TryGetValue(name, out var field) == true
            ? item.CustomFieldValues.SingleOrDefault(x => x.FieldDefinitionId == field)?.Value : null;
        var dependencies = await DependencyIds(project, item.Id).ToListAsync(ct);
        var dependencyIds = new List<string>();
        foreach (var dependency in dependencies)
        {
            var target = await permissions.HasAsync(Actor.ActorId, PlatformPermission.View, PermissionScope.WorkItem, dependency, ct)
                ? await store.GetTaskAsync(dependency, ct) : null;
            dependencyIds.Add(target?.ProjectId == project.ProjectId ? target.MethodTaskId : "dependência indisponível");
        }
        var canResume = state.StatusCode == MethodStatusCodes.Blocked && state.BlockingOriginStatusCode == MethodStatusCodes.InDevelopment
            && state.ActiveAuthorizationEventId is not null && state.ResumeApprovalEventId is not null
            && Actor.Role == MethodRole.Agent && state.BlockingPrincipalId == Actor.PrincipalId;
        var prefix = state.MethodTaskId + " · ";
        return new(item.Id, state.MethodTaskId, item.Title.StartsWith(prefix, StringComparison.Ordinal) ? item.Title[prefix.Length..] : item.Title,
            state.StatusCode, Value("HU"), Value("Arquivo"), Split(Value("Cobre")), Value("Risco"), Value("PR"), dependencyIds,
            state.ActiveAuthorizationEventId is not null, canResume, state.CycleId, state.BlockingEventId,
            state.BlockingPrincipalId, state.BlockingOriginStatusCode, state.ResumeApprovalEventId);
    }

    private async Task<bool> DependenciesCompletedAsync(Guid itemId, MethodProjectSnapshot project, CancellationToken ct)
    {
        var ids = await DependencyIds(project, itemId).ToListAsync(ct);
        foreach (var id in ids)
        {
            if (!await permissions.HasAsync(Actor.ActorId, PlatformPermission.View, PermissionScope.WorkItem, id, ct)) return false;
            var dependency = await store.GetTaskAsync(id, ct);
            if (dependency is null || dependency.ProjectId != project.ProjectId || dependency.StatusCode != MethodStatusCodes.Completed) return false;
        }
        return true;
    }

    // Só as chaves das relações do card já validado. Inclui bloqueio de outro projeto sem carregar seus dados.
    private IQueryable<Guid> DependencyIds(MethodProjectSnapshot project, Guid itemId) => db.WorkItemLinks.IgnoreQueryFilters().AsNoTracking()
        .Where(x => x.SourceWorkItemId == itemId && x.Type == WorkItemLinkType.DependsOn
                && x.SourceWorkItem.Board.ProjectId == project.ProjectId && x.SourceWorkItem.Board.OrganizationId == project.OrganizationId
            || x.TargetWorkItemId == itemId && x.Type == WorkItemLinkType.Blocks
                && x.TargetWorkItem.Board.ProjectId == project.ProjectId && x.TargetWorkItem.Board.OrganizationId == project.OrganizationId)
        .Select(x => x.SourceWorkItemId == itemId ? x.TargetWorkItemId : x.SourceWorkItemId);

    private async Task ContentAsync(MethodProjectSnapshot project, WorkItem item, string title, string hu,
        string file, IReadOnlyList<string> covers, string risk, CancellationToken ct)
    {
        var id = item.CustomFieldValues.Single(x => x.FieldDefinitionId == project.MethodIdFieldId).Value!;
        item.Title = CardTitle(id, title);
        var oldHu = item.CustomFieldValues.SingleOrDefault(x => x.FieldDefinitionId == project.HuFieldId)?.Value;
        Field(item, project.HuFieldId, hu.Trim());
        foreach (var pair in new[] { ("Arquivo", file), ("Cobre", string.Join(", ", covers)), ("Risco", risk) })
            Field(item, project.Fields![pair.Item1], pair.Item2.Trim());
        var tag = await db.Tags.SingleOrDefaultAsync(x => x.Name == hu.Trim() && x.OrganizationId == project.OrganizationId, ct);
        if (tag is null) { tag = Tag.Criar(hu.Trim(), null); tag.OrganizationId = project.OrganizationId; db.Add(tag); }
        if (!await db.ProjectTags.AnyAsync(x => x.ProjectId == project.ProjectId && x.TagId == tag.Id, ct)
            && !db.ProjectTags.Local.Any(x => x.ProjectId == project.ProjectId && x.TagId == tag.Id))
            db.ProjectTags.Add(new ProjectTag { ProjectId = project.ProjectId, TagId = tag.Id });
        if (oldHu != hu.Trim())
            foreach (var old in item.WorkItemTags.Where(x => x.Tag is not null && x.Tag.Name == oldHu).ToArray()) db.Remove(old);
        if (!item.WorkItemTags.Any(x => x.TagId == tag.Id)) item.WorkItemTags.Add(new WorkItemTag { WorkItemId = item.Id, TagId = tag.Id });
    }

    private void Field(WorkItem item, Guid fieldId, string? value)
    {
        var field = item.CustomFieldValues.SingleOrDefault(x => x.FieldDefinitionId == fieldId);
        if (field is null) { field = new WorkItemCustomFieldValue { WorkItemId = item.Id, FieldDefinitionId = fieldId }; item.CustomFieldValues.Add(field); }
        field.Value = value; field.UpdatedBy = Actor.ActorId; field.UpdatedAt = _clock.GetUtcNow();
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
        { logger?.LogWarning("Tarefa {WorkItemId} salva; atualização em tempo real pendente.", itemId); }
    }

    private static void ValidateId(string id)
    { if (id is not { Length: >= 6 and <= 32 } || !id.StartsWith("T-", StringComparison.Ordinal) || !id[2..].All(char.IsAsciiDigit)) throw Invalid("Use ID do método no formato T-0001, com até 32 caracteres."); }
    private static void ValidateContent(string title, string hu, string file, IReadOnlyList<string> covers, string risk)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 160) throw Invalid("Informe título de até 160 caracteres.");
        if (hu is not { Length: >= 7 and <= 32 } || !hu.StartsWith("HU-", StringComparison.Ordinal) || !hu[3..].All(char.IsAsciiDigit)) throw Invalid("Informe a HU no formato HU-0001.");
        if (string.IsNullOrWhiteSpace(file) || file.Length > 2000 || covers is null || covers.Count > 100
            || covers.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 200) || string.IsNullOrWhiteSpace(risk) || risk.Length > 2000)
            throw Invalid("Informe arquivo, critérios cobertos e risco com tamanho válido.");
    }
    private static string CardTitle(string id, string title) => $"{id} · {title.Trim()}";
    private static string[] Split(string? value) => (value ?? "").Split(new[] { ',', ';', '\n' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    private static MethodOperationException Invalid(string message) => new(400, "invalid_method_task", message);
}
