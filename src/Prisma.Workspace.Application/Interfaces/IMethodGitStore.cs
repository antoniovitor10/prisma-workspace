using Prisma.Workspace.Application.Features.MethodGit.Contracts;

namespace Prisma.Workspace.Application.Interfaces;

public interface IMethodGitStore
{
    // A entrada anônima recebe só a configuração desta integração; isso não libera consulta global de tasks.
    Task<GitRepositoryDescriptor?> GetRepositoryForIngressAsync(Guid integrationId, CancellationToken cancellationToken = default);
    Task<GitRepositoryDescriptor?> GetRepositoryAsync(Guid integrationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitRepositoryDescriptor>> ListRepositoriesAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task SaveRepositoryAsync(GitRepositoryDescriptor repository, CancellationToken cancellationToken = default);
    Task<MethodProjectSnapshot?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<MethodTaskSnapshot?> GetTaskAsync(Guid workItemId, CancellationToken cancellationToken = default);
    // Pode devolver múltiplos resultados: identidade ambígua é erro, não fallback para outro título/projeto.
    Task<IReadOnlyList<MethodTaskSnapshot>> FindTasksAsync(Guid projectId, string methodTaskId, CancellationToken cancellationToken = default);
    Task<TaskGitLinkDto?> GetExplicitPullRequestLinkAsync(Guid integrationId, int number, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskGitLinkDto>> GetTaskLinksAsync(Guid workItemId, CancellationToken cancellationToken = default);
    Task<GitPullRequestObservationDto> UpsertObservationAsync(Guid integrationId, GitPullRequestSnapshot pullRequest, string? unlinkedReason, CancellationToken cancellationToken = default);
    Task<TaskGitLinkDto> LinkInferredAsync(Guid integrationId, Guid workItemId, Guid? observationId, string? branchName, string source, CancellationToken cancellationToken = default);
    // Operação atômica para vínculo, campo PR e comentário; autoria vem do contexto validado.
    Task<TaskGitLinkDto> LinkExplicitAsync(Guid integrationId, Guid workItemId, Guid observationId, string reason, CancellationToken cancellationToken = default);
    Task<GitPullRequestPage> ListPullRequestsAsync(Guid projectId, bool? linked, int page, int pageSize, CancellationToken cancellationToken = default);
    // Busca pontual no tenant para vínculo explícito; não percorrer todas as páginas para achar um ID.
    Task<GitPullRequestObservationDto?> GetPullRequestObservationAsync(Guid projectId, Guid observationId,
        CancellationToken cancellationToken = default) => Task.FromResult<GitPullRequestObservationDto?>(null);
    Task<GitDeliveryReceipt> AcceptDeliveryAsync(Guid integrationId, string deliveryId, byte[] bodyHash, NormalizedGitEvent normalizedEvent, string correlationId, CancellationToken cancellationToken = default);
    // Claim global só de metadados; o worker inicializa tenant antes de carregar a entrega/task.
    Task<GitDeliveryLease?> ClaimNextDeliveryAsync(DateTimeOffset nowUtc, TimeSpan lease, CancellationToken cancellationToken = default);
    Task<GitDeliverySnapshot?> GetDeliveryAsync(Guid deliveryId, CancellationToken cancellationToken = default);
    Task<bool> CompleteDeliveryAsync(Guid deliveryId, byte[] expectedRowVersion, string status, string? errorCode, DateTimeOffset? nextAttemptAtUtc, CancellationToken cancellationToken = default);
    Task<bool> HasAppliedFactAsync(Guid projectId, Guid workItemId, byte[] semanticKey, CancellationToken cancellationToken = default);
}
