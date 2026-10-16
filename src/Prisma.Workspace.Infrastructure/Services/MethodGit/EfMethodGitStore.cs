using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;

namespace Prisma.Workspace.Infrastructure.Services.MethodGit;

public sealed partial class EfMethodGitStore(AppDbContext db, IOrganizationContext organization,
    IMethodAccessContext access, IPermissionService permissions, TimeProvider? clock = null,
    GitWebhookIngressLimiter? ingressLimiter = null) : IMethodGitStore
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private Guid Tenant => organization.RequireOrganizationId();
    private void Scope(Guid projectId)
    {
        if (access.Current is { AuthenticationKind: MethodAuthenticationKind.MethodToken or MethodAuthenticationKind.IntegrationWorker } actor
            && actor.ProjectId != projectId) throw new MethodOperationException(403, "method_project_scope", "Projeto fora do escopo da credencial.");
    }
    private IQueryable<GitRepositoryIntegration> Repositories => db.Set<GitRepositoryIntegration>().Where(x => x.OrganizationId == Tenant);
    private IQueryable<GitWebhookDelivery> Deliveries => db.Set<GitWebhookDelivery>().Where(x => x.Integration.OrganizationId == Tenant);
    private IQueryable<GitPullRequestObservation> Observations => db.Set<GitPullRequestObservation>().Where(x => x.Integration.OrganizationId == Tenant);
    private IQueryable<TaskGitLink> Links => db.Set<TaskGitLink>().Where(x => x.Integration.OrganizationId == Tenant);
    private static GitRepositoryDescriptor Descriptor(GitRepositoryIntegration r) => new(r.Id, r.OrganizationId, r.ProjectId, r.Provider, r.Host,
        r.RepositoryId, r.RepositoryFullName, r.DefaultBranch, r.AppId, r.InstallationId, r.AppPrivateKeySecretRef,
        r.WebhookSecretRef, r.PreviousWebhookSecretRef, r.PreviousSecretValidUntilUtc, r.SystemPrincipalId, r.Enabled, r.RowVersion);

    public async Task<GitRepositoryDescriptor?> GetRepositoryForIngressAsync(Guid integrationId, CancellationToken ct = default)
    {
        // Única busca global por ID para autenticar a entrada: sem Include nem leitura de tasks.
        var row = await db.Set<GitRepositoryIntegration>().IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == integrationId, ct);
        return row is null ? null : Descriptor(row);
    }
    public async Task<GitRepositoryDescriptor?> GetRepositoryAsync(Guid integrationId, CancellationToken ct = default)
    {
        var row = await Repositories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == integrationId, ct);
        if (row is null) return null; Scope(row.ProjectId);
        return Descriptor(row) with { OperationalError = await OperationalErrorAsync(row.Id, ct) };
    }
    public async Task<IReadOnlyList<GitRepositoryDescriptor>> ListRepositoriesAsync(Guid projectId, CancellationToken ct = default)
    {
        Scope(projectId);
        var result = new List<GitRepositoryDescriptor>();
        foreach (var row in await Repositories.AsNoTracking().Where(x => x.ProjectId == projectId).OrderBy(x => x.RepositoryFullName).ToListAsync(ct))
            result.Add(Descriptor(row) with { OperationalError = await OperationalErrorAsync(row.Id, ct) });
        return result;
    }
    public async Task SaveRepositoryAsync(GitRepositoryDescriptor repository, CancellationToken ct = default)
    {
        Scope(repository.ProjectId);
        if (repository.OrganizationId != Tenant) throw Forbidden();
        var settings = await GetProjectAsync(repository.ProjectId, ct) ?? throw Missing();
        var principal = await db.Set<MethodPrincipal>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == repository.SystemPrincipalId
            && x.ProjectId == repository.ProjectId && x.OrganizationId == Tenant && x.Role == MethodRole.System && x.IsActive, ct);
        if (principal is null || settings.OrganizationId != repository.OrganizationId) throw Forbidden();
        var row = await Repositories.SingleOrDefaultAsync(x => x.Id == repository.Id, ct);
        if (row is null)
        {
            row = new GitRepositoryIntegration { Id = repository.Id, OrganizationId = Tenant, ProjectId = repository.ProjectId,
                Provider = repository.Provider, Host = repository.Host, RepositoryId = repository.RepositoryId,
                AppId = repository.AppId, InstallationId = repository.InstallationId, SystemPrincipalId = repository.SystemPrincipalId,
                CreatedAtUtc = _clock.GetUtcNow() };
            db.Add(row);
        }
        else if (row.ProjectId != repository.ProjectId || row.Provider != repository.Provider || row.Host != repository.Host
            || row.RepositoryId != repository.RepositoryId || row.AppId != repository.AppId || row.InstallationId != repository.InstallationId
            || row.SystemPrincipalId != repository.SystemPrincipalId) throw new MethodOperationException(409, "git_repository_immutable", "Identidade da integração não pode ser substituída.");
        if (repository.RowVersion is not null && db.Entry(row).State != EntityState.Added)
            db.Entry(row).Property(x => x.RowVersion).OriginalValue = repository.RowVersion;
        row.RepositoryFullName = repository.RepositoryFullName; row.DefaultBranch = repository.DefaultBranch;
        row.AppPrivateKeySecretRef = repository.AppPrivateKeySecretRef; row.WebhookSecretRef = repository.WebhookSecretRef;
        row.PreviousWebhookSecretRef = repository.PreviousWebhookSecretRef; row.PreviousSecretValidUntilUtc = repository.PreviousSecretValidUntilUtc;
        row.Enabled = repository.Enabled; row.UpdatedAtUtc = _clock.GetUtcNow();
        await ScheduleLegacyCandidatesAsync(repository, settings.PrFieldId, ct);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { throw new MethodOperationException(409, "git_repository_changed", "Configuração alterada por outra operação. Atualize os dados antes de salvar."); }
        catch (DbUpdateException exception) when (UniqueViolation(exception))
        { throw new MethodOperationException(409, "git_repository_already_registered", "Repositório já cadastrado para um projeto."); }
    }
    public async Task<MethodProjectSnapshot?> GetProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        Scope(projectId);
        var settings = await db.Set<MethodProjectSettings>().AsNoTracking().Include(x => x.StageMappings)
            .SingleOrDefaultAsync(x => x.ProjectId == projectId && x.OrganizationId == Tenant, ct);
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(x => x.Id == projectId && x.OrganizationId == Tenant && !x.IsArchived, ct);
        if (settings is null || project is null) return null;
        var board = await db.Boards.AsNoTracking().SingleOrDefaultAsync(x => x.Id == settings.BoardId && x.OrganizationId == Tenant && x.ProjectId == projectId, ct);
        if (board is null) return null;
        var definitions = await db.Set<ProjectCustomFieldDefinition>().AsNoTracking().Where(x => x.ProjectId == projectId && x.IsActive && x.Type == CustomFieldType.Text).ToListAsync(ct);
        var fields = definitions.GroupBy(x => x.Name, StringComparer.Ordinal).Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.Single().Id, StringComparer.Ordinal);
        var mappings = settings.StageMappings.ToDictionary(x => x.StatusCode, x => x.StageId);
        var ids = mappings.Values.ToArray();
        var columns = await db.Stages.AsNoTracking().Where(x => x.ProjectId == projectId && x.BoardId == board.Id && ids.Contains(x.Id)).ToListAsync(ct);
        var valid = mappings.Count == 7 && mappings.Keys.All(MethodStatusCodes.IsValid) && ids.Distinct().Count() == 7 && columns.Count == 7
            && mappings.All(pair => columns.Any(stage => stage.Id == pair.Value && (pair.Key == MethodStatusCodes.Completed ? stage.Category == StageCategory.Done : stage.Category != StageCategory.Done)))
            && new[] { settings.MethodIdFieldId, settings.HuFieldId, settings.PrFieldId }.All(id => definitions.Any(definition => definition.Id == id));
        return new(Tenant, projectId, project.Name, board.Id, board.Name, settings.MethodIdFieldId, settings.HuFieldId, settings.PrFieldId,
            settings.Enabled && valid, settings.GitPhase, settings.AutonomyLevel, settings.PolicyVersion, mappings, fields);
    }
    public async Task<MethodTaskSnapshot?> GetTaskAsync(Guid workItemId, CancellationToken ct = default)
    {
        var state = await db.Set<MethodTaskState>().AsNoTracking().SingleOrDefaultAsync(x => x.WorkItemId == workItemId && x.OrganizationId == Tenant, ct);
        if (state is null || string.IsNullOrWhiteSpace(state.MethodTaskId) || state.MethodTaskId.Length is < 6 or > 32
            || !state.MethodTaskId.StartsWith("T-", StringComparison.Ordinal) || !state.MethodTaskId[2..].All(char.IsAsciiDigit)) return null;
        Scope(state.ProjectId);
        var project = await GetProjectAsync(state.ProjectId, ct);
        if (project is null) return null;
        var item = await db.WorkItems.AsNoTracking().Include(x => x.Board).SingleOrDefaultAsync(x => x.Id == workItemId && x.Board.OrganizationId == Tenant, ct);
        if (item is null || item.Board.ProjectId != state.ProjectId || item.BoardId != project.BoardId) return null;
        var idValues = await db.Set<WorkItemCustomFieldValue>().AsNoTracking().Where(x => x.WorkItemId == workItemId && x.FieldDefinitionId == project.MethodIdFieldId).Select(x => x.Value).ToListAsync(ct);
        if (idValues.Count != 1 || idValues[0] != state.MethodTaskId) return null;
        var events = db.Set<MethodAuthorizationEvent>().AsNoTracking().Where(x => x.ProjectId == state.ProjectId && x.WorkItemId == workItemId && x.CycleId == state.CycleId);
        var grant = state.ActiveAuthorizationEventId.HasValue ? await events.SingleOrDefaultAsync(x => x.Id == state.ActiveAuthorizationEventId && x.EventKind == "Granted", ct) : null;
        var block = state.BlockingEventId.HasValue ? await events.SingleOrDefaultAsync(x => x.Id == state.BlockingEventId && x.EventKind == "Blocked", ct) : null;
        if (grant is not null && !await HasUiProofAsync(grant.TaskEventId, workItemId, ct)) grant = null;
        string? origin = null;
        if (block is not null)
        {
            var payload = await db.Set<TaskEvent>().AsNoTracking().Where(x => x.Id == block.TaskEventId && x.WorkItemId == workItemId).Select(x => x.Payload).SingleOrDefaultAsync(ct);
            if (payload is not null)
            {
                try
                {
                    using var json = JsonDocument.Parse(payload);
                    if (block.ActorPrincipalId == state.BlockingPrincipalId && json.RootElement.TryGetProperty("fromStatus", out var status)
                        && status.ValueKind == JsonValueKind.String && MethodStatusCodes.IsValid(status.GetString())) origin = status.GetString();
                    else if (block.ActorPrincipalId == state.BlockingPrincipalId && json.RootElement.TryGetProperty("fromStageId", out var previous)
                        && previous.TryGetGuid(out var stageId)) origin = project.StageMappings.FirstOrDefault(pair => pair.Value == stageId).Key;
                }
                catch (JsonException) { /* Prova incompleta nunca libera retomada. */ }
            }
        }
        var approval = state.ResumeApprovalEventId.HasValue && block is not null
            ? await events.SingleOrDefaultAsync(x => x.Id == state.ResumeApprovalEventId && x.EventKind == "ResumeAllowed" && x.RelatedEventId == block.Id, ct) : null;
        if (approval is not null && !await HasUiProofAsync(approval.TaskEventId, workItemId, ct)) approval = null;
        var authorizationEventIds = await db.Set<MethodAuthorizationEvent>().AsNoTracking()
            .Where(x => x.ProjectId == state.ProjectId && x.WorkItemId == workItemId).Select(x => x.TaskEventId).ToListAsync(ct);
        DateTimeOffset? lastInteractive = null, lastManual = null;
        // Tipo de credencial vem do payload confiável. UserId humano também existe em MethodToken.
        await foreach (var entry in db.Set<TaskEvent>().AsNoTracking().Where(x => x.WorkItemId == workItemId
            && (x.Kind == "stage_changed" || x.Kind == "stage_reclassified" || authorizationEventIds.Contains(x.Id)))
            .OrderByDescending(x => x.CreatedAt).Select(x => new { x.Payload, x.CreatedAt, x.Kind }).AsAsyncEnumerable().WithCancellation(ct))
        {
            if (lastInteractive is null && HasUiProof(entry.Payload)) lastInteractive = entry.CreatedAt;
            if (lastManual is null && entry.Kind is "stage_changed" or "stage_reclassified" && HasManualProof(entry.Payload)) lastManual = entry.CreatedAt;
            if (lastInteractive.HasValue && lastManual.HasValue) break;
        }
        return new(Tenant, state.ProjectId, item.BoardId, item.Id, state.MethodTaskId, item.StageId,
            project.StageMappings.FirstOrDefault(pair => pair.Value == item.StageId).Key ?? string.Empty,
            state.CycleId, grant?.Id, grant?.CreatedAtUtc, lastInteractive, state.BlockingEventId,
            state.BlockingPrincipalId, origin, approval?.Id, item.ParentId is null, item.IsArchived, state.RowVersion, LastManualChangeAtUtc: lastManual);
    }
    public async Task<IReadOnlyList<MethodTaskSnapshot>> FindTasksAsync(Guid projectId, string methodTaskId, CancellationToken ct = default)
    {
        Scope(projectId); var project = await GetProjectAsync(projectId, ct); if (project is null) return [];
        var ids = await db.Set<WorkItemCustomFieldValue>().AsNoTracking().Where(x => x.FieldDefinitionId == project.MethodIdFieldId
            && x.Value == methodTaskId && x.WorkItem.Board.ProjectId == projectId && x.WorkItem.Board.OrganizationId == Tenant && !x.WorkItem.IsArchived)
            .Select(x => x.WorkItemId).Distinct().ToListAsync(ct);
        var result = new List<MethodTaskSnapshot>();
        foreach (var id in ids)
        {
            var snapshot = await GetTaskAsync(id, ct);
            result.Add(snapshot ?? new(Tenant, projectId, project.BoardId, id, methodTaskId, null, "", Guid.Empty,
                null, null, null, null, null, null, null, false, false, []));
        }
        return result;
    }
    public async Task<GitDeliveryReceipt> AcceptDeliveryAsync(Guid integrationId, string deliveryId, byte[] hash, NormalizedGitEvent normalized,
        string correlation, CancellationToken ct = default)
    {
        var repository = await GetRepositoryAsync(integrationId, ct) ?? throw Missing();
        if (hash.Length != 32) throw new ArgumentException("Hash de entrega inválido.", nameof(hash));
        var existing = await Deliveries.AsNoTracking().SingleOrDefaultAsync(x => x.IntegrationId == integrationId && x.Provider == repository.Provider && x.DeliveryId == deliveryId, ct);
        if (existing is not null) return Receipt(existing, hash);
        var before = db.ChangeTracker.Entries().Select(entry => entry.Entity).ToHashSet(ReferenceEqualityComparer.Instance);
        var delivery = new GitWebhookDelivery { Id = Guid.NewGuid(), IntegrationId = repository.Id, ProjectId = repository.ProjectId,
            Provider = repository.Provider, DeliveryId = deliveryId, BodyHash = hash, EventName = normalized.EventName, EventAction = normalized.Action,
            NormalizedEventJson = JsonSerializer.Serialize(normalized, JsonOptions), CorrelationId = correlation, ReceivedAtUtc = _clock.GetUtcNow() };
        db.Add(delivery);
        try { await db.SaveChangesAsync(ct); return new(delivery.Id, false, false, delivery.Status); }
        catch (DbUpdateException exception) when (UniqueViolation(exception))
        {
            // Remove somente entradas deste SaveChanges, incluindo auditoria gerada e revertida com ele.
            foreach (var entry in db.ChangeTracker.Entries().Where(entry => !before.Contains(entry.Entity)).ToArray()) entry.State = EntityState.Detached;
            existing = await Deliveries.AsNoTracking().SingleOrDefaultAsync(x => x.IntegrationId == integrationId && x.Provider == repository.Provider && x.DeliveryId == deliveryId, ct);
            if (existing is null) throw;
            return Receipt(existing, hash);
        }
    }
    private static GitDeliveryReceipt Receipt(GitWebhookDelivery row, byte[] hash) => new(row.Id, true, !CryptographicOperations.FixedTimeEquals(row.BodyHash, hash), row.Status);
    public async Task<GitDeliveryLease?> ClaimNextDeliveryAsync(DateTimeOffset now, TimeSpan lease, CancellationToken ct = default)
    {
        if (lease <= TimeSpan.Zero || lease > TimeSpan.FromMinutes(10)) throw new ArgumentOutOfRangeException(nameof(lease));
        var query = db.Set<GitWebhookDelivery>().IgnoreQueryFilters().Where(x => x.Integration.Enabled && x.Integration.Settings.Enabled
            && (x.Status == GitDeliveryStates.Received || x.Status == GitDeliveryStates.RetryableFailure || x.Status == GitDeliveryStates.Processing)
            && (!x.NextAttemptAtUtc.HasValue || x.NextAttemptAtUtc <= now) && (!x.LeaseUntilUtc.HasValue || x.LeaseUntilUtc <= now));
        var candidate = await query.AsNoTracking().OrderBy(x => x.ReceivedAtUtc).Select(x => new { x.Id, x.ProjectId, x.IntegrationId, x.RowVersion }).FirstOrDefaultAsync(ct);
        if (candidate is null) return null;
        var affected = await query.Where(x => x.Id == candidate.Id && x.RowVersion == candidate.RowVersion).ExecuteUpdateAsync(set => set
            .SetProperty(x => x.Status, GitDeliveryStates.Processing).SetProperty(x => x.Attempts, x => x.Attempts + 1)
            .SetProperty(x => x.LeaseUntilUtc, now.Add(lease)).SetProperty(x => x.NextAttemptAtUtc, (DateTimeOffset?)null), ct);
        if (affected != 1) return null;
        return await db.Set<GitWebhookDelivery>().IgnoreQueryFilters().AsNoTracking().Where(x => x.Id == candidate.Id)
            .Select(x => new GitDeliveryLease(x.Id, x.IntegrationId, x.ProjectId, x.RowVersion)).SingleAsync(ct);
    }
    public async Task<GitDeliverySnapshot?> GetDeliveryAsync(Guid deliveryId, CancellationToken ct = default)
    {
        var row = await Deliveries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == deliveryId, ct);
        if (row is null) return null; Scope(row.ProjectId);
        var normalized = JsonSerializer.Deserialize<NormalizedGitEvent>(row.NormalizedEventJson, JsonOptions)
            ?? throw new MethodOperationException(409, "invalid_stored_event", "Evento persistido inválido; avise o administrador.");
        return new(row.Id, row.IntegrationId, row.ProjectId, row.Provider, row.DeliveryId, normalized, row.Status, row.Attempts, row.CorrelationId, row.RowVersion);
    }
    public async Task<bool> CompleteDeliveryAsync(Guid id, byte[] version, string status, string? errorCode, DateTimeOffset? retryAt, CancellationToken ct = default)
    {
        if (status is not (GitDeliveryStates.Processed or GitDeliveryStates.Ignored or GitDeliveryStates.RetryableFailure or GitDeliveryStates.PermanentFailure))
            throw new ArgumentException("Estado de entrega inválido.", nameof(status));
        var row = await Deliveries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); if (row is null) return false; Scope(row.ProjectId);
        var now = _clock.GetUtcNow();
        return await Deliveries.Where(x => x.Id == id && x.RowVersion == version && x.Status == GitDeliveryStates.Processing && x.LeaseUntilUtc > now)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, status).SetProperty(x => x.LastErrorCode, errorCode)
                .SetProperty(x => x.NextAttemptAtUtc, retryAt).SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.ProcessedAtUtc, status == GitDeliveryStates.RetryableFailure ? (DateTimeOffset?)null : now), ct) == 1;
    }
    public async Task<bool> HasAppliedFactAsync(Guid projectId, Guid workItemId, byte[] key, CancellationToken ct = default)
    {
        Scope(projectId);
        return await db.Set<GitAppliedFact>().AsNoTracking().AnyAsync(x => x.ProjectId == projectId && x.WorkItemId == workItemId
            && x.Integration.OrganizationId == Tenant && x.SemanticKey == key, ct);
    }
    private async Task<string?> OperationalErrorAsync(Guid integrationId, CancellationToken ct)
    {
        if (ingressLimiter?.OperationalError(integrationId, _clock.GetUtcNow()) is { } ingressError) return ingressError;
        var code = await Deliveries.AsNoTracking().Where(x => x.IntegrationId == integrationId && x.LastErrorCode != null
            && (x.Status == GitDeliveryStates.RetryableFailure || x.Status == GitDeliveryStates.PermanentFailure
                || (x.Status == GitDeliveryStates.Ignored && x.LastErrorCode == "retained_merge_closed_without_merge")))
            .OrderByDescending(x => x.ReceivedAtUtc).Select(x => x.LastErrorCode).FirstOrDefaultAsync(ct);
        return code switch
        {
            null => null,
            "git_rate_limited" => "Limite de consultas do GitHub atingido; a integração aguardará o prazo indicado pelo provedor.",
            "merge_waiting_open_pr" => "Há outro PR aberto da mesma task. Aguardando com consultas espaçadas; a promoção para teste permanece pendente.",
            "effective_changes_requested" => "Há alterações solicitadas na revisão vigente do PR. Aguardando com consultas espaçadas.",
            "git_repository_disabled" => "Há um repositório desabilitado. Aguardando a atualização da integração antes de confirmar a visão dos PRs.",
            "method_disabled" or "method_git_disabled" => "Automação do método suspensa. Aguardando a configuração com consultas espaçadas, sem mover tasks.",
            "method_mapping" => "Mapeamento das colunas indisponível. Revise a configuração do método.",
            "retained_merge_closed_without_merge" => "Um PR fechou sem merge após um merge retido. Confira o status e o histórico da task antes de prosseguir.",
            "git_access_unavailable" or "git_app_key_invalid" or "git_repository_access_mismatch" => "Acesso da GitHub App indisponível. Confira a instalação e as credenciais externas.",
            _ => "Há uma entrega pendente ou com falha. Confira a integração e o histórico do projeto."
        };
    }
    private async Task ScheduleLegacyCandidatesAsync(GitRepositoryDescriptor repository, Guid prFieldId, CancellationToken ct)
    {
        var values = await db.Set<WorkItemCustomFieldValue>().AsNoTracking().Where(x => x.FieldDefinitionId == prFieldId && x.Value != null
            && x.WorkItem.Board.ProjectId == repository.ProjectId && x.WorkItem.Board.OrganizationId == Tenant
            && x.WorkItem.ParentId == null && !x.WorkItem.IsArchived).Select(x => x.Value!).Distinct().ToListAsync(ct);
        var numbers = new HashSet<int>();
        foreach (var value in values)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "github.com" || !uri.IsDefaultPort
                || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0) continue;
            var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4 || parts[2] != "pull" || !string.Equals($"{parts[0]}/{parts[1]}", repository.RepositoryFullName, StringComparison.OrdinalIgnoreCase)
                || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0 || !numbers.Add(number)) continue;
            var internalId = $"legacy:{number}";
            if (await Observations.AsNoTracking().AnyAsync(x => x.IntegrationId == repository.Id && x.PullRequestNumber == number, ct)
                || await Deliveries.AsNoTracking().AnyAsync(x => x.IntegrationId == repository.Id && x.DeliveryId == internalId, ct)) continue;
            var normalized = new NormalizedGitEvent("github", repository.RepositoryId, repository.InstallationId, GitDeliveryProcessor.LegacyCandidateEvent,
                "candidate", _clock.GetUtcNow(), internalId, null, number, value, null, null, null, null, null, null);
            var json = JsonSerializer.Serialize(normalized, JsonOptions);
            db.Add(new GitWebhookDelivery { Id = Guid.NewGuid(), ProjectId = repository.ProjectId, IntegrationId = repository.Id, DeliveryId = internalId,
                BodyHash = SHA256.HashData(Encoding.UTF8.GetBytes(json)), EventName = normalized.EventName, EventAction = "candidate",
                NormalizedEventJson = json, ReceivedAtUtc = _clock.GetUtcNow(), CorrelationId = access.Current?.CorrelationId ?? $"legacy:{Guid.NewGuid():N}" });
        }
    }
    private static bool HasManualProof(string? payload)
    {
        if (payload is null) return false;
        try
        {
            using var json = JsonDocument.Parse(payload);
            return json.RootElement.TryGetProperty("authenticationKind", out var kind)
                && ((kind.ValueKind == JsonValueKind.String && kind.GetString() is "uiSession" or "methodToken")
                    || (kind.ValueKind == JsonValueKind.Number && kind.TryGetInt32(out var value) && value is 1 or 2));
        }
        catch (JsonException) { return false; }
    }
    private async Task TouchTaskVersionAsync(Guid workItemId, Guid projectId, CancellationToken ct)
    {
        var state = await db.Set<MethodTaskState>().SingleAsync(x => x.WorkItemId == workItemId && x.ProjectId == projectId && x.OrganizationId == Tenant, ct);
        // Mesmo valor: invalida a leitura concorrente de PRs sem criar outra identidade/status.
        db.Entry(state).Property(x => x.MethodTaskId).IsModified = true;
    }
    private async Task<bool> HasUiProofAsync(Guid eventId, Guid workItemId, CancellationToken ct)
        => HasUiProof(await db.Set<TaskEvent>().AsNoTracking().Where(x => x.Id == eventId && x.WorkItemId == workItemId).Select(x => x.Payload).SingleOrDefaultAsync(ct));
    private static bool HasUiProof(string? payload)
    {
        if (payload is null) return false;
        try
        {
            using var json = JsonDocument.Parse(payload);
            return json.RootElement.TryGetProperty("authenticationKind", out var kind)
                && ((kind.ValueKind == JsonValueKind.String && kind.GetString() == "uiSession")
                    || (kind.ValueKind == JsonValueKind.Number && kind.TryGetInt32(out var value) && value == (int)MethodAuthenticationKind.UiSession));
        }
        catch (JsonException) { return false; }
    }
    private static bool UniqueViolation(DbUpdateException exception) => exception.GetBaseException() is SqlException { Number: 2601 or 2627 };
    private static MethodOperationException Missing() => new(404, "method_resource_not_found", "Recurso do método não encontrado.");
    private static MethodOperationException Forbidden() => new(403, "method_resource_scope", "Recurso fora do escopo autorizado.");
}
