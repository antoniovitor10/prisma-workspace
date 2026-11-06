using System.Globalization;
using MediatR;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Features.MethodGit.Repositories;

public sealed class GitPullRequestOperations(IMethodGitStore store, IGitProvider provider, GitRepositoryOperations projects,
    IMethodAccessContext access, IPermissionService permissions)
{
    public async Task<GitPullRequestPage> ListAsync(Guid projectId, bool? linked, int page, int size, string actor, CancellationToken ct)
    {
        await projects.EnsureAccessAsync(projectId, actor, false, ct);
        return await store.ListPullRequestsAsync(projectId, linked, page, size, ct);
    }
    public async Task<TaskGitLinkDto> LinkUrlAsync(Guid projectId, string methodId, LinkMethodPullRequestRequest request, string actor, CancellationToken ct)
    {
        await projects.EnsureAccessAsync(projectId, actor, false, ct);
        var task = await SingleTaskAsync(projectId, methodId, ct);
        var (fullName, number) = ParseUrl(request.Url);
        GitRepositoryDescriptor? matched = null;
        var repositories = await store.ListRepositoriesAsync(projectId, ct);
        foreach (var repository in repositories.Where(r => r.Enabled).OrderByDescending(r => r.RepositoryFullName.Equals(fullName, StringComparison.OrdinalIgnoreCase)))
        {
            // URL nunca é requisitada: confirma o nome contra o ID estável e a instalação já cadastrados.
            var candidate = repository with { RepositoryFullName = fullName };
            if ((await provider.ValidateInstallationAsync(candidate, ct)).Allowed) { matched = candidate; break; }
        }
        if (matched is null) throw new MethodOperationException(400, "unregistered_git_repository", "URL não pertence a repositório autorizado e acessível deste projeto.");
        await EnsureLinkPermission(task.WorkItemId, actor, ct);
        var pr = await provider.GetPullRequestAsync(matched, number, ct);
        var observation = await store.UpsertObservationAsync(matched.Id, pr, "explicit_confirmation_pending", ct);
        return await store.LinkExplicitAsync(matched.Id, task.WorkItemId, observation.Id, request.Reason, ct);
    }
    public async Task<TaskGitLinkDto> LinkBranchAsync(Guid projectId, string methodId, LinkMethodBranchRequest request, string actor, CancellationToken ct)
    {
        await projects.EnsureAccessAsync(projectId, actor, false, ct);
        var task = await SingleTaskAsync(projectId, methodId, ct);
        if (!GitBranchName.IsValid(request.Branch)) throw GitBranchName.InvalidError();
        if (GitBranchName.ReferencesOtherTask(request.Branch, task.MethodTaskId)) throw GitBranchName.OtherTaskError(task.MethodTaskId);
        var fullName = request.Repository?.Trim() ?? string.Empty;
        var parts = fullName.Split('/');
        if (parts.Length != 2 || parts.Any(p => p.Length == 0 || p is "." or ".." || p.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))))
            throw new MethodOperationException(400, "invalid_git_repository", "Informe o repositório no formato dono/repositório.");
        // Só repositórios habilitados deste projeto; a branch pode ainda não existir no GitHub, então não há leitura no provedor.
        var repository = (await store.ListRepositoriesAsync(projectId, ct))
            .SingleOrDefault(r => r.Enabled && r.RepositoryFullName.Equals(fullName, StringComparison.OrdinalIgnoreCase))
            ?? throw new MethodOperationException(400, "unregistered_git_repository", "Repositório não cadastrado ou desabilitado neste projeto.");
        await EnsureLinkPermission(task.WorkItemId, actor, ct);
        return await store.LinkBranchAsync(repository.Id, task.WorkItemId, request.Branch, ct);
    }
    public async Task<TaskGitLinkDto> LinkObservedAsync(Guid projectId, Guid observationId, LinkObservedPullRequestRequest request, string actor, CancellationToken ct)
    {
        await projects.EnsureAccessAsync(projectId, actor, false, ct);
        await EnsureLinkPermission(request.WorkItemId, actor, ct);
        var observation = await store.GetPullRequestObservationAsync(projectId, observationId, ct);
        if (observation is null) throw new MethodOperationException(404, "git_observation_not_found", "PR observado não encontrado neste projeto.");
        var repository = await store.GetRepositoryAsync(observation.IntegrationId, ct);
        if (repository is null || !repository.Enabled || repository.ProjectId != projectId) throw new MethodOperationException(409, "git_repository_unavailable", "Integração do PR indisponível.");
        var pr = await provider.GetPullRequestAsync(repository, observation.PullRequestNumber, ct);
        await store.UpsertObservationAsync(repository.Id, pr, "explicit_confirmation_pending", ct);
        return await store.LinkExplicitAsync(repository.Id, request.WorkItemId, observation.Id, request.Reason, ct);
    }
    private async Task<MethodTaskSnapshot> SingleTaskAsync(Guid projectId, string methodId, CancellationToken ct)
    {
        if (methodId.Length is < 6 or > 32 || !methodId.StartsWith("T-", StringComparison.Ordinal)
            || !methodId[2..].All(char.IsAsciiDigit)) throw new MethodOperationException(400, "invalid_method_task_id", "ID da task inválido; use T-0001.");
        var tasks = await store.FindTasksAsync(projectId, methodId, ct);
        if (tasks.Count != 1 || !tasks[0].IsTopLevel || tasks[0].IsArchived) throw new MethodOperationException(409, "ambiguous_method_task", "Task ausente, ambígua ou fora do método.");
        return tasks[0];
    }
    private async Task EnsureLinkPermission(Guid id, string actorId, CancellationToken ct)
    {
        var actor = access.Current;
        if (actor is null || actor.ActorId != actorId || actor.Role == MethodRole.System
            || actor.AuthenticationKind is not (MethodAuthenticationKind.UiSession or MethodAuthenticationKind.MethodToken))
            throw new MethodOperationException(403, "git_link_permission", "O principal não pode confirmar vínculos explícitos.");
        await permissions.EnsureAsync(actorId, PlatformPermission.Edit, PermissionScope.WorkItem, id, ct);
        await permissions.EnsureAsync(actorId, PlatformPermission.Comment, PermissionScope.WorkItem, id, ct);
    }
    private static (string FullName, int Number) ParseUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "github.com" || !uri.IsDefaultPort
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0) throw InvalidUrl();
        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || parts[2] != "pull" || parts.Take(2).Any(p => p is "." or ".." || p.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')))
            || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0) throw InvalidUrl();
        return ($"{parts[0]}/{parts[1]}", number);
    }
    private static MethodOperationException InvalidUrl() => new(400, "invalid_git_pull_request_url", "Use uma URL canônica HTTPS do GitHub no formato dono/repositório/pull/número, sem credenciais ou parâmetros.");
}

public sealed record ListMethodGitPullRequestsQuery(Guid ProjectId, bool? Linked, int Page, int PageSize, string ActorId) : IRequest<GitPullRequestPage>;
public sealed class ListMethodGitPullRequestsHandler(GitPullRequestOperations operations) : IRequestHandler<ListMethodGitPullRequestsQuery, GitPullRequestPage>
{ public Task<GitPullRequestPage> Handle(ListMethodGitPullRequestsQuery r, CancellationToken ct) => operations.ListAsync(r.ProjectId, r.Linked, r.Page, r.PageSize, r.ActorId, ct); }
public sealed record LinkMethodGitPullRequestCommand(Guid ProjectId, string MethodId, LinkMethodPullRequestRequest Data, string ActorId) : IRequest<TaskGitLinkDto>;
public sealed class LinkMethodGitPullRequestHandler(GitPullRequestOperations operations) : IRequestHandler<LinkMethodGitPullRequestCommand, TaskGitLinkDto>
{ public Task<TaskGitLinkDto> Handle(LinkMethodGitPullRequestCommand r, CancellationToken ct) => operations.LinkUrlAsync(r.ProjectId, r.MethodId, r.Data, r.ActorId, ct); }
public sealed record LinkObservedMethodGitPullRequestCommand(Guid ProjectId, Guid ObservationId, LinkObservedPullRequestRequest Data, string ActorId) : IRequest<TaskGitLinkDto>;
public sealed class LinkObservedMethodGitPullRequestHandler(GitPullRequestOperations operations) : IRequestHandler<LinkObservedMethodGitPullRequestCommand, TaskGitLinkDto>
{ public Task<TaskGitLinkDto> Handle(LinkObservedMethodGitPullRequestCommand r, CancellationToken ct) => operations.LinkObservedAsync(r.ProjectId, r.ObservationId, r.Data, r.ActorId, ct); }
public sealed record LinkMethodGitBranchCommand(Guid ProjectId, string MethodId, LinkMethodBranchRequest Data, string ActorId) : IRequest<TaskGitLinkDto>;
public sealed class LinkMethodGitBranchHandler(GitPullRequestOperations operations) : IRequestHandler<LinkMethodGitBranchCommand, TaskGitLinkDto>
{ public Task<TaskGitLinkDto> Handle(LinkMethodGitBranchCommand r, CancellationToken ct) => operations.LinkBranchAsync(r.ProjectId, r.MethodId, r.Data, r.ActorId, ct); }
