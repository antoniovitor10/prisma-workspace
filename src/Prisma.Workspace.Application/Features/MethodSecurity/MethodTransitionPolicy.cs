using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Features.MethodSecurity;

public sealed class MethodTransitionPolicy(
    IMethodAccessContext context, IMethodGitStore store, IPermissionService permissions) : IMethodTransitionPolicy
{
    public async Task EnsureAllowedAsync(MethodMutation mutation, CancellationToken cancellationToken = default)
    {
        var decision = await EvaluateAsync(mutation, cancellationToken);
        if (!decision.Allowed)
            throw new MethodOperationException(decision.Code == "stale_task" ? 409 : 403, decision.Code, decision.Message);
    }

    public async Task<MethodTransitionDecision> EvaluateAsync(MethodMutation mutation, CancellationToken cancellationToken = default)
    {
        var actor = context.Current;
        // Permissões/validações existentes continuam nos handlers. A matriz não restringe a sessão humana.
        if (actor?.IsInteractiveHuman == true) return MethodTransitionDecision.Permit();
        var legacyActor = actor is null || actor.AuthenticationKind == MethodAuthenticationKind.Unknown;
        if (legacyActor)
        {
            var legacyProject = await store.GetProjectAsync(mutation.ProjectId, cancellationToken);
            if (legacyProject is null || !legacyProject.Enabled) return MethodTransitionDecision.Permit();
            if (actor?.PrincipalId is not null || actor?.TokenId is not null)
                return Deny("untrusted_actor", "A operação exige identidade autenticada confiável.");
            // Portal, seed e automações legadas conservam suas validações, mas não fabricam execução autorizada.
            var entersDevelopment = StatusOf(legacyProject, mutation.ToStageId) == MethodStatusCodes.InDevelopment
                && (mutation.IsCreation || mutation.FromStageId != mutation.ToStageId
                    || mutation.DestinationBoardId is { } board && board != mutation.BoardId);
            return entersDevelopment
                ? Deny("interactive_authorization_required", "Conceder execução no método exige sessão humana da UI; origem legada não autoriza.")
                : MethodTransitionDecision.Permit();
        }
        if (actor is null || actor.AuthenticationKind is not (MethodAuthenticationKind.MethodToken or MethodAuthenticationKind.IntegrationWorker)
            || !Enum.IsDefined(actor.Role))
            return Deny("untrusted_actor", "A operação exige identidade autenticada confiável.");
        if (actor.ProjectId != mutation.ProjectId || actor.PrincipalId is null
            || (mutation.DestinationProjectId is not null && mutation.DestinationProjectId != actor.ProjectId))
            return Deny("method_scope", "A credencial do método não permite operar fora do seu projeto.");

        var project = await store.GetProjectAsync(mutation.ProjectId, cancellationToken);
        if (project is null || !project.Enabled || project.OrganizationId != actor.OrganizationId)
            return Deny("method_disabled", "O método está desabilitado ou sua configuração não está disponível.");
        if (actor.Role != MethodRole.Human && (project.BoardId != mutation.BoardId
            || (mutation.DestinationBoardId is not null && mutation.DestinationBoardId != project.BoardId)))
            return Deny("method_board", "O principal do método não pode transferir a tarefa para outro quadro.");

        var required = mutation.IsCreation ? PlatformPermission.Create : PlatformPermission.ChangeStatus;
        if (!actor.PermissionCeiling.Contains(required)
            || !await permissions.HasAsync(actor.ActorId, required,
                mutation.IsCreation || mutation.WorkItemId is null ? PermissionScope.Project : PermissionScope.WorkItem,
                mutation.IsCreation ? mutation.ProjectId : mutation.WorkItemId ?? mutation.ProjectId, cancellationToken))
            return Deny("method_permission", "A credencial não possui permissão efetiva para esta operação.");
        if (actor.Role != MethodRole.Human && mutation.ChangesMethodIdentity && !mutation.IsCreation)
            return Deny("method_identity", "O principal não pode remover ou alterar a identidade da task para contornar o método.");

        var to = StatusOf(project, mutation.ToStageId);
        var from = StatusOf(project, mutation.FromStageId);
        var task = !mutation.IsCreation && mutation.WorkItemId is { } id ? await store.GetTaskAsync(id, cancellationToken) : null;
        if (task is not null && (task.ProjectId != mutation.ProjectId || task.BoardId != mutation.BoardId
            || task.OrganizationId != project.OrganizationId || task.StageId != mutation.FromStageId))
            return Deny("stale_task", "A tarefa mudou durante a operação. Atualize os dados antes de tentar novamente.");

        // Avalia o destino real antes de permitir operações humanas fora do quadro do método.
        // Transferir/criar de outro quadro para a coluna de desenvolvimento também seria início sem UI.
        if (actor.Role == MethodRole.Human)
        {
            if (to != MethodStatusCodes.InDevelopment) return MethodTransitionDecision.Permit();
            var sameMethodBoard = mutation.BoardId == project.BoardId
                && (mutation.DestinationBoardId is null || mutation.DestinationBoardId == project.BoardId);
            if (!mutation.IsCreation && sameMethodBoard && (from == to
                || from == MethodStatusCodes.InReview && task?.ActiveAuthorizationEventId is not null))
                return MethodTransitionDecision.Permit();
            return Deny("interactive_authorization_required", "Conceder ou renovar execução exige sessão humana da UI; token humano não autoriza, inclusive por transferência.");
        }
        if (to is null)
            return Deny("method_mapping", "A coluna de destino não possui mapeamento válido do método.");
        if (mutation.IsCreation)
        {
            if (actor.Role == MethodRole.System || actor.AuthenticationKind == MethodAuthenticationKind.IntegrationWorker)
                return Deny("system_cannot_create", "O sistema de integração não cria tasks.");
            return to is MethodStatusCodes.Backlog or MethodStatusCodes.Completed
                ? MethodTransitionDecision.Permit()
                : Deny("agent_creation", "O agente só cria tasks em backlog ou concluida, sem autorização implícita.");
        }
        if (from is null) return Deny("method_mapping", "A coluna de origem não possui mapeamento válido do método.");
        if (task is null || !task.IsTopLevel || task.IsArchived)
            return Deny("method_task", "A tarefa não possui identidade válida de topo no método.");

        if (actor.Role == MethodRole.System)
        {
            if (mutation.GitFact is { } fact)
            {
                var repository = await store.GetRepositoryAsync(fact.IntegrationId, cancellationToken);
                if (repository is null || !repository.Enabled || repository.ProjectId != project.ProjectId
                    || repository.OrganizationId != project.OrganizationId || repository.SystemPrincipalId != actor.PrincipalId)
                    return Deny("integration_scope", "O fato não pertence à integração e ao principal autenticados.");
            }
            return EvaluateSystem(actor, mutation, task, from, to);
        }
        if (actor.AuthenticationKind != MethodAuthenticationKind.MethodToken)
            return Deny("untrusted_actor", "O principal agente precisa de credencial válida do método.");
        if (from == to) return MethodTransitionDecision.Permit();
        if (to == MethodStatusCodes.Blocked && from != MethodStatusCodes.Completed)
            return HasReason(mutation);
        if (from is MethodStatusCodes.Backlog or MethodStatusCodes.Ready && to == MethodStatusCodes.InDevelopment)
            return Deny("interactive_authorization_required", "O agente nunca autoriza backlog/pronta para em-desenvolvimento. Solicite autorização humana na UI.");
        if (from == MethodStatusCodes.InDevelopment && to == MethodStatusCodes.InReview
            && project.AutonomyLevel == 0 && task.ActiveAuthorizationEventId is not null)
            return HasReason(mutation);
        if (from == MethodStatusCodes.Blocked && to == MethodStatusCodes.InDevelopment)
        {
            if (task.CycleId != Guid.Empty && task.ActiveAuthorizationEventId is not null
                && task.BlockingEventId is not null && task.BlockingPrincipalId == actor.PrincipalId
                && task.BlockingOriginStatusCode == MethodStatusCodes.InDevelopment && task.ResumeApprovalEventId is not null)
                return HasReason(mutation);
            return Deny("resume_approval_required", "Retomar exige bloqueio próprio de execução autorizada e liberação humana para o bloqueio e ciclo atuais.");
        }
        return Deny("method_transition", "Transição não permitida ao agente; siga as regras do projeto e aguarde a evidência Git quando aplicável.");
    }

    private static MethodTransitionDecision EvaluateSystem(MethodActorIdentity actor, MethodMutation mutation,
        MethodTaskSnapshot task, string from, string to)
    {
        var fact = mutation.GitFact;
        if (actor.AuthenticationKind != MethodAuthenticationKind.IntegrationWorker || fact is null)
            return Deny("verified_fact_required", "O sistema só movimenta por fato Git verificado no worker interno.");
        var lastIntervention = task.LastManualChangeAtUtc is { } manual
            && (task.LastInteractiveChangeAtUtc is null || manual > task.LastInteractiveChangeAtUtc)
            ? manual : task.LastInteractiveChangeAtUtc;
        if (fact.ProjectId != task.ProjectId || fact.WorkItemId != task.WorkItemId || fact.CycleId != task.CycleId
            || task.ActiveAuthorizationEventId is null || task.AuthorizedAtUtc is null
            || fact.OccurredAtUtc < task.AuthorizedAtUtc || string.IsNullOrWhiteSpace(fact.ProviderRevision)
            || fact.SemanticKey.Length != 32
            || (lastIntervention is { } changed && fact.OccurredAtUtc <= changed))
            return Deny("stale_git_fact", "O fato não comprova autorização do ciclo vigente ou é anterior à intervenção humana.");
        var allowed = from == MethodStatusCodes.InDevelopment && to == MethodStatusCodes.InReview
            && fact.FactType is MethodGitFactCodes.PullRequestOpened or MethodGitFactCodes.PullRequestReadyForReview or MethodGitFactCodes.PullRequestReopened
            && !fact.HasEffectiveChangesRequested
            || from == MethodStatusCodes.InReview && to == MethodStatusCodes.InDevelopment
            && fact.FactType is MethodGitFactCodes.PullRequestClosedWithoutMerge or MethodGitFactCodes.ChangesRequested
            || from == MethodStatusCodes.InReview && to == MethodStatusCodes.InTest
            && fact.FactType == MethodGitFactCodes.PullRequestMerged && fact.OpenPullRequestsReadComplete
            && !fact.HasOtherOpenPullRequests && !fact.HasEffectiveChangesRequested && !string.IsNullOrWhiteSpace(fact.MergeSha);
        return allowed ? HasReason(mutation)
            : Deny("git_transition", "O fato Git não permite esta transição ou a leitura dos PRs está incompleta.");
    }

    private static string? StatusOf(MethodProjectSnapshot project, Guid? stageId)
        => stageId is null ? null : project.StageMappings.FirstOrDefault(x => x.Value == stageId && MethodStatusCodes.IsValid(x.Key)).Key;

    private static MethodTransitionDecision HasReason(MethodMutation mutation)
        => string.IsNullOrWhiteSpace(mutation.Reason) || mutation.Reason.Length > 1000
            ? Deny("method_reason", "Informe um motivo de até 1000 caracteres para a movimentação.")
            : MethodTransitionDecision.Permit();

    private static MethodTransitionDecision Deny(string code, string message) => MethodTransitionDecision.Deny(code, message);
}
