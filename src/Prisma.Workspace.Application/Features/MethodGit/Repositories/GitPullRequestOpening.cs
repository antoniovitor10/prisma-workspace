using FluentValidation;
using MediatR;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Features.MethodGit.Repositories;

/// <summary>
/// HU-0007 · T-0027: abre o PR da branch da task pela GitHub App (ou vincula o PR aberto que já existe), com "[T-XXXX] " no título
/// e o link do card no corpo, e move o card de em-desenvolvimento para em-revisao como a pessoa que clicou (D4).
/// Só sessão da UI (D8): o agente prepara o rascunho e para ali (ADR-0001).
/// </summary>
public sealed class GitPullRequestOpening(IMethodGitStore store, IGitProvider provider, GitRepositoryOperations projects,
    GitPullRequestOperations tasks, IMethodAccessContext access, IPermissionService permissions, IMethodMoveService movement,
    IMethodCardLinks cardLinks, IAutomationExecutor automation, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<OpenMethodPullRequestResult> OpenAsync(Guid projectId, string methodId, OpenMethodPullRequestRequest request,
        string actorId, CancellationToken ct)
    {
        // D8: antes de qualquer leitura; token do método (agente ou humano) nunca abre PR.
        var actor = access.Current;
        if (actor is null || actor.ActorId != actorId || !actor.IsInteractiveHuman)
            throw new MethodOperationException(403, "interactive_pr_required",
                "Abrir PR exige a pessoa autenticada na tela do Prisma; tokens do método não abrem PR (o agente só prepara o rascunho).");
        var project = await projects.EnsureAccessAsync(projectId, actorId, false, ct);
        var task = await tasks.SingleTaskAsync(projectId, methodId, ct);
        // CA-8: quem abre precisa poder editar, comentar e mudar o status da task.
        await permissions.EnsureAsync(actorId, PlatformPermission.Edit, PermissionScope.WorkItem, task.WorkItemId, ct);
        await permissions.EnsureAsync(actorId, PlatformPermission.Comment, PermissionScope.WorkItem, task.WorkItemId, ct);
        await permissions.EnsureAsync(actorId, PlatformPermission.ChangeStatus, PermissionScope.WorkItem, task.WorkItemId, ct);

        var link = (await store.GetTaskLinksAsync(task.WorkItemId, ct)).SingleOrDefault(x => x.Id == request.LinkId)
            ?? throw new MethodOperationException(404, "git_link_not_found", "Vínculo de branch não encontrado nesta task.");
        if (link.ObservationId is not null || string.IsNullOrEmpty(link.BranchName))
            throw new MethodOperationException(409, "git_link_not_branch", "O vínculo escolhido não é de branch.");
        var branch = link.BranchName;
        if (link.BranchState != GitBranchStates.Remote)
            throw new MethodOperationException(409, "git_branch_not_pushed", $"A branch {branch} ainda não está no GitHub. Faça o push antes de abrir o PR.");
        var repository = await store.GetRepositoryAsync(link.IntegrationId, ct);
        if (repository is null || repository.ProjectId != projectId || !repository.Enabled)
            throw new MethodOperationException(409, "git_repository_disabled", "O repositório da branch não está habilitado neste projeto.");
        if (repository.Mode != GitIntegrationModes.App)
            throw new MethodOperationException(409, "git_repository_not_app",
                "Este repositório não tem a GitHub App: abra o PR pela página de comparação do GitHub.");
        var baseBranch = repository.DefaultBranch;

        GitPullRequestSnapshot pr;
        string outcome;
        try
        {
            // RF7: PR aberto com a mesma head é vinculado; nada é criado no GitHub.
            var existing = await provider.FindOpenPullRequestByHeadAsync(repository, branch, ct);
            if (existing is not null) (pr, outcome) = (existing, MethodPullRequestOutcomes.LinkedExisting);
            else
            {
                var creation = new GitPullRequestCreation(branch, baseBranch, MethodPullRequestText.EnsureTaskPrefix(methodId, request.Title),
                    MethodPullRequestText.AppendCardLine(request.Body, cardLinks.CardUrl(projectId, task.WorkItemId)), request.Draft);
                try { (pr, outcome) = (await provider.CreatePullRequestAsync(repository, creation, ct), MethodPullRequestOutcomes.Opened); }
                catch (GitPullRequestRejectedException rejected) when (rejected.Code == GitPullRequestErrorCodes.AlreadyExists)
                {
                    // Corrida (duplo clique, outra aba, PR aberto à mão agora): vincula o que o GitHub diz que já existe.
                    pr = await provider.FindOpenPullRequestByHeadAsync(repository, branch, ct)
                        ?? throw new GitPullRequestRejectedException(GitPullRequestErrorCodes.Invalid);
                    outcome = MethodPullRequestOutcomes.LinkedExisting;
                }
            }
        }
        catch (GitPullRequestRejectedException rejected) { throw Translate(rejected, branch, baseBranch); }

        try
        {
            var observation = await store.UpsertObservationAsync(repository.Id, pr, "explicit_confirmation_pending", ct);
            await store.LinkExplicitAsync(repository.Id, task.WorkItemId, observation.Id, $"PR #{pr.Number} aberto pelo Prisma.", ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Único erro com algo fora do Prisma: o próximo clique cai no RF7 e vincula; o webhook também vincula pela branch.
            throw new MethodOperationException(502, GitPullRequestErrorCodes.LinkFailed, outcome == MethodPullRequestOutcomes.Opened
                ? $"O PR #{pr.Number} foi aberto no GitHub, mas não consegui vinculá-lo. Clique de novo para vincular."
                : $"Já havia um PR (#{pr.Number}), mas não consegui vinculá-lo. Clique de novo para vincular.");
        }

        // D4: só anda de em-desenvolvimento e com PR que não é rascunho; relê a task depois do vínculo.
        var current = await store.GetTaskAsync(task.WorkItemId, ct) ?? task;
        var moved = false;
        string? moveError = null;
        if (current.StatusCode == MethodStatusCodes.InDevelopment && !pr.IsDraft
            && project.StageMappings.TryGetValue(MethodStatusCodes.InReview, out var reviewStage))
        {
            try
            {
                var result = await movement.MoveAsync(new MethodMoveRequest(task.WorkItemId, reviewStage, $"PR #{pr.Number} aberto pelo Prisma",
                    ExpectedRowVersion: current.RowVersion), ct);
                moved = result.Applied;
                if (result.Applied) await automation.ExecuteStageEnteredAsync(task.WorkItemId, reviewStage, actorId, ct);
            }
            catch (MethodOperationException exception)
            {
                // A reavaliação do vínculo (worker) pode ter movido o card no meio: aí ele já está onde deveria.
                current = await store.GetTaskAsync(task.WorkItemId, ct) ?? current;
                if (current.StatusCode != MethodStatusCodes.InReview) moveError = exception.Message;
            }
        }
        await store.RecordPullRequestOpenedAsync(task.WorkItemId,
            new(repository.Id, repository.RepositoryFullName, pr.Number, pr.Url, pr.IsDraft, outcome, moved), ct);
        return new(outcome, pr.Number, pr.Url, moved, Message(outcome, pr, moved, current.StatusCode, moveError));
    }

    private static string Message(string outcome, GitPullRequestSnapshot pr, bool moved, string status, string? moveError)
    {
        var head = outcome == MethodPullRequestOutcomes.LinkedExisting
            ? $"Já havia um PR, vinculei o #{pr.Number}"
            : pr.IsDraft ? $"PR #{pr.Number} aberto como rascunho e vinculado" : $"PR #{pr.Number} aberto";
        if (moved) return $"{head}; o card foi para Em revisão.";
        if (moveError is not null) return $"{head}; o card não foi movido: {moveError}";
        if (status == MethodStatusCodes.InReview) return outcome == MethodPullRequestOutcomes.Opened && !pr.IsDraft ? $"{head} e vinculado." : $"{head}.";
        if (status == MethodStatusCodes.InDevelopment && pr.IsDraft) return $"{head}; o card vai para Em revisão quando o PR sair do rascunho.";
        return outcome == MethodPullRequestOutcomes.Opened && !pr.IsDraft
            ? $"{head} e vinculado; o card ficou em {StatusLabel(status)}."
            : $"{head}; o card ficou em {StatusLabel(status)}.";
    }

    internal static string StatusLabel(string status) => status switch
    {
        MethodStatusCodes.Backlog => "Backlog",
        MethodStatusCodes.Ready => "Pronta",
        MethodStatusCodes.InDevelopment => "Em desenvolvimento",
        MethodStatusCodes.InReview => "Em revisão",
        MethodStatusCodes.InTest => "Em teste",
        MethodStatusCodes.Completed => "Concluída",
        MethodStatusCodes.Blocked => "Bloqueada",
        _ => status,
    };

    // D5: recusa do GitHub vira mensagem em português, sem texto bruto do GitHub.
    private MethodOperationException Translate(GitPullRequestRejectedException rejected, string branch, string baseBranch) => rejected.Code switch
    {
        GitPullRequestErrorCodes.PermissionMissing => new(409, rejected.Code,
            "A App ainda não tem permissão para abrir PR neste repositório. Aceite a nova permissão da instalação no GitHub ou avise o administrador."),
        GitPullRequestErrorCodes.HeadMissing => new(409, rejected.Code, $"A branch {branch} não existe mais no GitHub. Faça o push de novo."),
        GitPullRequestErrorCodes.NothingToCompare => new(409, rejected.Code, $"Não há diferença entre {branch} e {baseBranch}: nada para abrir no PR."),
        GitPullRequestErrorCodes.DraftUnsupported => new(409, rejected.Code,
            "Este repositório não aceita PR em rascunho no plano atual. Desmarque \"abrir como rascunho\"."),
        GitPullRequestErrorCodes.RateLimited => new(429, rejected.Code, $"O GitHub limitou as chamadas. Tente de novo depois de {RetryTime(rejected.RetryAtUtc)}."),
        GitPullRequestErrorCodes.Timeout or GitPullRequestErrorCodes.Unavailable => new(503, rejected.Code, "O GitHub não respondeu. Tente de novo em instantes."),
        _ => new(422, GitPullRequestErrorCodes.Invalid, "O GitHub recusou o PR (título ou corpo inválido). Revise o texto."),
    };

    // Hora de Brasília (sem horário de verão desde 2019), para a pessoa saber quando tentar de novo.
    private string RetryTime(DateTimeOffset? retryAtUtc)
    {
        var at = (retryAtUtc ?? _clock.GetUtcNow().AddMinutes(1)).ToOffset(TimeSpan.FromHours(-3));
        return $"{at:HH:mm} (horário de Brasília)";
    }
}

public sealed record OpenMethodPullRequestCommand(Guid ProjectId, string MethodId, OpenMethodPullRequestRequest Data, string ActorId)
    : IRequest<OpenMethodPullRequestResult>;
public sealed class OpenMethodPullRequestHandler(GitPullRequestOpening opening) : IRequestHandler<OpenMethodPullRequestCommand, OpenMethodPullRequestResult>
{
    public Task<OpenMethodPullRequestResult> Handle(OpenMethodPullRequestCommand r, CancellationToken ct) => opening.OpenAsync(r.ProjectId, r.MethodId, r.Data, r.ActorId, ct);
}

public sealed class OpenMethodPullRequestCommandValidator : AbstractValidator<OpenMethodPullRequestCommand>
{
    public OpenMethodPullRequestCommandValidator()
    {
        RuleFor(x => x.Data).NotNull().WithMessage("Informe o vínculo de branch, o título e o corpo do PR.");
        RuleFor(x => x.Data.LinkId).NotEmpty().WithMessage("Informe o vínculo de branch da task.").When(x => x.Data is not null);
        // O limite de 256 vale para o título final, já com o prefixo "[T-XXXX] " que o Prisma acrescenta.
        RuleFor(x => x).Cascade(CascadeMode.Stop)
            .Must(x => !string.IsNullOrWhiteSpace(x.Data.Title)).WithMessage("Informe o título do PR.")
            .Must(x => MethodPullRequestText.EnsureTaskPrefix(x.MethodId ?? string.Empty, x.Data.Title).Length <= MethodPullRequestLimits.TitleMaxLength)
            .WithMessage($"O título do PR deve ter até {MethodPullRequestLimits.TitleMaxLength} caracteres, contando o prefixo [T-XXXX].")
            .OverridePropertyName("title")
            .When(x => x.Data is not null);
        RuleFor(x => x.Data.Body).Must(body => (body?.Length ?? 0) <= MethodPullRequestLimits.BodyMaxLength)
            .WithMessage("O corpo do PR deve ter até 60.000 caracteres.").When(x => x.Data is not null);
    }
}
