using FluentValidation;
using MediatR;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Features.MethodGit.Repositories;

/// <summary>
/// HU-0007 · T-0026: rascunho de título e corpo do PR da task, gravado pelo agente (MCP <c>preparar_pr</c>) ou por uma pessoa.
/// Fica no histórico da task (<c>git_pr_draft_saved</c>); o atual é o último evento.
/// </summary>
public sealed class GitPullRequestDrafts(IMethodGitStore store, GitRepositoryOperations projects, GitPullRequestOperations tasks,
    IMethodAccessContext access, IPermissionService permissions)
{
    public async Task<MethodPullRequestDraftDto> GetAsync(Guid projectId, string methodId, string actorId, CancellationToken ct)
    {
        await projects.EnsureAccessAsync(projectId, actorId, false, ct);
        var task = await tasks.SingleTaskAsync(projectId, methodId, ct);
        await permissions.EnsureAsync(actorId, PlatformPermission.View, PermissionScope.WorkItem, task.WorkItemId, ct);
        return await store.GetPullRequestDraftAsync(task.WorkItemId, ct)
            ?? throw new MethodOperationException(404, "pr_draft_not_found", "A task ainda não tem rascunho de PR.");
    }

    public async Task<MethodPullRequestDraftDto> SaveAsync(Guid projectId, string methodId, SaveMethodPullRequestDraftRequest request,
        string actorId, CancellationToken ct)
    {
        await projects.EnsureAccessAsync(projectId, actorId, false, ct);
        var task = await tasks.SingleTaskAsync(projectId, methodId, ct);
        var actor = access.Current;
        // Agente e humano (token ou sessão da UI) gravam; principal de sistema (integração) não.
        if (actor is null || actor.ActorId != actorId || actor.Role == MethodRole.System
            || actor.AuthenticationKind is not (MethodAuthenticationKind.UiSession or MethodAuthenticationKind.MethodToken))
            throw new MethodOperationException(403, "git_pr_draft_permission", "O principal não pode gravar o rascunho do PR.");
        await permissions.EnsureAsync(actorId, PlatformPermission.Edit, PermissionScope.WorkItem, task.WorkItemId, ct);
        return await store.SavePullRequestDraftAsync(task.WorkItemId, request.Title.Trim(), request.Body ?? string.Empty, ct);
    }
}

public sealed record GetMethodPullRequestDraftQuery(Guid ProjectId, string MethodId, string ActorId) : IRequest<MethodPullRequestDraftDto>;
public sealed class GetMethodPullRequestDraftHandler(GitPullRequestDrafts drafts) : IRequestHandler<GetMethodPullRequestDraftQuery, MethodPullRequestDraftDto>
{
    public Task<MethodPullRequestDraftDto> Handle(GetMethodPullRequestDraftQuery r, CancellationToken ct) => drafts.GetAsync(r.ProjectId, r.MethodId, r.ActorId, ct);
}

public sealed record SaveMethodPullRequestDraftCommand(Guid ProjectId, string MethodId, SaveMethodPullRequestDraftRequest Data, string ActorId)
    : IRequest<MethodPullRequestDraftDto>;
public sealed class SaveMethodPullRequestDraftHandler(GitPullRequestDrafts drafts) : IRequestHandler<SaveMethodPullRequestDraftCommand, MethodPullRequestDraftDto>
{
    public Task<MethodPullRequestDraftDto> Handle(SaveMethodPullRequestDraftCommand r, CancellationToken ct) => drafts.SaveAsync(r.ProjectId, r.MethodId, r.Data, r.ActorId, ct);
}

public sealed class SaveMethodPullRequestDraftCommandValidator : AbstractValidator<SaveMethodPullRequestDraftCommand>
{
    public SaveMethodPullRequestDraftCommandValidator()
    {
        RuleFor(x => x.Data).NotNull().WithMessage("Informe o título e o corpo do rascunho do PR.");
        RuleFor(x => x.Data.Title).Cascade(CascadeMode.Stop)
            .Must(title => !string.IsNullOrWhiteSpace(title)).WithMessage("Informe o título do PR.")
            .Must(title => title.Trim().Length <= MethodPullRequestLimits.TitleMaxLength)
            .WithMessage($"O título do PR deve ter até {MethodPullRequestLimits.TitleMaxLength} caracteres.")
            .When(x => x.Data is not null);
        RuleFor(x => x.Data.Body).Must(body => (body?.Length ?? 0) <= MethodPullRequestLimits.BodyMaxLength)
            .WithMessage("O corpo do PR deve ter até 60.000 caracteres.")
            .When(x => x.Data is not null);
    }
}
