using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Services.MethodGit;

namespace Prisma.Workspace.Infrastructure.Services.MethodSecurity;

/// <summary>Uma projeção de provas/histórico para todos os movimentos, inclusive os handlers legados.</summary>
internal sealed class MethodMovementRecorder(AppDbContext db, IMethodAccessContext? access, IAuditContext? audit, TimeProvider clock)
{
    private MethodActorIdentity? Actor => access?.Current;
    private readonly DateTimeOffset _now = clock.GetUtcNow();
    private DateTimeOffset Now => _now;

    internal async Task PrepareAsync(IReadOnlyList<MethodItemChange> changes, CancellationToken ct)
    {
        foreach (var board in db.ChangeTracker.Entries<Board>().Where(x => x.State == EntityState.Deleted).ToArray())
        {
            var settings = await db.Set<MethodProjectSettings>().IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => x.BoardId == board.Entity.Id && x.OrganizationId == board.Entity.OrganizationId, ct);
            if (settings is null) continue;
            settings.Enabled = false; settings.BoardId = null; settings.UpdatedAtUtc = Now;
        }
        await AdoptSettingsAsync(ct);
        foreach (var change in changes) await RecordAsync(change, ct);
        if (Actor is not null)
        {
            foreach (var row in db.ChangeTracker.Entries<TaskEvent>().Where(x => x.State == EntityState.Added).Select(x => x.Entity))
            {
                var payload = Payload(row);
                payload["authenticationKind"] = JsonNamingPolicy.CamelCase.ConvertName(Actor.AuthenticationKind.ToString());
                payload["principalId"] = JsonValue.Create(Actor.PrincipalId); payload["methodRole"] = Actor.Role.ToCode();
                payload["channel"] = Actor.Channel; payload["correlationId"] = Actor.CorrelationId;
                payload["actorId"] = Actor.ActorId; payload["actorName"] = Actor.DisplayName;
                row.ActorId = Actor.ActorId; row.Payload = payload.ToJsonString();
            }
        }
    }

    private async Task AdoptSettingsAsync(CancellationToken ct)
    {
        foreach (var entry in db.ChangeTracker.Entries<MethodProjectSettings>().Where(x => x.State == EntityState.Added
            || x.State == EntityState.Modified && (x.Property(y => y.BoardId).IsModified
                || x.Property(y => y.MethodIdFieldId).IsModified || x.Property(y => y.Enabled).IsModified)).ToArray())
        {
            var settings = entry.Entity;
            var associationChanged = entry.State == EntityState.Added || entry.Property(x => x.BoardId).IsModified
                || entry.Property(x => x.MethodIdFieldId).IsModified;
            var states = await db.Set<MethodTaskState>().IgnoreQueryFilters().Where(x => x.ProjectId == settings.ProjectId
                && x.OrganizationId == settings.OrganizationId).ToListAsync(ct);
            if (associationChanged)
                foreach (var state in states)
                {
                    Revoke(state, "Quadro ou identidade da configuração do método alterados.");
                    ClearProofs(state); state.MethodTaskId = null;
                    state.CycleId = Guid.NewGuid(); state.LastObservedFactId = null;
                }
            if (settings.BoardId is not { } boardId) continue;
            // A adoção lê o campo real, inclusive em outros quadros e arquivados, sem escolher duplicatas.
            var rows = await db.WorkItemCustomFieldValues.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.FieldDefinitionId == settings.MethodIdFieldId
                    && x.WorkItem.Board.ProjectId == settings.ProjectId
                    && x.WorkItem.Board.OrganizationId == settings.OrganizationId)
                .Select(x => new { x.WorkItemId, x.Value, x.WorkItem.BoardId, x.WorkItem.ParentId, x.WorkItem.IsArchived }).ToListAsync(ct);
            foreach (var group in rows.Where(x => ValidId(x.Value)).GroupBy(x => x.Value!, StringComparer.Ordinal))
            {
                if (group.Count() != 1)
                {
                    foreach (var duplicate in states.Where(x => x.MethodTaskId == group.Key))
                    {
                        Revoke(duplicate, "ID do método duplicado no projeto, inclusive em cards arquivados.");
                        ClearProofs(duplicate); duplicate.MethodTaskId = null;
                        duplicate.CycleId = Guid.NewGuid(); duplicate.LastObservedFactId = null;
                    }
                    continue;
                }
                var row = group.Single();
                if (row.BoardId != boardId || row.ParentId is not null || row.IsArchived) continue;
                var state = states.SingleOrDefault(x => x.WorkItemId == row.WorkItemId);
                if (state is null)
                {
                    state = new MethodTaskState { ProjectId = settings.ProjectId, OrganizationId = settings.OrganizationId,
                        WorkItemId = row.WorkItemId, CycleId = Guid.NewGuid(), MethodTaskId = row.Value };
                    states.Add(state); db.Add(state);
                }
                else if (state.MethodTaskId is null) state.MethodTaskId = row.Value;
                // Jamais deriva autorização retroativa da coluna, de comentários ou de StageHistory legado.
            }
        }
    }

    private async Task RecordAsync(MethodItemChange change, CancellationToken ct)
    {
        var item = change.Entry.Entity;
        var settings = change.Settings;
        var state = db.Set<MethodTaskState>().Local.SingleOrDefault(x => x.ProjectId == settings.ProjectId && x.WorkItemId == item.Id)
            ?? await db.Set<MethodTaskState>().IgnoreQueryFilters().SingleOrDefaultAsync(x => x.ProjectId == settings.ProjectId
                && x.WorkItemId == item.Id && x.OrganizationId == settings.OrganizationId, ct);
        var active = change.Entry.State != EntityState.Deleted && change.Destination.ProjectId == settings.ProjectId
            && settings.BoardId == change.Destination.Id && item.ParentId is null && !item.IsArchived && ValidId(change.MethodId);
        if (active && await DuplicatedAsync(change, ct))
        {
            var fieldWasEdited = db.ChangeTracker.Entries<WorkItemCustomFieldValue>().Any(x => MethodWriteGuard.Changed(x)
                && x.Entity.WorkItemId == item.Id && x.Entity.FieldDefinitionId == settings.MethodIdFieldId);
            if (Actor?.IsInteractiveHuman != true || change.IsCreation || fieldWasEdited)
                throw new MethodOperationException(409, "ambiguous_method_id", "O ID do método já está em uso no projeto.");
            // Transferência humana não é bloqueada por uma chave antiga que voltou a colidir.
            active = false;
        }
        if (state is null)
        {
            if (!active) return;
            state = new MethodTaskState { ProjectId = settings.ProjectId, WorkItemId = item.Id,
                OrganizationId = settings.OrganizationId, CycleId = Guid.NewGuid() };
            db.Add(state);
        }
        var oldId = state.MethodTaskId;
        var changesAssociation = state.MethodTaskId != (active ? change.MethodId : null)
            || change.ChangesIdentity || change.Source.ProjectId != change.Destination.ProjectId;
        if (changesAssociation)
        {
            Revoke(state, "Identidade ou escopo da task alterados.");
            ClearProofs(state); state.CycleId = Guid.NewGuid(); state.LastObservedFactId = null;
        }
        if (state.CycleId == Guid.Empty) state.CycleId = Guid.NewGuid();
        state.MethodTaskId = active ? change.MethodId : null;
        if (!active)
        {
            if (state.ActiveAuthorizationEventId is not null) Revoke(state, "Task retirada do controle ativo do método.");
            ClearProofs(state);
            if (change.WritesStage) await CanonicalEventAsync(change, state, oldId, ct);
            return;
        }
        if (!change.WritesStage)
        {
            if (state.ResumeApprovalEventId is not null && change.Entry.Property(x => x.Position).IsModified)
            {
                state.ResumeApprovalEventId = null;
                var invalidated = NewEvent(item.Id, "method_resume_invalidated");
                invalidated.Payload = Metadata(state, state.MethodTaskId).ToJsonString();
            }
            foreach (var row in db.ChangeTracker.Entries<TaskEvent>().Where(x => x.State == EntityState.Added
                && x.Entity.WorkItemId == item.Id && x.Entity.Kind == "stage_reclassified").Select(x => x.Entity))
                Enrich(row, change, state, state.MethodTaskId);
            return;
        }
        var from = Status(settings, change.Stored?.StageId);
        var to = Status(settings, item.StageId);
        var previousBlock = state.BlockingEventId;
        var approval = state.ResumeApprovalEventId;
        state.ResumeApprovalEventId = null;
        var stageEvent = await CanonicalEventAsync(change, state, state.MethodTaskId, ct);
        if (Actor?.IsInteractiveHuman == true && to == MethodStatusCodes.InDevelopment)
        {
            if (state.ActiveAuthorizationEventId is not null) Revoke(state, "Execução reconcedida pela pessoa na UI.");
            ClearProofs(state); state.CycleId = Guid.NewGuid();
            Enrich(stageEvent, change, state, state.MethodTaskId);
            state.ActiveAuthorizationEventId = Authorization(state, "Granted", stageEvent,
                null, Reason(change, "Execução autorizada pela pessoa na UI."))?.Id;
        }
        else if (to == MethodStatusCodes.Blocked)
        {
            var block = Authorization(state, "Blocked", stageEvent, state.ActiveAuthorizationEventId,
                Reason(change, "Task bloqueada."));
            state.BlockingEventId = block?.Id; state.BlockingPrincipalId = block?.ActorPrincipalId;
        }
        else if (from == MethodStatusCodes.Blocked && to == MethodStatusCodes.InDevelopment
            && Actor is { Role: MethodRole.Agent, AuthenticationKind: MethodAuthenticationKind.MethodToken }
            && previousBlock is not null && approval is not null)
        {
            Authorization(state, "Resumed", stageEvent, previousBlock, Reason(change, "Bloqueio próprio retomado após liberação humana."));
            state.BlockingEventId = null; state.BlockingPrincipalId = null;
        }
        else
        {
            state.BlockingEventId = null; state.BlockingPrincipalId = null;
            if (to is not (MethodStatusCodes.InDevelopment or MethodStatusCodes.InReview))
            {
                Revoke(state, "Execução encerrada ou retirada do fluxo autorizado.");
                state.ActiveAuthorizationEventId = null;
            }
        }
        // Todo movimento toca a versão, mesmo quando mantém a concessão do ciclo.
        if (db.Entry(state).State != EntityState.Added)
            db.Entry(state).Property(x => x.LastObservedFactId).IsModified = true;
        if (db.MethodExecution is { } execution && execution.Request.WorkItemId == item.Id)
        {
            if (execution.Request.ExpectedRowVersion is { } expected && db.Entry(state).State != EntityState.Added)
                db.Entry(state).Property(x => x.RowVersion).OriginalValue = expected;
            if (execution.Request.GitFact is { } fact)
                await ApplyFactAsync(change, state, stageEvent, fact, execution.Request, ct);
            execution.Result = new(item.Id, change.Stored?.StageId, item.StageId!.Value, true, false,
                stageEvent.Id, StageHistoryId(stageEvent), "applied");
        }
    }

    private async Task<bool> DuplicatedAsync(MethodItemChange change, CancellationToken ct)
    {
        var sameBatch = db.ChangeTracker.Entries<WorkItemCustomFieldValue>().Where(x => x.State is EntityState.Added or EntityState.Modified)
            .Any(x => x.Entity.FieldDefinitionId == change.Settings.MethodIdFieldId && x.Entity.Value == change.MethodId
                && x.Entity.WorkItemId != change.Entry.Entity.Id);
        return sameBatch || await db.WorkItemCustomFieldValues.IgnoreQueryFilters().AnyAsync(x =>
            x.FieldDefinitionId == change.Settings.MethodIdFieldId && x.Value == change.MethodId
            && x.WorkItemId != change.Entry.Entity.Id && x.WorkItem.Board.ProjectId == change.Settings.ProjectId, ct);
    }

    private void Revoke(MethodTaskState state, string reason)
    {
        if (state.ActiveAuthorizationEventId is not { } grant) return;
        var taskEvent = NewEvent(state.WorkItemId, "method_authorization_revoked");
        taskEvent.Payload = Metadata(state, state.MethodTaskId).ToJsonString();
        Authorization(state, "Revoked", taskEvent, grant, reason);
        state.ActiveAuthorizationEventId = null;
    }

    private MethodAuthorizationEvent? Authorization(MethodTaskState state, string kind, TaskEvent taskEvent, Guid? related, string reason)
    {
        // Origem legada pode retirar execução, mas não recebe uma identidade humana ou de sistema inventada.
        if (Actor is null || Actor.AuthenticationKind == MethodAuthenticationKind.Unknown) return null;
        var record = new MethodAuthorizationEvent { Id = Guid.NewGuid(), ProjectId = state.ProjectId,
            OrganizationId = state.OrganizationId, WorkItemId = state.WorkItemId, CycleId = state.CycleId,
            EventKind = kind, RelatedEventId = related, TaskEventId = taskEvent.Id,
            ActorPrincipalId = Actor.PrincipalId, ActorUserId = Actor.Role == MethodRole.Human ? Actor.ActorId : null,
            ActorName = Actor.DisplayName, Reason = reason.Length > 1000 ? reason[..1000] : reason, CreatedAtUtc = Now };
        db.Add(record); return record;
    }

    private async Task<TaskEvent> CanonicalEventAsync(MethodItemChange change, MethodTaskState state, string? methodId, CancellationToken ct)
    {
        var item = change.Entry.Entity;
        var history = db.ChangeTracker.Entries<StageHistory>().Where(x => x.State == EntityState.Added
            && x.Entity.WorkItemId == item.Id && x.Entity.StageId == item.StageId)
            .OrderByDescending(x => x.Entity.EnteredAt).Select(x => x.Entity).FirstOrDefault();
        if (item.StageId is { } stageId)
        {
            history ??= new StageHistory { Id = Guid.NewGuid(), WorkItemId = item.Id, StageId = stageId, EnteredAt = Now };
            if (db.Entry(history).State == EntityState.Detached) db.Add(history);
            if (history.Id == Guid.Empty) history.Id = Guid.NewGuid();
            var open = await db.StageHistories.IgnoreQueryFilters().Where(x => x.WorkItemId == item.Id && x.LeftAt == null).ToListAsync(ct);
            foreach (var row in open.Concat(db.StageHistories.Local).DistinctBy(x => x.Id))
                if (row.WorkItemId == item.Id && row.Id != history.Id && (row.LeftAt is null
                    || db.Entry(row).Property(x => x.LeftAt).IsModified
                        && db.Entry(row).Property(x => x.LeftAt).OriginalValue is null)) row.LeftAt = Now;
            history.EnteredAt = Now;
            if (Actor is not null) { history.ActorId = Actor.ActorId; history.ActorName = Actor.DisplayName; }
            var reason = Reason(change, "Movimentação pelo método.");
            history.Reason = reason.Length > 500 ? reason[..500] : reason;
        }
        var taskEvent = db.ChangeTracker.Entries<TaskEvent>().Where(x => x.State == EntityState.Added
            && x.Entity.WorkItemId == item.Id && x.Entity.Kind == "stage_changed")
            .Select(x => x.Entity).FirstOrDefault() ?? NewEvent(item.Id, "stage_changed");
        Enrich(taskEvent, change, state, methodId);
        var payload = Payload(taskEvent);
        payload["stageHistoryId"] = history is null ? null : JsonValue.Create(history.Id);
        taskEvent.Payload = payload.ToJsonString();
        return taskEvent;
    }

    private void Enrich(TaskEvent taskEvent, MethodItemChange change, MethodTaskState state, string? methodId)
    {
        var payload = Payload(taskEvent);
        foreach (var pair in Metadata(state, methodId)) payload[pair.Key] = pair.Value?.DeepClone();
        payload["fromStageId"] = JsonValue.Create(change.Stored?.StageId);
        payload["toStageId"] = JsonValue.Create(change.Entry.Entity.StageId);
        payload["fromStatus"] = Status(change.Settings, change.Stored?.StageId);
        payload["toStatus"] = Status(change.Settings, change.Entry.Entity.StageId);
        payload["fromProjectId"] = JsonValue.Create(change.Source.ProjectId);
        payload["toProjectId"] = JsonValue.Create(change.Destination.ProjectId);
        payload["reason"] = Reason(change, "Movimentação pelo método.");
        taskEvent.Payload = payload.ToJsonString();
        if (Actor is not null) taskEvent.ActorId = Actor.ActorId;
        taskEvent.CreatedAt = Now;
    }

    private JsonObject Metadata(MethodTaskState state, string? methodId) => new()
    {
        ["projectId"] = JsonValue.Create(state.ProjectId), ["methodTaskId"] = methodId,
        ["cycleId"] = JsonValue.Create(state.CycleId), ["principalId"] = JsonValue.Create(Actor?.PrincipalId),
        ["methodRole"] = Actor?.Role.ToCode(),
        ["authenticationKind"] = Actor is null ? "unknown" : JsonNamingPolicy.CamelCase.ConvertName(Actor.AuthenticationKind.ToString()),
        ["channel"] = Actor?.Channel ?? audit?.Origin ?? "system", ["correlationId"] = Actor?.CorrelationId ?? audit?.CorrelationId,
        ["actorId"] = Actor?.ActorId ?? audit?.UserId, ["actorName"] = Actor?.DisplayName
    };

    private TaskEvent NewEvent(Guid itemId, string kind)
    {
        var row = new TaskEvent { Id = Guid.NewGuid(), WorkItemId = itemId, Kind = kind,
            ActorId = Actor?.ActorId ?? audit?.UserId ?? string.Empty, CreatedAt = Now };
        db.Add(row); return row;
    }

    private async Task ApplyFactAsync(MethodItemChange change, MethodTaskState state, TaskEvent taskEvent,
        VerifiedGitFact fact, MethodMoveRequest request, CancellationToken ct)
    {
        if (Actor?.AuthenticationKind != MethodAuthenticationKind.IntegrationWorker || Actor.Role != MethodRole.System
            || Actor.ProjectId != state.ProjectId || fact.WorkItemId != state.WorkItemId || fact.CycleId != state.CycleId)
            throw new MethodOperationException(403, "verified_fact_required", "O movimento exige fato Git interno e verificado.");
        var applied = new GitAppliedFact { Id = Guid.NewGuid(), ProjectId = state.ProjectId, WorkItemId = state.WorkItemId,
            IntegrationId = fact.IntegrationId, CycleId = state.CycleId, SemanticKey = fact.SemanticKey,
            FactType = fact.FactType, ProviderEntityId = fact.ProviderEntityId, ProviderRevision = fact.ProviderRevision,
            FromStageId = change.Stored!.StageId!.Value, ToStageId = change.Entry.Entity.StageId!.Value,
            TaskEventId = taskEvent.Id, DeliveryId = request.DeliveryId, AppliedAtUtc = Now };
        db.Add(applied); state.LastObservedFactId = applied.Id;
        var payload = Payload(taskEvent);
        payload["integrationId"] = JsonValue.Create(fact.IntegrationId); payload["appliedFactId"] = JsonValue.Create(applied.Id);
        payload["deliveryId"] = JsonValue.Create(request.DeliveryId); payload["factType"] = fact.FactType;
        payload["providerRevision"] = fact.ProviderRevision; payload["headSha"] = fact.HeadSha; payload["mergeSha"] = fact.MergeSha;
        payload["pullRequestNumber"] = JsonValue.Create(fact.PullRequestNumber);
        payload["gitActorId"] = fact.ObservedGitActorId; payload["gitActorName"] = fact.ObservedGitActorName;
        taskEvent.Payload = payload.ToJsonString();
        if (request.DeliveryId is not { } deliveryId || request.ExpectedDeliveryRowVersion is not { } expected)
            throw new MethodOperationException(409, "lease_lost", "A entrega não possui lease vigente para confirmar o efeito.");
        var delivery = await db.Set<GitWebhookDelivery>().IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == deliveryId
            && x.ProjectId == state.ProjectId && x.IntegrationId == fact.IntegrationId, ct);
        if (delivery is null || delivery.Status != GitDeliveryStates.Processing || delivery.LeaseUntilUtc <= Now
            || !delivery.RowVersion.SequenceEqual(expected))
            throw new MethodOperationException(409, "lease_lost", "A entrega foi retomada por outro worker; nenhum efeito foi confirmado.");
        db.Entry(delivery).Property(x => x.RowVersion).OriginalValue = expected;
        delivery.Status = GitDeliveryStates.Processed; delivery.ProcessedAtUtc = Now;
        delivery.LeaseUntilUtc = null; delivery.NextAttemptAtUtc = null; delivery.LastErrorCode = null;
    }

    private static void ClearProofs(MethodTaskState state)
    {
        state.ActiveAuthorizationEventId = null; state.BlockingEventId = null;
        state.BlockingPrincipalId = null; state.ResumeApprovalEventId = null;
    }
    private static bool ValidId(string? value) => value is { Length: >= 6 and <= 32 }
        && value.StartsWith("T-", StringComparison.Ordinal) && value[2..].All(char.IsAsciiDigit);
    private static string? Status(MethodProjectSettings settings, Guid? stageId)
        => stageId is null ? null : settings.StageMappings.FirstOrDefault(x => x.StageId == stageId)?.StatusCode;
    private static string Reason(MethodItemChange change, string fallback)
        => string.IsNullOrWhiteSpace(change.Reason) ? fallback : change.Reason.Trim();
    private static JsonObject Payload(TaskEvent row)
    {
        try { return JsonNode.Parse(row.Payload ?? "{}") as JsonObject ?? new JsonObject { ["legacyPayload"] = row.Payload }; }
        catch (JsonException) { return new JsonObject { ["legacyPayload"] = row.Payload }; }
    }
    private static Guid? StageHistoryId(TaskEvent row)
        => Payload(row)["stageHistoryId"]?.GetValue<Guid>();
}
