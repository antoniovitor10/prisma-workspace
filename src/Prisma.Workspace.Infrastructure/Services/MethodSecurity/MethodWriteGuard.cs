using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodSecurity;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Identity;
using Prisma.Workspace.Infrastructure.Identity.MethodTokens;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Services.MethodGit;

namespace Prisma.Workspace.Infrastructure.Services.MethodSecurity;

/// <summary>Barreira comum das escritas EF; contexto de método é confiável e não vem dos requests.</summary>
internal sealed class MethodWriteGuard(AppDbContext db, IOrganizationContext? organization,
    IMethodAccessContext? access, TimeProvider clock)
{
    private readonly Dictionary<Guid, MethodProjectSettings?> _settings = [];
    private MethodActorIdentity? Actor => access?.Current;
    private bool IsMethodActor => Actor?.AuthenticationKind is MethodAuthenticationKind.MethodToken or MethodAuthenticationKind.IntegrationWorker;

    internal async Task<IReadOnlyList<MethodItemChange>> ValidateAsync(CancellationToken ct)
    {
        db.ChangeTracker.DetectChanges();
        var pending = db.ChangeTracker.Entries().Where(Changed).ToArray();
        if (pending.Any(x => x.Entity is MethodAuthorizationEvent or GitAppliedFact
            && x.State is EntityState.Modified or EntityState.Deleted))
            throw Denied("method_history_immutable", "As provas do método são permanentes e não podem ser alteradas ou apagadas.");
        if (IsMethodActor) await ValidateScopeAsync(pending, ct);

        var items = db.ChangeTracker.Entries<WorkItem>().Where(Changed).ToDictionary(x => x.Entity.Id);
        foreach (var id in pending.Where(x => x.Entity is WorkItemCustomFieldValue)
            .Select(x => ((WorkItemCustomFieldValue)x.Entity).WorkItemId).Distinct())
        {
            if (items.ContainsKey(id)) continue;
            var item = db.WorkItems.Local.SingleOrDefault(x => x.Id == id)
                ?? await db.WorkItems.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id, ct);
            if (item is not null) items[id] = db.Entry(item);
        }
        var result = new List<MethodItemChange>();
        foreach (var entry in items.Values)
        {
            var item = entry.Entity;
            var created = entry.State == EntityState.Added;
            var stored = created ? null : await db.WorkItems.IgnoreQueryFilters().AsNoTracking().Where(x => x.Id == item.Id)
                .Select(x => new MethodStoredItem(x.BoardId, x.StageId, x.ParentId, x.IsArchived)).SingleOrDefaultAsync(ct);
            if (!created && stored is null) throw Conflict();
            var destination = await BoardAsync(item.BoardId, ct);
            var source = stored is null || stored.BoardId == destination.Id ? destination : await BoardAsync(stored.BoardId, ct);
            EnsureTenant(source); EnsureTenant(destination);
            if (item.StageId is { } stageId)
            {
                var stage = db.Stages.Local.SingleOrDefault(x => x.Id == stageId)
                    ?? await db.Stages.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == stageId, ct);
                if (stage is null || stage.BoardId != destination.Id || stage.ProjectId != destination.ProjectId)
                    throw Denied("method_stage_scope", "A coluna deve pertencer ao quadro e ao projeto de destino.");
            }
            var sourceSettings = await SettingsAsync(source.ProjectId, ct);
            var destinationSettings = source.ProjectId == destination.ProjectId ? sourceSettings : await SettingsAsync(destination.ProjectId, ct);
            var settings = sourceSettings ?? destinationSettings;
            if (settings is null)
            {
                if (IsMethodActor) throw Denied("method_scope", "A credencial exige um projeto configurado no método.");
                continue;
            }
            var fields = pending.Where(x => x.Entity is WorkItemCustomFieldValue v && v.WorkItemId == item.Id).ToArray();
            var currentId = await CurrentFieldAsync(item.Id, settings.MethodIdFieldId, fields, ct);
            foreach (var field in fields)
            {
                var value = (WorkItemCustomFieldValue)field.Entity;
                var fieldProject = await db.ProjectCustomFields.IgnoreQueryFilters().Where(x => x.Id == value.FieldDefinitionId)
                    .Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct);
                if (fieldProject != destination.ProjectId && !(field.State == EntityState.Deleted && fieldProject == source.ProjectId))
                    throw Denied("method_field_scope", "O campo personalizado não pertence ao projeto da tarefa.");
                if (value.FieldDefinitionId == settings.PrFieldId && ChangedValue(field)
                    && currentId is { Length: >= 6 and <= 32 } && currentId.StartsWith("T-", StringComparison.Ordinal)
                    && currentId[2..].All(char.IsAsciiDigit))
                {
                    var dedicatedLink = pending.Any(x => x.Entity is TaskGitLink link && link.ProjectId == settings.ProjectId && link.WorkItemId == item.Id);
                    if (!dedicatedLink)
                        throw Denied("structured_pr_required", "Vincule o PR pela operação dedicada para preservar o vínculo estruturado.");
                }
            }
            var identityChanged = fields.Any(x => ((WorkItemCustomFieldValue)x.Entity).FieldDefinitionId == settings.MethodIdFieldId
                    && (x.State is EntityState.Added or EntityState.Deleted || x.Property(nameof(WorkItemCustomFieldValue.Value)).IsModified))
                || stored is not null && (stored.BoardId != item.BoardId || stored.ParentId != item.ParentId || stored.IsArchived != item.IsArchived);
            var writesStage = created || entry.Property(x => x.StageId).IsModified && stored!.StageId != item.StageId
                || stored is not null && stored.BoardId != item.BoardId;
            if (!created && entry.Property(x => x.StageId).IsModified
                && entry.Property(x => x.StageId).OriginalValue != stored!.StageId)
                throw Conflict();
            if (!created && (entry.Property(x => x.CompletedAt).IsModified || entry.Property(x => x.WorkflowStatusId).IsModified)
                && entry.Property(x => x.StageId).OriginalValue != stored!.StageId) throw Conflict();
            if (!created && entry.Property(x => x.BoardId).OriginalValue != stored!.BoardId) throw Conflict();
            var reason = Reason(item.Id);
            if (created || writesStage || identityChanged)
            {
                if (IsMethodActor)
                {
                    if (organization is null || organization.OrganizationId != Actor!.OrganizationId || access is null)
                        throw Denied("method_tenant_scope", "Organização inválida para a credencial.");
                    var permissions = new PermissionService(db, organization, access, new MethodPermissionEvaluator(db, clock));
                    var policy = new MethodTransitionPolicy(access, new EfMethodGitStore(db, organization, access, permissions, clock), permissions);
                    await policy.EnsureAllowedAsync(new MethodMutation(source.ProjectId, source.Id, item.Id, stored?.StageId,
                        item.StageId, reason, created, identityChanged, destination.ProjectId, destination.Id,
                        db.MethodExecution?.Request.GitFact), ct);
                    if (Actor.Role != MethodRole.Human && created && item.ParentId is not null)
                        throw Denied("method_top_level", "Tasks do método devem ser itens de topo.");
                }
                else if (Actor?.IsInteractiveHuman != true && settings.Enabled
                    && settings.StageMappings.Any(x => x.StatusCode == MethodStatusCodes.InDevelopment && x.StageId == item.StageId)
                    && (created || writesStage))
                    throw Denied("interactive_authorization_required", "Iniciar execução no método exige sessão humana da UI.");
            }
            result.Add(new(entry, source, destination, settings, stored, created, writesStage, identityChanged, currentId, reason));
            if (sourceSettings is not null && destinationSettings is not null && source.ProjectId != destination.ProjectId)
            {
                var destinationId = await CurrentFieldAsync(item.Id, destinationSettings.MethodIdFieldId, fields, ct);
                result.Add(new(entry, source, destination, destinationSettings, stored, created, writesStage, true, destinationId, reason));
            }
        }
        return result;
    }

    private async Task ValidateScopeAsync(EntityEntry[] entries, CancellationToken ct)
    {
        var actor = Actor!;
        var touchesTask = entries.Any(x => x.Entity is WorkItem or WorkItemLink
            || x.Metadata.FindProperty("WorkItemId") is not null);
        if (touchesTask)
        {
            if (organization is null || actor.ProjectId is null)
                throw Denied("method_project_scope", "A operação exige organização e projeto autenticados.");
            var permissionService = new PermissionService(db, organization, access, new MethodPermissionEvaluator(db, clock));
            var project = await new EfMethodGitStore(db, organization, access!, permissionService, clock).GetProjectAsync(actor.ProjectId.Value, ct);
            if (project?.Enabled != true)
                throw Denied("method_disabled", "O método foi pausado ou sua configuração ficou inválida. Pare e avise.");
        }
        foreach (var entry in entries)
        {
            if (entry.Entity is MethodProjectSettings or MethodStageMapping or MethodPrincipal or MethodApiToken or MethodTaskState or MethodAuthorizationEvent or GitAppliedFact or AuditLog)
                throw Denied("method_authority_write", "A credencial não pode modificar diretamente configuração, autorização ou provas do método.");
            if (actor.Role != MethodRole.Human && entry.Entity is Board or Stage or Project or ProjectCustomFieldDefinition
                or WorkflowStatus or WorkflowTransition or Organization or OrganizationMember or PermissionGrant or ProjectMember or ProjectTeam)
                throw Denied("method_structure", "Agente e sistema não podem alterar estrutura, permissões ou mapeamento para contornar o método.");
            if (entry.Entity is WorkItem workItem)
            {
                if (entry.State == EntityState.Added)
                    await RequireItemPermissionAsync(workItem.Id, PlatformPermission.Create, ct);
                else
                {
                    var movement = new[] { nameof(WorkItem.StageId), nameof(WorkItem.WorkflowStatusId), nameof(WorkItem.CompletedAt),
                        nameof(WorkItem.StartDate), nameof(WorkItem.UpdatedAt), nameof(WorkItem.Position), nameof(WorkItem.BacklogRank) };
                    if (entry.Properties.Any(x => x.IsModified && !movement.Contains(x.Metadata.Name)))
                        await RequireItemPermissionAsync(workItem.Id, PlatformPermission.Edit, ct);
                    if (entry.Property(nameof(WorkItem.Position)).IsModified)
                        await RequireItemPermissionAsync(workItem.Id, PlatformPermission.ChangeStatus, ct);
                }
                continue; // Origem/destino e matriz são validados juntos.
            }
            if (entry.Entity is Comment comment && comment.UserId != actor.ActorId)
                throw Denied("method_comment_actor", "O comentário deve preservar a autoria autenticada.");
            if (entry.Entity is Comment ownComment) ownComment.UserName = actor.DisplayName;
            if (entry.Entity is StageHistory history && entry.State == EntityState.Added)
            {
                history.ActorId = actor.ActorId; history.ActorName = actor.DisplayName;
            }
            if (entry.Entity is TaskEvent taskEvent && entry.State == EntityState.Added) taskEvent.ActorId = actor.ActorId;
            if (entry.Entity is Tag tag)
            {
                if (tag.OrganizationId != actor.OrganizationId || !entries.Any(x => x.Entity is WorkItemTag wt && wt.TagId == tag.Id))
                    throw Denied("method_tag_scope", "Tags do método precisam estar associadas a uma task do projeto.");
                continue;
            }
            if (entry.Entity is WorkItemLink link)
            {
                await RequireItemPermissionAsync(link.SourceWorkItemId, PlatformPermission.Edit, ct);
                await RequireItemPermissionAsync(link.TargetWorkItemId, PlatformPermission.View, ct);
                continue;
            }
            var workItemProperty = entry.Metadata.FindProperty("WorkItemId");
            if (workItemProperty is not null && entry.Property("WorkItemId").CurrentValue is Guid itemId)
            {
                var permission = entry.Entity switch
                {
                    Comment => PlatformPermission.Comment,
                    WorkItemAssignee => PlatformPermission.Assign,
                    StageHistory => PlatformPermission.ChangeStatus,
                    TaskEvent e when e.Kind.StartsWith("comment", StringComparison.Ordinal) => PlatformPermission.Comment,
                    TaskEvent e when e.Kind is "stage_changed" or "stage_reclassified" => PlatformPermission.ChangeStatus,
                    _ => PlatformPermission.Edit
                };
                await RequireItemPermissionAsync(itemId, permission, ct);
                continue;
            }
            var projectId = entry.Entity is Project project ? project.Id
                : entry.Metadata.FindProperty("ProjectId") is not null ? entry.Property("ProjectId").CurrentValue as Guid? : null;
            if (projectId is not null && projectId == actor.ProjectId) continue;
            throw Denied("method_project_scope", "A credencial do método não permite escrever fora do seu projeto.");
        }
    }

    private async Task RequireItemPermissionAsync(Guid id, PlatformPermission permission, CancellationToken ct)
    {
        var actor = Actor!;
        var added = db.ChangeTracker.Entries<WorkItem>().SingleOrDefault(x => x.State == EntityState.Added && x.Entity.Id == id);
        var evaluator = new MethodPermissionEvaluator(db, clock);
        if (added is not null)
        {
            var board = await BoardAsync(added.Entity.BoardId, ct);
            if (board.ProjectId == actor.ProjectId && board.OrganizationId == actor.OrganizationId
                && await evaluator.HasAsync(actor, PlatformPermission.Create, PermissionScope.Project, board.ProjectId, ct)) return;
        }
        else if (await evaluator.HasAsync(actor, permission, PermissionScope.WorkItem, id, ct)) return;
        throw Denied("method_permission", "A credencial não possui permissão efetiva para esta tarefa.");
    }

    private async Task<MethodBoardScope> BoardAsync(Guid id, CancellationToken ct)
    {
        var local = db.Boards.Local.SingleOrDefault(x => x.Id == id);
        if (local is not null) return new(local.Id, local.ProjectId, local.OrganizationId);
        return await db.Boards.IgnoreQueryFilters().AsNoTracking().Where(x => x.Id == id)
            .Select(x => new MethodBoardScope(x.Id, x.ProjectId, x.OrganizationId)).SingleOrDefaultAsync(ct)
            ?? throw Denied("method_board_scope", "O quadro da tarefa não está disponível.");
    }

    private void EnsureTenant(MethodBoardScope board)
    {
        if (organization?.OrganizationId is { } tenant && board.OrganizationId != tenant
            || IsMethodActor && (board.OrganizationId != Actor!.OrganizationId || board.ProjectId != Actor.ProjectId))
            throw Denied("method_project_scope", "A tarefa não pertence ao escopo autenticado.");
    }

    internal async Task<MethodProjectSettings?> SettingsAsync(Guid projectId, CancellationToken ct)
    {
        var local = db.Set<MethodProjectSettings>().Local.SingleOrDefault(x => x.ProjectId == projectId);
        if (local is not null) return local;
        if (_settings.TryGetValue(projectId, out var cached)) return cached;
        var settings = await db.Set<MethodProjectSettings>().IgnoreQueryFilters().Include(x => x.StageMappings)
            .SingleOrDefaultAsync(x => x.ProjectId == projectId, ct);
        _settings[projectId] = settings;
        return settings;
    }

    private async Task<string?> CurrentFieldAsync(Guid id, Guid fieldId, EntityEntry[] entries, CancellationToken ct)
    {
        var change = entries.SingleOrDefault(x => ((WorkItemCustomFieldValue)x.Entity).FieldDefinitionId == fieldId);
        if (change is not null) return change.State == EntityState.Deleted ? null : ((WorkItemCustomFieldValue)change.Entity).Value;
        return await db.WorkItemCustomFieldValues.IgnoreQueryFilters().Where(x => x.WorkItemId == id && x.FieldDefinitionId == fieldId)
            .Select(x => x.Value).SingleOrDefaultAsync(ct);
    }

    private string Reason(Guid id)
    {
        if (db.MethodExecution?.Request is { } request && request.WorkItemId == id) return request.Reason;
        foreach (var entry in db.ChangeTracker.Entries<TaskEvent>().Where(x => x.State == EntityState.Added && x.Entity.WorkItemId == id))
        {
            try
            {
                using var json = JsonDocument.Parse(entry.Entity.Payload ?? "{}");
                if (json.RootElement.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String)
                    return reason.GetString() ?? string.Empty;
            }
            catch (JsonException) { }
        }
        return string.Empty;
    }

    internal static bool Changed(EntityEntry entry) => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted;
    private static bool ChangedValue(EntityEntry entry)
    {
        var value = ((WorkItemCustomFieldValue)entry.Entity).Value;
        if (entry.State == EntityState.Added) return !string.IsNullOrWhiteSpace(value);
        var original = entry.Property(nameof(WorkItemCustomFieldValue.Value)).OriginalValue as string;
        return entry.State == EntityState.Deleted ? !string.IsNullOrWhiteSpace(original) : original != value;
    }
    private static MethodOperationException Conflict() => new(409, "stale_task", "A tarefa mudou. Atualize os dados antes de tentar novamente.");
    private static MethodOperationException Denied(string code, string message) => new(403, code, message);
}
