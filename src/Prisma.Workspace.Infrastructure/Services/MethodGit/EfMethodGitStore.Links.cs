using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Infrastructure.Services.MethodGit;

public sealed partial class EfMethodGitStore
{
    private static TaskGitLinkDto LinkDto(TaskGitLink row) => new(row.Id, row.IntegrationId, row.WorkItemId,
        row.PullRequestObservationId, row.BranchName, row.Source, row.Observation?.Url, row.CreatedByPrincipalId, row.CreatedByUserId, row.CreatedAtUtc);
    public async Task<TaskGitLinkDto?> GetExplicitPullRequestLinkAsync(Guid integrationId, int number, CancellationToken ct = default)
    {
        var repository = await GetRepositoryAsync(integrationId, ct); if (repository is null) return null;
        var row = await Links.AsNoTracking().Include(x => x.Observation).SingleOrDefaultAsync(x => x.IntegrationId == integrationId
            && x.Source == "explicit" && x.Observation != null && x.Observation.PullRequestNumber == number, ct);
        return row is null ? null : LinkDto(row);
    }
    public async Task<IReadOnlyList<TaskGitLinkDto>> GetTaskLinksAsync(Guid workItemId, CancellationToken ct = default)
    {
        var task = await GetTaskAsync(workItemId, ct); if (task is null) return [];
        return (await Links.AsNoTracking().Include(x => x.Observation).Where(x => x.WorkItemId == workItemId && x.ProjectId == task.ProjectId)
            .OrderBy(x => x.CreatedAtUtc).ToListAsync(ct)).Select(LinkDto).ToArray();
    }
    public async Task<GitPullRequestObservationDto> UpsertObservationAsync(Guid integrationId, GitPullRequestSnapshot pr, string? reason, CancellationToken ct = default)
    {
        var repository = await GetRepositoryAsync(integrationId, ct) ?? throw Missing();
        var row = await Observations.SingleOrDefaultAsync(x => x.IntegrationId == integrationId && x.PullRequestNumber == pr.Number, ct);
        if (row is null)
        {
            row = new GitPullRequestObservation { Id = Guid.NewGuid(), ProjectId = repository.ProjectId, IntegrationId = repository.Id };
            db.Add(row);
        }
        if (pr.UpdatedAtUtc >= row.ProviderUpdatedAtUtc)
        {
            row.PullRequestNumber = pr.Number; row.Url = pr.Url; row.Title = pr.Title; row.HeadBranch = pr.HeadBranch;
            row.BaseBranch = pr.BaseBranch; row.HeadSha = pr.HeadSha; row.MergeSha = pr.MergeSha;
            row.HeadRepositoryId = pr.HeadRepositoryId; row.PullRequestAuthorId = pr.PullRequestAuthorId; row.PullRequestAuthorName = pr.PullRequestAuthorName;
            row.State = pr.State; row.IsDraft = pr.IsDraft; row.ProviderUpdatedAtUtc = pr.UpdatedAtUtc;
            row.ObservedAtUtc = _clock.GetUtcNow(); row.UnlinkedReason = reason;
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (UniqueViolation(exception))
        {
            db.Entry(row).State = EntityState.Detached;
            row = await Observations.SingleAsync(x => x.IntegrationId == integrationId && x.PullRequestNumber == pr.Number, ct);
        }
        return await ObservationDtoAsync(row, ct);
    }
    public async Task<TaskGitLinkDto> LinkInferredAsync(Guid integrationId, Guid workItemId, Guid? observationId,
        string? branch, string source, CancellationToken ct = default)
    {
        if (source is not ("branch" or "title")) throw new ArgumentException("Origem inferida inválida.", nameof(source));
        var repository = await GetRepositoryAsync(integrationId, ct) ?? throw Missing();
        var task = await GetTaskAsync(workItemId, ct) ?? throw Missing();
        if (task.ProjectId != repository.ProjectId || !task.IsTopLevel || task.IsArchived || task.OrganizationId != repository.OrganizationId) throw Forbidden();
        GitPullRequestObservation? observation = null;
        if (observationId.HasValue)
        {
            observation = await Observations.SingleOrDefaultAsync(x => x.Id == observationId && x.IntegrationId == integrationId && x.ProjectId == task.ProjectId, ct) ?? throw Missing();
        }
        else if (string.IsNullOrEmpty(branch) || branch.Length > 255) throw new MethodOperationException(400, "invalid_git_branch", "Branch inválida.");
        var existing = await Links.Include(x => x.Observation).SingleOrDefaultAsync(x => x.IntegrationId == integrationId
            && (observationId.HasValue ? x.PullRequestObservationId == observationId : x.PullRequestObservationId == null && x.BranchName == branch), ct);
        if (existing is not null)
        {
            if (existing.WorkItemId != workItemId) throw new MethodOperationException(409, "git_link_conflict", "PR ou branch já associado a outra task. Solicite resolução humana.");
            return LinkDto(existing);
        }
        var actor = access.Current ?? throw Forbidden();
        var row = new TaskGitLink { Id = Guid.NewGuid(), ProjectId = task.ProjectId, IntegrationId = integrationId,
            WorkItemId = workItemId, PullRequestObservationId = observationId, Observation = observation, BranchName = branch,
            Source = source, CreatedAtUtc = _clock.GetUtcNow(), CreatedByPrincipalId = actor.PrincipalId,
            CreatedByUserId = actor.Role == MethodRole.Human ? actor.ActorId : null };
        db.Add(row);
        await TouchTaskVersionAsync(workItemId, task.ProjectId, ct);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (UniqueViolation(exception))
        {
            db.Entry(row).State = EntityState.Detached;
            existing = await Links.Include(x => x.Observation).SingleOrDefaultAsync(x => x.IntegrationId == integrationId
                && (observationId.HasValue ? x.PullRequestObservationId == observationId : x.PullRequestObservationId == null && x.BranchName == branch), ct);
            if (existing is null || existing.WorkItemId != workItemId) throw new MethodOperationException(409, "git_link_conflict", "Vínculo alterado concorrentemente. Atualize os dados.");
            return LinkDto(existing);
        }
        return LinkDto(row);
    }
    public async Task<TaskGitLinkDto> LinkExplicitAsync(Guid integrationId, Guid workItemId, Guid observationId, string reason, CancellationToken ct = default)
    {
        var actor = access.Current ?? throw Forbidden();
        if (actor.AuthenticationKind is not (MethodAuthenticationKind.UiSession or MethodAuthenticationKind.MethodToken)
            || actor.Role == MethodRole.System || string.IsNullOrWhiteSpace(reason) || reason.Length > 1000) throw Forbidden();
        var repository = await GetRepositoryAsync(integrationId, ct) ?? throw Missing();
        var task = await GetTaskAsync(workItemId, ct) ?? throw Missing();
        if (task.ProjectId != repository.ProjectId || !task.IsTopLevel || task.IsArchived) throw Forbidden();
        var project = await GetProjectAsync(task.ProjectId, ct) ?? throw Missing();
        await permissions.EnsureAsync(actor.ActorId, PlatformPermission.Edit, PermissionScope.WorkItem, workItemId, ct);
        await permissions.EnsureAsync(actor.ActorId, PlatformPermission.Comment, PermissionScope.WorkItem, workItemId, ct);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var observation = await Observations.SingleOrDefaultAsync(x => x.Id == observationId && x.IntegrationId == integrationId && x.ProjectId == task.ProjectId, ct) ?? throw Missing();
            var existing = await Links.Include(x => x.Observation).SingleOrDefaultAsync(x => x.PullRequestObservationId == observationId, ct);
            if (existing is not null && existing.WorkItemId != workItemId)
            {
                if (!actor.IsInteractiveHuman) throw new MethodOperationException(409, "git_link_conflict", "PR já vinculado a outra task; solicite resolução humana na UI.");
                await permissions.EnsureAsync(actor.ActorId, PlatformPermission.Edit, PermissionScope.WorkItem, existing.WorkItemId, ct);
                var oldItem = existing.WorkItemId;
                var oldField = await db.Set<WorkItemCustomFieldValue>().SingleOrDefaultAsync(x => x.WorkItemId == oldItem && x.FieldDefinitionId == project.PrFieldId, ct);
                if (oldField?.Value == observation.Url)
                {
                    oldField.Value = await Links.Where(x => x.WorkItemId == oldItem && x.Id != existing.Id && x.Observation != null)
                        .OrderByDescending(x => x.CreatedAtUtc).Select(x => x.Observation!.Url).FirstOrDefaultAsync(ct);
                    oldField.UpdatedBy = actor.ActorId; oldField.UpdatedAt = _clock.GetUtcNow();
                }
                db.Add(new TaskEvent { Id = Guid.NewGuid(), WorkItemId = oldItem, ActorId = actor.ActorId, Kind = "git_pr_unlinked", CreatedAt = _clock.GetUtcNow(),
                    Payload = JsonSerializer.Serialize(new { principalId = actor.PrincipalId, actorName = actor.DisplayName, authenticationKind = "uiSession",
                        channel = actor.Channel, correlationId = actor.CorrelationId, integrationId, observationId, url = observation.Url, reason,
                        gitPullRequestAuthorId = observation.PullRequestAuthorId, gitPullRequestAuthorName = observation.PullRequestAuthorName }, JsonOptions) });
                await TouchTaskVersionAsync(oldItem, task.ProjectId, ct);
                existing.WorkItemId = workItemId; existing.Source = "branch"; // Abaixo promove explicitamente e preserva evidência anterior no histórico.
                existing.CreatedAtUtc = _clock.GetUtcNow();
            }
            if (existing is { Source: "explicit" }) { await transaction.CommitAsync(ct); return LinkDto(existing); }
            var row = existing ?? new TaskGitLink { Id = Guid.NewGuid(), ProjectId = task.ProjectId, IntegrationId = integrationId,
                WorkItemId = workItemId, PullRequestObservationId = observationId, Observation = observation, CreatedAtUtc = _clock.GetUtcNow() };
            if (existing is null) db.Add(row);
            row.Source = "explicit"; row.CreatedByPrincipalId = actor.PrincipalId;
            row.CreatedByUserId = actor.Role == MethodRole.Human ? actor.ActorId : null;
            var field = await db.Set<WorkItemCustomFieldValue>().SingleOrDefaultAsync(x => x.WorkItemId == workItemId && x.FieldDefinitionId == project.PrFieldId, ct);
            if (field is null)
            {
                field = new WorkItemCustomFieldValue { WorkItemId = workItemId, FieldDefinitionId = project.PrFieldId };
                db.Add(field);
            }
            field.Value = observation.Url; field.UpdatedAt = _clock.GetUtcNow(); field.UpdatedBy = actor.ActorId;
            db.Add(new Comment { Id = Guid.NewGuid(), WorkItemId = workItemId, UserId = actor.ActorId,
                UserName = actor.DisplayName, Content = $"[pr] {observation.Url} · {actor.Role.ToCode()} · {actor.Channel}", CreatedAt = _clock.GetUtcNow() });
            db.Add(new TaskEvent { Id = Guid.NewGuid(), WorkItemId = workItemId, ActorId = actor.ActorId, Kind = "git_pr_linked", CreatedAt = _clock.GetUtcNow(),
                Payload = JsonSerializer.Serialize(new { principalId = actor.PrincipalId, actorName = actor.DisplayName,
                    methodRole = actor.Role.ToCode(), authenticationKind = actor.AuthenticationKind == MethodAuthenticationKind.UiSession ? "uiSession" : "methodToken",
                    channel = actor.Channel, correlationId = actor.CorrelationId, integrationId, observationId, url = observation.Url, reason,
                    gitPullRequestAuthorId = observation.PullRequestAuthorId, gitPullRequestAuthorName = observation.PullRequestAuthorName }, JsonOptions) });
            observation.UnlinkedReason = null;
            await TouchTaskVersionAsync(workItemId, task.ProjectId, ct);
            var evaluation = new NormalizedGitEvent("github", repository.RepositoryId, repository.InstallationId,
                GitDeliveryProcessor.LinkEvaluationEvent, "reevaluate", _clock.GetUtcNow(), $"link:{row.Id:N}", null,
                observation.PullRequestNumber, observation.Url, observation.Title, observation.HeadBranch, observation.BaseBranch,
                observation.HeadSha, observation.MergeSha, observation.State, observation.IsDraft, observation.State == "closed" && observation.MergeSha is not null);
            var normalizedJson = JsonSerializer.Serialize(evaluation, JsonOptions);
            var inboxId = Guid.NewGuid();
            // Namespace interno explícito; nunca é apresentado como X-GitHub-Delivery ou evento recebido do provedor.
            db.Add(new GitWebhookDelivery { Id = inboxId, IntegrationId = integrationId, ProjectId = task.ProjectId, Provider = "github",
                DeliveryId = $"link:{inboxId:D}", BodyHash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedJson)),
                EventName = GitDeliveryProcessor.LinkEvaluationEvent, EventAction = "reevaluate", NormalizedEventJson = normalizedJson,
                ReceivedAtUtc = _clock.GetUtcNow(), CorrelationId = actor.CorrelationId });
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return LinkDto(row);
        });
    }
    public async Task<GitPullRequestPage> ListPullRequestsAsync(Guid projectId, bool? linked, int page, int pageSize, CancellationToken ct = default)
    {
        Scope(projectId);
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize) throw new MethodOperationException(400, "invalid_git_pagination", "Paginação inválida; pageSize deve estar entre1 e100.");
        var query = Observations.AsNoTracking().Where(x => x.ProjectId == projectId);
        if (linked == true) query = query.Where(x => x.UnlinkedReason == null && Links.Any(link => link.PullRequestObservationId == x.Id));
        else if (linked == false) query = query.Where(x => x.UnlinkedReason != null || !Links.Any(link => link.PullRequestObservationId == x.Id));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.ProviderUpdatedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var result = new List<GitPullRequestObservationDto>();
        foreach (var row in rows) result.Add(await ObservationDtoAsync(row, ct));
        return new(result, page, pageSize, total);
    }
    public async Task<GitPullRequestObservationDto?> GetPullRequestObservationAsync(Guid projectId, Guid observationId, CancellationToken ct = default)
    {
        Scope(projectId);
        var row = await Observations.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Id == observationId, ct);
        return row is null ? null : await ObservationDtoAsync(row, ct);
    }
    private async Task<GitPullRequestObservationDto> ObservationDtoAsync(GitPullRequestObservation row, CancellationToken ct)
    {
        var link = await Links.AsNoTracking().SingleOrDefaultAsync(x => x.PullRequestObservationId == row.Id, ct);
        string? methodTaskId = null;
        if (link is not null) methodTaskId = await db.Set<MethodTaskState>().AsNoTracking().Where(x => x.WorkItemId == link.WorkItemId && x.ProjectId == row.ProjectId && x.OrganizationId == Tenant)
            .Select(x => x.MethodTaskId).SingleOrDefaultAsync(ct);
        return new(row.Id, row.IntegrationId, row.ProjectId, row.PullRequestNumber, row.Url, row.Title, row.HeadBranch,
            row.BaseBranch, row.State, row.IsDraft, link?.WorkItemId, methodTaskId, row.UnlinkedReason, row.ProviderUpdatedAtUtc);
    }
}
