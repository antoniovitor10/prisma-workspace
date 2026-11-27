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
    // Vínculo de branch antes do push (BranchState = local); valida nome, repositório e permissão; grava git_branch_linked.
    Task<TaskGitLinkDto> LinkBranchAsync(Guid integrationId, Guid workItemId, string branchName, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
    // Marca a branch vista no GitHub (BranchState = remote) e grava git_branch_pushed; NotLinked quando não há vínculo da branch.
    Task<GitBranchPushResult> MarkBranchPushedAsync(Guid integrationId, string branchName, string? observedGitActorId = null,
        string? observedGitActorName = null, CancellationToken cancellationToken = default)
        => Task.FromResult(GitBranchPushResult.NotLinked);
    // Resumo das revisões efetivas na head atual (D4), para o card não chamar o GitHub; head diferente não grava nada.
    Task UpdateObservationReviewsAsync(Guid integrationId, int pullRequestNumber, string headSha, int approvals, bool changesRequested,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
    // Leitura agregada da aba Git (vínculos, observações e linha do tempo) de uma task do método.
    Task<TaskGitViewDto?> GetTaskGitViewAsync(Guid workItemId, CancellationToken cancellationToken = default)
        => Task.FromResult<TaskGitViewDto?>(null);
    // Resultado da leitura do modo consulta: código sem texto do GitHub; LastPolledAtUtc só avança com sucesso.
    Task RecordPollResultAsync(Guid integrationId, string status, bool success, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
    // Tasks com vínculo de branch exato (sem PR) na integração: resolve PR de branch sem ID (D-2 da T-0004).
    Task<IReadOnlyList<Guid>> FindBranchLinkTasksAsync(Guid integrationId, string branchName, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Guid>>([]);
    // Vínculos (branches e PRs) da integração, para a consulta decidir o que ler.
    Task<IReadOnlyList<TaskGitLinkDto>> ListIntegrationLinksAsync(Guid integrationId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TaskGitLinkDto>>([]);
    // Entrada interna "poll:" na fila do webhook (D1); falso quando o mesmo ID já existe (fato já enfileirado).
    Task<bool> EnqueuePollEvaluationAsync(Guid integrationId, GitPullRequestSnapshot pullRequest, string deliveryId, string correlationId,
        CancellationToken cancellationToken = default) => Task.FromResult(false);
    // Rascunho do PR (HU-0007): o atual é o último evento git_pr_draft_saved da task; null quando não há.
    Task<MethodPullRequestDraftDto?> GetPullRequestDraftAsync(Guid workItemId, CancellationToken cancellationToken = default)
        => Task.FromResult<MethodPullRequestDraftDto?>(null);
    // Grava git_pr_draft_saved com o ator do contexto; texto igual ao atual não grava evento (Changed = false).
    Task<MethodPullRequestDraftDto> SavePullRequestDraftAsync(Guid workItemId, string title, string body, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
    // Eventos git_pr_draft_saved e git_pr_opened da task como entradas da linha do tempo da aba Git (sem o corpo).
    Task<IReadOnlyList<TaskGitTimelineEntryDto>> GetPullRequestTimelineAsync(Guid workItemId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TaskGitTimelineEntryDto>>([]);
    // Evento git_pr_opened do botão Abrir PR; nunca recebe token nem corpo do PR.
    Task RecordPullRequestOpenedAsync(Guid workItemId, GitPullRequestOpenedRecord record, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
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
