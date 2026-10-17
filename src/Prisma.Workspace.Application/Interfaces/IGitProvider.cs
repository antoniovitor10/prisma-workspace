using Prisma.Workspace.Application.Features.MethodGit.Contracts;

namespace Prisma.Workspace.Application.Interfaces;

public interface IGitProvider
{
    string Provider { get; }
    bool VerifyWebhookSignature(ReadOnlySpan<byte> originalBody, string signature, string secret);
    NormalizedGitEvent NormalizeWebhook(string eventName, ReadOnlySpan<byte> originalBody);
    Task<GitInstallationValidation> ValidateInstallationAsync(GitRepositoryDescriptor repository, CancellationToken cancellationToken = default);
    Task<GitPullRequestSnapshot> GetPullRequestAsync(GitRepositoryDescriptor repository, int number, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitReviewSnapshot>> GetReviewsAsync(GitRepositoryDescriptor repository, int number, CancellationToken cancellationToken = default);
    Task<GitPullRequestReadSet> ListPullRequestsAsync(GitRepositoryDescriptor repository, string state, CancellationToken cancellationToken = default);
    // Implementações reais limitam o histórico ao ciclo, preservando a leitura completa de PRs abertos.
    // O padrão mantém compatibilidade de fakes/provedores; Complete nunca presume paginação não lida.
    Task<GitPullRequestReadSet> ListPullRequestsForCycleAsync(GitRepositoryDescriptor repository,
        DateTimeOffset authorizedAtUtc, CancellationToken cancellationToken = default)
        => ListPullRequestsAsync(repository, "all", cancellationToken);
}

/// <summary>Referências resolvidas apenas no namespace externo MethodGit:Secrets; valores nunca saem em DTO/log.</summary>
public interface IMethodGitSecretStore
{
    Task<string> ResolveAsync(string reference, CancellationToken cancellationToken = default);
}
