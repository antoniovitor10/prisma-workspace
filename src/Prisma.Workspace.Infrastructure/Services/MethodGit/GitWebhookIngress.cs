using System.Collections.Concurrent;
using MediatR;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Interfaces;

namespace Prisma.Workspace.Infrastructure.Services.MethodGit;

/// <summary>Limita apenas integrações existentes; IDs arbitrários não criam entradas na memória.</summary>
public sealed class GitWebhookIngressLimiter
{
    private readonly ConcurrentDictionary<Guid, (long Window, int Count)> _windows = new();
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _limited = new();
    public bool TryConsumeInvalid(Guid integrationId, DateTimeOffset now)
    {
        var window = now.ToUnixTimeSeconds() / 60;
        var value = _windows.AddOrUpdate(integrationId, (window, 1), (_, old) => old.Window == window ? (window, old.Count + 1) : (window, 1));
        if (value.Count <= 60) return true;
        _limited[integrationId] = now;
        return false;
    }
    public string? OperationalError(Guid integrationId, DateTimeOffset now) => _limited.TryGetValue(integrationId, out var at) && at >= now.AddHours(-24)
        ? "Webhooks com assinatura inválida atingiram o limite (429). Confira o segredo e configure somente create, pull_request e pull_request_review; após corrigir, reenvie manualmente as entregas recusadas no GitHub."
        : null;
}

public sealed partial class GitWebhookIngress(
    IMethodGitStore store, IGitProvider provider, IMethodGitSecretStore secrets,
    IMethodWorkerContextInitializer contextInitializer, GitWebhookIngressLimiter limiter,
    ILogger<GitWebhookIngress> logger, TimeProvider? clock = null)
{
    public const int MaximumBodyBytes = 5 * 1024 * 1024;
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<GitDeliveryReceipt> AcceptAsync(GitWebhookInput input, CancellationToken cancellationToken = default)
    {
        if (input.OriginalBody.Length > MaximumBodyBytes)
            throw new MethodOperationException(413, "git_payload_too_large", "Webhook acima de 5 MiB recusado sem efeito. Avise o administrador para reenvio controlado.");
        if (!Guid.TryParseExact(input.DeliveryId, "D", out _) || !EventPattern().IsMatch(input.EventName)
            || string.IsNullOrWhiteSpace(input.CorrelationId) || input.CorrelationId.Length > 64 || input.CorrelationId.Any(char.IsControl))
            throw new MethodOperationException(400, "invalid_git_headers", "Cabeçalhos obrigatórios do webhook inválidos.");
        var repository = await store.GetRepositoryForIngressAsync(input.IntegrationId, cancellationToken);
        if (repository is null || !repository.Enabled || repository.Provider != provider.Provider)
            throw new MethodOperationException(401, "invalid_git_signature", "Webhook não autenticado.");
        var currentSecret = await secrets.ResolveAsync(repository.WebhookSecretRef, cancellationToken);
        var version = "current";
        var valid = provider.VerifyWebhookSignature(input.OriginalBody.Span, input.Signature, currentSecret);
        if (!valid && repository.PreviousWebhookSecretRef is not null && repository.PreviousSecretValidUntilUtc > _clock.GetUtcNow())
        {
            var previous = await secrets.ResolveAsync(repository.PreviousWebhookSecretRef, cancellationToken);
            valid = provider.VerifyWebhookSignature(input.OriginalBody.Span, input.Signature, previous);
            version = "previous";
        }
        if (!valid)
        {
            if (!limiter.TryConsumeInvalid(repository.Id, _clock.GetUtcNow()))
            {
                logger.LogWarning("Integração Git {IntegrationId}: limite de assinaturas inválidas; confira a configuração e reenvie manualmente as entregas recusadas no GitHub.", repository.Id);
                throw new MethodOperationException(429, "git_ingress_rate_limited", "Limite de assinaturas inválidas atingido. Aguarde 60 segundos, confira o segredo e reenvie manualmente as entregas recusadas no GitHub.");
            }
            throw new MethodOperationException(401, "invalid_git_signature", "Assinatura do webhook inválida.");
        }
        // Assinaturas válidas não compartilham o balde de falhas: rajadas legítimas
        // permanecem duráveis na inbox, pois o GitHub não garante reenvio automático.
        var normalized = provider.NormalizeWebhook(input.EventName, input.OriginalBody.Span);
        if (normalized.RepositoryId != repository.RepositoryId
            || (normalized.InstallationId.HasValue && normalized.InstallationId != repository.InstallationId))
            throw new MethodOperationException(403, "git_repository_mismatch", "Repositório ou instalação fora do escopo autorizado.");

        // Só depois do HMAC e da identidade Git confirmada estabelece o tenant/principal.
        await contextInitializer.InitializeAsync(repository.Id, null, input.CorrelationId, cancellationToken);
        // Coluna/campo removido não descarta um fato autenticado: permanece na inbox,
        // e o processador suspende os efeitos até a configuração voltar a ser válida.
        GitDeliveryReceipt receipt;
        try
        {
            receipt = await store.AcceptDeliveryAsync(repository.Id, input.DeliveryId,
                SHA256.HashData(input.OriginalBody.Span), normalized, input.CorrelationId, cancellationToken);
        }
        catch (MethodOperationException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        { throw new MethodOperationException(503, "git_inbox_unavailable", "Não foi possível persistir a entrega. O webhook não foi aceito; tente novamente."); }
        logger.LogInformation("Webhook Git autenticado: integração {IntegrationId}, entrada {InboxId}, assinatura {SecretVersion}, duplicado {Duplicate}, conflito {Conflict}.",
            repository.Id, receipt.DeliveryId, version, receipt.Duplicate, receipt.Conflict);
        return receipt;
    }
    [GeneratedRegex("^[a-z_]{1,64}$", RegexOptions.CultureInvariant)] private static partial Regex EventPattern();
}

public sealed record AcceptGitWebhookCommand(GitWebhookInput Input) : IRequest<GitDeliveryReceipt>;
public sealed class AcceptGitWebhookHandler(GitWebhookIngress ingress) : IRequestHandler<AcceptGitWebhookCommand, GitDeliveryReceipt>
{
    public Task<GitDeliveryReceipt> Handle(AcceptGitWebhookCommand request, CancellationToken ct) => ingress.AcceptAsync(request.Input, ct);
}
