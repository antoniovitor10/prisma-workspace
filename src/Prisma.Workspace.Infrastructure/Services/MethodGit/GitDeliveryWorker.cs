using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Infrastructure.Persistence;

namespace Prisma.Workspace.Infrastructure.Services.MethodGit;

public sealed class GitDeliveryWorker(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<GitDeliveryWorker> logger, TimeProvider? clock = null) : BackgroundService
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private DateTimeOffset _nextPurge = DateTimeOffset.MinValue;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (configuration.GetValue<bool>("MethodGit:Enabled"))
                {
                    if (_clock.GetUtcNow() >= _nextPurge) { await PurgeFinishedAsync(stoppingToken); _nextPurge = _clock.GetUtcNow().AddDays(1); }
                    if (await ProcessNextAsync(stoppingToken)) continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogError("Fila Git indisponível; nenhuma confirmação de processamento foi fabricada."); }
            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
    public async Task<bool> ProcessNextAsync(CancellationToken ct = default)
    {
        GitDeliveryLease? lease;
        string correlation;
        await using (var discovery = scopes.CreateAsyncScope())
        {
            lease = await discovery.ServiceProvider.GetRequiredService<IMethodGitStore>().ClaimNextDeliveryAsync(_clock.GetUtcNow(), TimeSpan.FromMinutes(10), ct);
            if (lease is null) return false;
            // Metadados operacionais mínimos; não carrega evento/task antes de estabelecer tenant.
            correlation = await discovery.ServiceProvider.GetRequiredService<AppDbContext>().Set<GitWebhookDelivery>().IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.Id == lease.Id && x.IntegrationId == lease.IntegrationId).Select(x => x.CorrelationId).SingleAsync(ct);
        }
        await using var scope = scopes.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IMethodGitStore>();
        var initialized = false;
        GitDeliverySnapshot? snapshot = null;
        try
        {
            await scope.ServiceProvider.GetRequiredService<IMethodWorkerContextInitializer>().InitializeAsync(lease.IntegrationId, lease.Id, correlation, ct);
            initialized = true;
            snapshot = await store.GetDeliveryAsync(lease.Id, ct);
            if (snapshot is null || snapshot.IntegrationId != lease.IntegrationId || snapshot.ProjectId != lease.ProjectId
                || snapshot.Status != GitDeliveryStates.Processing || !snapshot.RowVersion.SequenceEqual(lease.RowVersion)) return true;
            await scope.ServiceProvider.GetRequiredService<GitDeliveryProcessor>().ProcessAsync(snapshot, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            var code = exception switch
            {
                GitHubUnavailableException github => github.Code,
                MethodOperationException method => method.Code,
                DbUpdateConcurrencyException => "git_concurrency",
                _ => "git_processing_failure"
            };
            var obsolete = code is "stale_git_fact" or "git_transition" or "method_transition";
            logger.LogWarning("Entrega Git {InboxId} pendente/recusada: {ErrorCode}; lease permanece protegida.", lease.Id, code);
            if (initialized)
            {
                var attempts = snapshot?.Attempts ?? 1;
                var next = exception is GitHubUnavailableException { RetryAtUtc: { } retry } ? retry
                    : _clock.GetUtcNow().AddSeconds(Math.Min(900, 5 * Math.Pow(2, Math.Min(attempts, 8))));
                if (next <= _clock.GetUtcNow()) next = _clock.GetUtcNow().AddSeconds(1);
                await store.CompleteDeliveryAsync(lease.Id, lease.RowVersion, obsolete ? GitDeliveryStates.Ignored : GitDeliveryStates.RetryableFailure,
                    code, obsolete ? null : next, ct);
            }
            // Falha antes do contexto não ganha autoridade: lease expira e a entrada durável será retomada.
        }
        return true;
    }
    private async Task PurgeFinishedAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var removed = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<GitWebhookDelivery>().IgnoreQueryFilters()
            .Where(x => x.ProcessedAtUtc < _clock.GetUtcNow().AddDays(-30)
                && (x.Status == GitDeliveryStates.Processed || x.Status == GitDeliveryStates.Ignored || x.Status == GitDeliveryStates.PermanentFailure))
            .ExecuteDeleteAsync(ct);
        logger.LogInformation("Expurgo operacional Git: {Count} entradas terminadas; fatos e histórico permanecem.", removed);
    }
}
