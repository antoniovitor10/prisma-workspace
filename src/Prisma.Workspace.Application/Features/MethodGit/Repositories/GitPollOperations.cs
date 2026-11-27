using MediatR;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Features.MethodGit.Repositories;

/// <summary>
/// "Consultar agora" (HU-0002, CA-10): no repositório ou na aba Git da tarefa. Só sessão humana interativa
/// (token de agente não dispara consultas ao GitHub); uma consulta por minuto por integração.
/// </summary>
public sealed class GitPollOperations(IMethodGitStore store, GitRepositoryOperations projects, IMethodAccessContext access,
    IPermissionService permissions, IGitPollRunner runner)
{
    public async Task<GitPollResultDto> PollRepositoryAsync(Guid projectId, Guid integrationId, string actorId, CancellationToken ct)
    {
        await EnsureHumanAsync(projectId, actorId, ct);
        var repository = await store.GetRepositoryAsync(integrationId, ct);
        if (repository is null || repository.ProjectId != projectId) throw new MethodOperationException(404, "git_repository_not_found", "Integração não encontrada.");
        if (!repository.IsPoll) throw new MethodOperationException(409, "git_repository_not_polled", "Este repositório recebe o webhook da GitHub App; não há consulta a fazer.");
        if (!repository.Enabled) throw new MethodOperationException(409, "git_repository_unavailable", "Integração pausada.");
        return await runner.PollNowAsync(integrationId, ct);
    }

    /// <summary>Consulta os repositórios no modo consulta dos vínculos da tarefa (ou todos os do projeto, se ela ainda não tem vínculo).</summary>
    public async Task<IReadOnlyList<GitPollResultDto>> PollTaskAsync(Guid projectId, Guid workItemId, string actorId, CancellationToken ct)
    {
        await EnsureHumanAsync(projectId, actorId, ct);
        var task = await store.GetTaskAsync(workItemId, ct);
        if (task is null || task.ProjectId != projectId) throw new MethodOperationException(404, "method_task_not_found", "Task do método não encontrada neste projeto.");
        await permissions.EnsureAsync(actorId, PlatformPermission.View, PermissionScope.WorkItem, workItemId, ct);
        var repositories = (await store.ListRepositoriesAsync(projectId, ct)).Where(r => r.IsPoll && r.Enabled).ToList();
        var linked = (await store.GetTaskLinksAsync(workItemId, ct)).Select(l => l.IntegrationId).ToHashSet();
        var targets = repositories.Where(r => linked.Contains(r.Id)).ToList();
        if (targets.Count == 0) targets = repositories;
        if (targets.Count == 0) throw new MethodOperationException(409, "git_repository_not_polled", "Nenhum repositório deste projeto está no modo consulta.");
        var results = new List<GitPollResultDto>();
        MethodOperationException? tooSoon = null;
        foreach (var repository in targets)
        {
            try { results.Add(await runner.PollNowAsync(repository.Id, ct)); }
            catch (MethodOperationException exception) when (exception.StatusCode == 429) { tooSoon = exception; }
        }
        if (results.Count == 0 && tooSoon is not null) throw tooSoon;
        return results;
    }

    private async Task EnsureHumanAsync(Guid projectId, string actorId, CancellationToken ct)
    {
        await projects.EnsureAccessAsync(projectId, actorId, false, ct);
        if (access.Current is not { IsInteractiveHuman: true })
            throw new MethodOperationException(403, "git_poll_permission", "Só uma pessoa na tela dispara a consulta ao GitHub.");
    }
}

public sealed record PollMethodGitRepositoryCommand(Guid ProjectId, Guid IntegrationId, string ActorId) : IRequest<GitPollResultDto>;
public sealed class PollMethodGitRepositoryHandler(GitPollOperations operations) : IRequestHandler<PollMethodGitRepositoryCommand, GitPollResultDto>
{ public Task<GitPollResultDto> Handle(PollMethodGitRepositoryCommand r, CancellationToken ct) => operations.PollRepositoryAsync(r.ProjectId, r.IntegrationId, r.ActorId, ct); }
public sealed record PollMethodTaskGitCommand(Guid ProjectId, Guid WorkItemId, string ActorId) : IRequest<IReadOnlyList<GitPollResultDto>>;
public sealed class PollMethodTaskGitHandler(GitPollOperations operations) : IRequestHandler<PollMethodTaskGitCommand, IReadOnlyList<GitPollResultDto>>
{ public Task<IReadOnlyList<GitPollResultDto>> Handle(PollMethodTaskGitCommand r, CancellationToken ct) => operations.PollTaskAsync(r.ProjectId, r.WorkItemId, r.ActorId, ct); }
