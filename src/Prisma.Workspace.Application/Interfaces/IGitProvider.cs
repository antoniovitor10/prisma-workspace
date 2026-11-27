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
    // Modo consulta (HU-0002): confere o repositório com o token de leitura e devolve o ID estável; só GET.
    Task<GitRepositoryIdentity> GetRepositoryIdentityAsync(GitRepositoryDescriptor repository, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
    // Modo consulta: a branch já existe no GitHub? (vínculo local vira enviado). Só GET.
    Task<bool> BranchExistsAsync(GitRepositoryDescriptor repository, string branch, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
    // HU-0007: PR aberto cuja head é a branch (do próprio repositório). Leitura com a sessão de leitura atual.
    // Erros do GitHub saem como GitPullRequestRejectedException, já classificados.
    async Task<GitPullRequestSnapshot?> FindOpenPullRequestByHeadAsync(GitRepositoryDescriptor repository, string branch,
        CancellationToken cancellationToken = default)
    {
        var open = await ListPullRequestsAsync(repository, "open", cancellationToken);
        return open.PullRequests.FirstOrDefault(pr => pr.State == "open" && pr.HeadBranch == branch
            && (pr.HeadRepositoryId is null || pr.HeadRepositoryId == repository.RepositoryId));
    }
    // HU-0007: abre o PR com um token de escrita (só pull_requests: write + metadata: read, um repositório, sem cache),
    // pedido na hora e descartado depois. Erros do GitHub saem como GitPullRequestRejectedException, já classificados.
    Task<GitPullRequestSnapshot> CreatePullRequestAsync(GitRepositoryDescriptor repository, GitPullRequestCreation pullRequest,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

/// <summary>Referências resolvidas apenas no namespace externo MethodGit:Secrets; valores nunca saem em DTO/log.</summary>
public interface IMethodGitSecretStore
{
    Task<string> ResolveAsync(string reference, CancellationToken cancellationToken = default);
}
