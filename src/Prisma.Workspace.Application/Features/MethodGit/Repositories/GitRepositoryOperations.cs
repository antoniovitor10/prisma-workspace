using System.Text;
using System.Text.RegularExpressions;
using MediatR;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Features.MethodGit.Repositories;

public sealed partial class GitRepositoryOperations(IMethodGitStore store, IGitProvider provider,
    IMethodGitSecretStore secrets, IMethodAccessContext access, IPermissionService permissions, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    public async Task<IReadOnlyList<GitRepositoryDto>> ListAsync(Guid projectId, string actorId, CancellationToken ct)
    {
        await EnsureAccessAsync(projectId, actorId, false, ct);
        return (await store.ListRepositoriesAsync(projectId, ct)).Select(ToDto).ToArray();
    }
    public async Task<GitRepositoryDto> RegisterAsync(Guid projectId, RegisterGitRepositoryRequest request, string actorId, CancellationToken ct)
    {
        var project = await EnsureAccessAsync(projectId, actorId, true, ct);
        if (!string.Equals(request.Host, "github.com", StringComparison.OrdinalIgnoreCase) || request.RepositoryId <= 0
            || request.AppId <= 0 || request.InstallationId <= 0 || request.SystemPrincipalId == Guid.Empty
            || request.RepositoryFullName.Length > 300 || !RepositoryPattern().IsMatch(request.RepositoryFullName))
            throw Invalid("Cadastro GitHub inválido; confira IDs, host e nome do repositório.");
        ValidateBranch(request.DefaultBranch);
        ValidateRotation(request.WebhookSecretRef, request.PreviousWebhookSecretRef, request.PreviousSecretValidUntilUtc);
        await ValidateWebhookSecret(request.WebhookSecretRef, ct);
        var repository = new GitRepositoryDescriptor(Guid.NewGuid(), project.OrganizationId, projectId, "github", "github.com", request.RepositoryId,
            request.RepositoryFullName, request.DefaultBranch, request.AppId, request.InstallationId, request.AppPrivateKeySecretRef,
            request.WebhookSecretRef, request.PreviousWebhookSecretRef, request.PreviousSecretValidUntilUtc, request.SystemPrincipalId, request.Enabled);
        var validation = await provider.ValidateInstallationAsync(repository, ct);
        if (!validation.Allowed) throw new MethodOperationException(503, validation.ErrorCode ?? "git_installation_unavailable",
            validation.Message ?? "Instalação GitHub sem acesso ao repositório.");
        await store.SaveRepositoryAsync(repository, ct);
        return ToDto(repository);
    }
    public async Task<GitRepositoryDto> UpdateAsync(Guid projectId, Guid integrationId, UpdateGitRepositoryRequest request, string actorId, CancellationToken ct)
    {
        await EnsureAccessAsync(projectId, actorId, true, ct);
        var old = await store.GetRepositoryAsync(integrationId, ct);
        if (old is null || old.ProjectId != projectId) throw new MethodOperationException(404, "git_repository_not_found", "Integração não encontrada.");
        var currentRef = request.WebhookSecretRef ?? old.WebhookSecretRef;
        var rotating = currentRef != old.WebhookSecretRef;
        var previousRef = request.PreviousWebhookSecretRef ?? (rotating ? old.WebhookSecretRef : old.PreviousWebhookSecretRef);
        var until = request.PreviousSecretValidUntilUtc ?? (rotating || request.PreviousWebhookSecretRef is not null ? _clock.GetUtcNow().AddHours(24) : old.PreviousSecretValidUntilUtc);
        if (!rotating && request.PreviousWebhookSecretRef is null && until <= _clock.GetUtcNow()) { previousRef = null; until = null; }
        ValidateBranch(request.DefaultBranch ?? old.DefaultBranch);
        ValidateRotation(currentRef, previousRef, until);
        await ValidateWebhookSecret(currentRef, ct);
        var repository = old with { Enabled = request.Enabled, DefaultBranch = request.DefaultBranch ?? old.DefaultBranch,
            AppPrivateKeySecretRef = request.AppPrivateKeySecretRef ?? old.AppPrivateKeySecretRef, WebhookSecretRef = currentRef,
            PreviousWebhookSecretRef = previousRef, PreviousSecretValidUntilUtc = until };
        if (repository.Enabled)
        {
            var validation = await provider.ValidateInstallationAsync(repository, ct);
            if (!validation.Allowed) throw new MethodOperationException(503, validation.ErrorCode ?? "git_installation_unavailable", "Acesso da GitHub App indisponível; confira a instalação e a configuração externa.");
        }
        await store.SaveRepositoryAsync(repository, ct);
        return ToDto(repository);
    }
    internal async Task<MethodProjectSnapshot> EnsureAccessAsync(Guid projectId, string actorId, bool admin, CancellationToken ct)
    {
        var actor = access.Current;
        if (actor is null || actor.ActorId != actorId || (!actor.IsInteractiveHuman && actor.ProjectId != projectId)
            || (admin && !actor.IsInteractiveHuman))
            throw new MethodOperationException(403, "git_project_permission", "Operação não autorizada no projeto; configuração exige sessão humana administrativa.");
        await permissions.EnsureAsync(actorId, admin ? PlatformPermission.ManagePermissions : PlatformPermission.View, PermissionScope.Project, projectId, ct);
        return await store.GetProjectAsync(projectId, ct) ?? throw new MethodOperationException(404, "method_not_provisioned", "Método não provisionado neste projeto.");
    }
    private async Task ValidateWebhookSecret(string reference, CancellationToken ct)
    {
        var value = await secrets.ResolveAsync(reference, ct);
        if (Encoding.UTF8.GetByteCount(value) < 32) throw Invalid("O segredo externo do webhook deve ter pelo menos 32 bytes aleatórios e ser exclusivo deste repositório.");
    }
    private void ValidateRotation(string current, string? previous, DateTimeOffset? until)
    {
        if ((previous is null) != (until is null) || (previous is not null && (previous == current || until <= _clock.GetUtcNow() || until > _clock.GetUtcNow().AddHours(24))))
            throw Invalid("Rotação HMAC exige referência anterior distinta e validade futura de até 24 horas.");
    }
    private static void ValidateBranch(string branch)
    {
        if (string.IsNullOrWhiteSpace(branch) || branch.Length > 255 || branch.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            throw Invalid("Nome da branch principal inválido.");
    }
    private static MethodOperationException Invalid(string message) => new(400, "invalid_git_configuration", message);
    private static GitRepositoryDto ToDto(GitRepositoryDescriptor r) => new(r.Id, r.ProjectId, r.Host, r.RepositoryId, r.RepositoryFullName,
        r.DefaultBranch, r.AppId, r.InstallationId, r.SystemPrincipalId, r.Enabled, r.OperationalError);
    [GeneratedRegex("^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant)] private static partial Regex RepositoryPattern();
}

public sealed record ListMethodGitRepositoriesQuery(Guid ProjectId, string ActorId) : IRequest<IReadOnlyList<GitRepositoryDto>>;
public sealed class ListMethodGitRepositoriesHandler(GitRepositoryOperations operations) : IRequestHandler<ListMethodGitRepositoriesQuery, IReadOnlyList<GitRepositoryDto>>
{ public Task<IReadOnlyList<GitRepositoryDto>> Handle(ListMethodGitRepositoriesQuery request, CancellationToken ct) => operations.ListAsync(request.ProjectId, request.ActorId, ct); }
public sealed record RegisterMethodGitRepositoryCommand(Guid ProjectId, RegisterGitRepositoryRequest Data, string ActorId) : IRequest<GitRepositoryDto>;
public sealed class RegisterMethodGitRepositoryHandler(GitRepositoryOperations operations) : IRequestHandler<RegisterMethodGitRepositoryCommand, GitRepositoryDto>
{ public Task<GitRepositoryDto> Handle(RegisterMethodGitRepositoryCommand request, CancellationToken ct) => operations.RegisterAsync(request.ProjectId, request.Data, request.ActorId, ct); }
public sealed record UpdateMethodGitRepositoryCommand(Guid ProjectId, Guid IntegrationId, UpdateGitRepositoryRequest Data, string ActorId) : IRequest<GitRepositoryDto>;
public sealed class UpdateMethodGitRepositoryHandler(GitRepositoryOperations operations) : IRequestHandler<UpdateMethodGitRepositoryCommand, GitRepositoryDto>
{ public Task<GitRepositoryDto> Handle(UpdateMethodGitRepositoryCommand request, CancellationToken ct) => operations.UpdateAsync(request.ProjectId, request.IntegrationId, request.Data, request.ActorId, ct); }
