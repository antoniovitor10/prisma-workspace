using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Infrastructure.Services.MethodGit;

public sealed partial class EfMethodGitStore
{
    public async Task<IReadOnlyList<Guid>> FindBranchLinkTasksAsync(Guid integrationId, string branchName, CancellationToken ct = default)
    {
        var repository = await GetRepositoryAsync(integrationId, ct); if (repository is null || string.IsNullOrEmpty(branchName)) return [];
        // BranchName usa collation binária: a comparação diferencia maiúsculas, como as refs do Git.
        return await Links.AsNoTracking().Where(x => x.IntegrationId == repository.Id && x.PullRequestObservationId == null && x.BranchName == branchName)
            .Select(x => x.WorkItemId).Distinct().Take(2).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TaskGitLinkDto>> ListIntegrationLinksAsync(Guid integrationId, CancellationToken ct = default)
    {
        var repository = await GetRepositoryAsync(integrationId, ct); if (repository is null) return [];
        return (await Links.AsNoTracking().Include(x => x.Observation).Include(x => x.Integration)
            .Where(x => x.IntegrationId == repository.Id).ToListAsync(ct)).Select(LinkDto).ToArray();
    }

    public async Task<bool> EnqueuePollEvaluationAsync(Guid integrationId, GitPullRequestSnapshot pr, string deliveryId, string correlationId,
        CancellationToken ct = default)
    {
        var repository = await GetRepositoryAsync(integrationId, ct) ?? throw Missing();
        if (!deliveryId.StartsWith("poll:", StringComparison.Ordinal) || deliveryId.Length > 100)
            throw new ArgumentException("Entrada interna da consulta inválida.", nameof(deliveryId));
        if (await Deliveries.AsNoTracking().AnyAsync(x => x.IntegrationId == repository.Id && x.DeliveryId == deliveryId, ct)) return false;
        var evaluation = new NormalizedGitEvent("github", repository.RepositoryId, null, GitDeliveryProcessor.PollEvaluationEvent, "reevaluate",
            _clock.GetUtcNow(), deliveryId, null, pr.Number, pr.Url, pr.Title, pr.HeadBranch, pr.BaseBranch, pr.HeadSha,
            pr.IsMerged ? pr.MergeSha : null, pr.State, pr.IsDraft, pr.IsMerged, HeadRepositoryId: pr.HeadRepositoryId);
        var json = JsonSerializer.Serialize(evaluation, JsonOptions);
        // Namespace interno explícito; nunca é apresentado como X-GitHub-Delivery ou evento recebido do provedor.
        db.Add(new GitWebhookDelivery { Id = Guid.NewGuid(), IntegrationId = repository.Id, ProjectId = repository.ProjectId, Provider = "github",
            DeliveryId = deliveryId, BodyHash = SHA256.HashData(Encoding.UTF8.GetBytes(json)), EventName = GitDeliveryProcessor.PollEvaluationEvent,
            EventAction = "reevaluate", NormalizedEventJson = json, ReceivedAtUtc = _clock.GetUtcNow(),
            CorrelationId = correlationId.Length > 64 ? correlationId[..64] : correlationId });
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateException exception) when (UniqueViolation(exception)) { db.ChangeTracker.Clear(); return false; }
    }
}
