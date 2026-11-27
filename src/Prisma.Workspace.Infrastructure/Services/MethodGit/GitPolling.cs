using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
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

/// <summary>Códigos da última consulta (LastPollStatus), sem texto do GitHub (HU-0002, RF15).</summary>
public static class GitPollStatus
{
    public const string Ok = "ok";
    public const string TokenInvalid = "git_token_invalid";
    public const string TokenForbidden = "git_token_forbidden";
    public const string RepositoryNotFound = "git_repository_not_found";
    public const string CredentialRevoked = "git_credential_revoked";
    public const string RepositoryMismatch = "git_repository_mismatch";
    public const string RateLimited = "git_rate_limited";
    public const string Incomplete = "git_read_incomplete";
    public const string Unavailable = "git_unavailable";

    public static string From(Exception exception) => exception switch
    {
        GitHubUnavailableException { Code: "git_rate_limited" } => RateLimited,
        GitHubUnavailableException { Code: "git_credential_revoked" or "git_credential_unreadable" } => CredentialRevoked,
        GitHubUnavailableException { StatusCode: 401 } => TokenInvalid,
        GitHubUnavailableException { StatusCode: 403 } => TokenForbidden,
        GitHubUnavailableException { Code: "git_not_found" } => RepositoryNotFound,
        GitHubUnavailableException { Code: "git_repository_access_mismatch" or "invalid_git_repository_name" } => RepositoryMismatch,
        _ => Unavailable,
    };
}

/// <summary>Exclusão entre instâncias da API: um lock de aplicação do SQL Server por integração.</summary>
public interface IGitPollLock
{
    Task<IAsyncDisposable?> TryAcquireAsync(Guid integrationId, CancellationToken cancellationToken = default);
}

/// <summary>sp_getapplock com dono de sessão e espera zero, numa conexão própria mantida aberta durante a consulta.</summary>
public sealed class SqlGitPollLock(IServiceScopeFactory scopes) : IGitPollLock
{
    public async Task<IAsyncDisposable?> TryAcquireAsync(Guid integrationId, CancellationToken ct = default)
    {
        string connectionString;
        await using (var scope = scopes.CreateAsyncScope())
            connectionString = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetConnectionString()
                ?? throw new InvalidOperationException("Conexão do banco ausente.");
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "DECLARE @r int; EXEC @r = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0; SELECT @r;";
        command.Parameters.Add(new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = Resource(integrationId) });
        var result = Convert.ToInt32(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        if (result >= 0) return new Held(connection, integrationId);
        await connection.DisposeAsync();
        return null;
    }
    private static string Resource(Guid integrationId) => $"prisma-git-poll:{integrationId:N}";
    private sealed class Held(SqlConnection connection, Guid integrationId) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "EXEC sp_releaseapplock @Resource = @resource, @LockOwner = 'Session';";
                command.Parameters.Add(new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = Resource(integrationId) });
                await command.ExecuteNonQueryAsync();
            }
            finally { await connection.DisposeAsync(); }
        }
    }
}

/// <summary>
/// Uma consulta de um repositório no modo sem admin (HU-0002, RF9, RF11, D1). Lê o GitHub com o token de leitura e
/// só então grava: branches locais vistas viram enviadas e cada PR relevante vira uma entrada "poll:" na fila do webhook,
/// onde o processador aplica as mesmas guardas e move o card. A consulta nunca move um card por conta própria.
/// Executa num escopo já inicializado com a identidade de sistema da integração e o canal "consulta".
/// </summary>
public sealed class GitPollService(IMethodGitStore store, IGitProvider provider, ILogger<GitPollService> logger, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    /// <summary>Folga na janela de PRs atualizados, para cobrir relógio e paginação entre duas consultas.</summary>
    public static readonly TimeSpan Overlap = TimeSpan.FromMinutes(10);

    public async Task<GitPollResultDto> PollAsync(Guid integrationId, bool manual, string correlationId, CancellationToken ct = default)
    {
        var repository = await store.GetRepositoryAsync(integrationId, ct)
            ?? throw new MethodOperationException(404, "git_repository_not_found", "Integração não encontrada.");
        if (!repository.IsPoll) throw new MethodOperationException(409, "git_repository_not_polled", "O repositório não está no modo consulta.");
        if (!repository.Enabled) throw new MethodOperationException(409, "git_repository_unavailable", "Integração pausada.");
        var now = _clock.GetUtcNow();
        List<(TaskGitLinkDto Link, bool Exists)> branches;
        List<GitPullRequestSnapshot> evaluate;
        try
        {
            // 1. Leituras (só GET). Nada é gravado antes de o GitHub responder tudo.
            var links = await store.ListIntegrationLinksAsync(repository.Id, ct);
            var since = (repository.LastPolledAtUtc ?? now.AddDays(-1)) - Overlap;
            var read = await provider.ListPullRequestsForCycleAsync(repository, since, ct);
            if (!read.Complete) throw new GitHubUnavailableException(read.IncompleteReason ?? GitPollStatus.Incomplete);
            var linkedBranches = links.Where(l => l.ObservationId is null && l.BranchName is not null).Select(l => l.BranchName!).ToHashSet(StringComparer.Ordinal);
            var linkedNumbers = links.Where(l => l.PullRequestNumber.HasValue).Select(l => l.PullRequestNumber!.Value).ToHashSet();
            // PRs atualizados desde a última consulta e, sempre, os abertos ligados a uma branch ou PR já vinculados.
            evaluate = read.PullRequests.Where(pr => pr.UpdatedAtUtc >= since
                    || (pr.State == "open" && (linkedBranches.Contains(pr.HeadBranch) || linkedNumbers.Contains(pr.Number))))
                .GroupBy(pr => pr.Number).Select(g => g.OrderByDescending(pr => pr.UpdatedAtUtc).First()).OrderBy(pr => pr.Number).ToList();
            var heads = read.PullRequests.Where(pr => pr.HeadRepositoryId is null || pr.HeadRepositoryId == repository.RepositoryId)
                .Select(pr => pr.HeadBranch).ToHashSet(StringComparer.Ordinal);
            branches = [];
            foreach (var link in links.Where(l => l.ObservationId is null && l.BranchName is not null && l.BranchState == GitBranchStates.Local))
                branches.Add((link, heads.Contains(link.BranchName!) || await provider.BranchExistsAsync(repository, link.BranchName!, ct)));
        }
        catch (Exception exception) when (exception is GitHubUnavailableException)
        {
            var status = GitPollStatus.From(exception);
            logger.LogWarning("Consulta Git da integração {IntegrationId} sem sucesso: {PollStatus}; nenhum card movido.", repository.Id, status);
            await store.RecordPollResultAsync(repository.Id, status, false, ct);
            return new(repository.Id, repository.RepositoryFullName, status, false, repository.LastPolledAtUtc, 0, 0);
        }
        // 2. Gravações: branch vista vira enviada (git_branch_pushed pelo canal consulta); PRs entram na fila do webhook.
        var pushed = 0;
        foreach (var (link, exists) in branches.Where(b => b.Exists))
            if (await store.MarkBranchPushedAsync(repository.Id, link.BranchName!, null, null, ct) == GitBranchPushResult.Recorded) pushed++;
        var enqueued = 0;
        var links2 = await store.ListIntegrationLinksAsync(repository.Id, ct);
        foreach (var pr in evaluate)
        {
            // Agendada: ID determinístico pelo estado do PR e pelo vínculo da branch, para não reenfileirar o mesmo fato a cada hora.
            // Consultar agora: ID novo, reavalia na hora (o fato aplicado continua deduplicado pela chave semântica).
            var hint = links2.FirstOrDefault(l => l.ObservationId is null && l.BranchName == pr.HeadBranch)?.WorkItemId.ToString("N")[..8] ?? "-";
            var deliveryId = manual ? $"poll:{Guid.NewGuid():N}"
                : $"poll:{pr.Number}:{pr.UpdatedAtUtc.UtcDateTime.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture)}:{hint}";
            if (await store.EnqueuePollEvaluationAsync(repository.Id, pr, deliveryId, correlationId, ct)) enqueued++;
        }
        await store.RecordPollResultAsync(repository.Id, GitPollStatus.Ok, true, ct);
        logger.LogInformation("Consulta Git da integração {IntegrationId}: {Enqueued} PR(s) reavaliado(s), {Pushed} branch(es) enviada(s).", repository.Id, enqueued, pushed);
        return new(repository.Id, repository.RepositoryFullName, GitPollStatus.Ok, true, now, enqueued, pushed);
    }
}

/// <summary>
/// Orquestra as consultas: de hora em hora (GitPollingWorker) e "Consultar agora" (uma por minuto por integração).
/// Exclusão entre instâncias pelo lock do SQL Server e conferência do horário dentro do lock; falha espera uma hora em memória.
/// </summary>
public sealed class GitPollRunner(IServiceScopeFactory scopes, IGitPollLock locks, ILogger<GitPollRunner> logger, TimeProvider? clock = null) : IGitPollRunner
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    public static readonly TimeSpan ManualCooldown = TimeSpan.FromMinutes(1);
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _failedUntil = new();
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _lastManual = new();

    public async Task<GitPollResultDto> PollNowAsync(Guid integrationId, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow();
        _ = await StateAsync(integrationId, ct) ?? throw new MethodOperationException(404, "git_repository_not_found", "Integração não encontrada.");
        // Limite por instância do "Consultar agora"; a leitura de teste do cadastro e a consulta agendada não contam.
        if (_lastManual.TryGetValue(integrationId, out var last) && now - last < ManualCooldown)
            throw new MethodOperationException(429, "git_poll_too_soon", "Uma consulta deste repositório acabou de rodar. Tente de novo em 1 minuto.");
        await using var held = await locks.TryAcquireAsync(integrationId, ct)
            ?? throw new MethodOperationException(429, "git_poll_running", "Uma consulta deste repositório já está em andamento. Tente de novo em 1 minuto.");
        _lastManual[integrationId] = now;
        var result = await RunAsync(integrationId, manual: true, ct);
        if (result.Success) _failedUntil.TryRemove(integrationId, out _); else _failedUntil[integrationId] = now.Add(Interval);
        return result;
    }

    /// <summary>Uma rodada do worker: consulta cada integração no modo consulta que está há 1 hora sem consulta com sucesso.</summary>
    public async Task<int> PollDueAsync(CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow();
        List<Guid> due;
        await using (var scope = scopes.CreateAsyncScope())
        {
            // Descoberta global só de IDs; o tenant é estabelecido por integração antes de ler qualquer dado.
            var limit = now.Subtract(Interval);
            due = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<GitRepositoryIntegration>().IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.Mode == GitIntegrationModes.Poll && x.Enabled && x.Settings.Enabled && (x.LastPolledAtUtc == null || x.LastPolledAtUtc <= limit))
                .OrderBy(x => x.LastPolledAtUtc).Select(x => x.Id).Take(50).ToListAsync(ct);
        }
        var polled = 0;
        foreach (var id in due)
        {
            if (_failedUntil.TryGetValue(id, out var until) && until > now) continue;
            await using var held = await locks.TryAcquireAsync(id, ct);
            if (held is null) continue; // Outra instância está consultando.
            var state = await StateAsync(id, ct);
            // Dentro do lock, relê o horário: a outra instância pode ter acabado de consultar.
            if (state is null || !state.Enabled || state.LastPolledAtUtc > now.Subtract(Interval)) continue;
            try
            {
                var result = await RunAsync(id, manual: false, ct);
                if (result.Success) _failedUntil.TryRemove(id, out _); else _failedUntil[id] = now.Add(Interval);
                polled++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                _failedUntil[id] = now.Add(Interval);
                logger.LogWarning("Consulta Git agendada da integração {IntegrationId} falhou: {ErrorType}.", id, exception.GetType().Name);
            }
        }
        return polled;
    }

    private sealed record PollState(bool Enabled, DateTimeOffset? LastPolledAtUtc);
    private async Task<PollState?> StateAsync(Guid integrationId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<GitRepositoryIntegration>().IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == integrationId && x.Mode == GitIntegrationModes.Poll)
            .Select(x => new PollState(x.Enabled && x.Settings.Enabled, x.LastPolledAtUtc)).SingleOrDefaultAsync(ct);
    }

    private async Task<GitPollResultDto> RunAsync(Guid integrationId, bool manual, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        // Correlação "poll:" define o canal "consulta" no histórico (MethodWorkerContextInitializer).
        var correlation = $"poll:{Guid.NewGuid():N}";
        await scope.ServiceProvider.GetRequiredService<IMethodWorkerContextInitializer>().InitializeAsync(integrationId, null, correlation, ct);
        return await scope.ServiceProvider.GetRequiredService<GitPollService>().PollAsync(integrationId, manual, correlation, ct);
    }
}

/// <summary>De minuto em minuto procura repositórios no modo consulta vencidos (1 hora) e os consulta.</summary>
public sealed class GitPollingWorker(GitPollRunner runner, IConfiguration configuration, ILogger<GitPollingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (configuration.GetValue<bool>("MethodGit:Enabled")) await runner.PollDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError("Consulta Git agendada indisponível: {ErrorType}.", exception.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
