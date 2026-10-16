using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.Workflow;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;

namespace Prisma.Workspace.Infrastructure.Services.MethodSecurity;

public sealed class MethodMoveService(AppDbContext db, IMethodAccessContext access, IMethodGitStore store,
    IWorkflowRepository workflow, IPermissionService permissions, IBoardRealtimeNotifier realtime,
    ILogger<MethodMoveService> logger, TimeProvider? clock = null) : IMethodMoveService
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<MethodMoveResult> MoveAsync(MethodMoveRequest request, CancellationToken cancellationToken = default)
    {
        if (access.Current?.IsInteractiveHuman == true && string.IsNullOrWhiteSpace(request.Reason))
            request = request with { Reason = "Movimentação pela pessoa na UI." };
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000
            || !double.IsFinite(request.Position) || request.Position < 0)
            throw new MethodOperationException(400, "invalid_move", "Informe motivo de até 1.000 caracteres e posição válida.");
        var actor = access.Current
            ?? throw new MethodOperationException(401, "method_identity_required", "Identidade autenticada necessária.");
        if (request.GitFact is not null && actor.AuthenticationKind != MethodAuthenticationKind.IntegrationWorker)
            throw new MethodOperationException(403, "verified_fact_required", "Somente o worker interno aplica fatos Git verificados.");
        if (actor.AuthenticationKind == MethodAuthenticationKind.IntegrationWorker && request.GitFact is null)
            throw new MethodOperationException(403, "verified_fact_required", "O worker deve apresentar o fato Git verificado.");
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("A unidade de movimento deve possuir sua transação e publicar somente depois do commit.");

        Guid boardId = Guid.Empty;
        var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            IDbContextTransaction? transaction = null;
            try
            {
                if (db.Database.IsRelational())
                    transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                // O processador pode ter consultado o estado antes de aguardar o lock. Releia tudo dentro da unidade.
                db.ChangeTracker.Clear();
                var item = await db.WorkItems.Include(x => x.Board).SingleOrDefaultAsync(x => x.Id == request.WorkItemId, cancellationToken)
                    ?? throw new MethodOperationException(404, "task_not_found", "Tarefa não encontrada no escopo autenticado.");
                boardId = item.BoardId;
                await permissions.EnsureAsync(actor.ActorId, PlatformPermission.ChangeStatus, PermissionScope.WorkItem, item.Id, cancellationToken);
                var stage = await db.Stages.SingleOrDefaultAsync(x => x.Id == request.DestinationStageId
                    && x.BoardId == item.BoardId && x.ProjectId == item.Board.ProjectId, cancellationToken)
                    ?? throw new MethodOperationException(400, "invalid_stage", "Escolha uma coluna do quadro atual da tarefa.");
                if (item.IsArchived)
                    throw new MethodOperationException(409, "task_archived", "Reative a tarefa antes de movimentá-la.");
                var state = await db.Set<MethodTaskState>().SingleOrDefaultAsync(x => x.WorkItemId == item.Id
                    && x.ProjectId == item.Board.ProjectId, cancellationToken);
                if (request.ExpectedRowVersion is { } expected
                    && (state is null || !state.RowVersion.SequenceEqual(expected)))
                    throw new MethodOperationException(409, "git_concurrency", "A tarefa mudou após a leitura; reavalie o evento atual.");
                if (state is not null && request.ExpectedRowVersion is { } original)
                    db.Entry(state).Property(x => x.RowVersion).OriginalValue = original;
                if (request.GitFact is { } fact)
                {
                    if (fact.ProjectId != item.Board.ProjectId || fact.WorkItemId != item.Id || state?.CycleId != fact.CycleId
                        || actor.ProjectId != fact.ProjectId)
                        throw new MethodOperationException(409, "stale_git_fact", "O fato não pertence ao ciclo atual da tarefa.");
                    var delivery = await db.Set<GitWebhookDelivery>().SingleOrDefaultAsync(x => x.Id == request.DeliveryId
                        && x.ProjectId == fact.ProjectId && x.IntegrationId == fact.IntegrationId, cancellationToken);
                    if (delivery is null || request.ExpectedDeliveryRowVersion is not { } deliveryVersion
                        || !delivery.RowVersion.SequenceEqual(deliveryVersion) || delivery.Status != GitDeliveryStates.Processing
                        || delivery.LeaseUntilUtc is null || delivery.LeaseUntilUtc <= _clock.GetUtcNow())
                        throw new MethodOperationException(409, "lease_lost", "A entrega não possui lease vigente; nenhum efeito será confirmado.");
                    db.Entry(delivery).Property(x => x.RowVersion).OriginalValue = deliveryVersion;
                    if (await store.HasAppliedFactAsync(fact.ProjectId, fact.WorkItemId, fact.SemanticKey, cancellationToken))
                    {
                        delivery.Status = GitDeliveryStates.Processed;
                        delivery.ProcessedAtUtc = _clock.GetUtcNow(); delivery.LeaseUntilUtc = null;
                        delivery.NextAttemptAtUtc = null; delivery.LastErrorCode = null;
                        await db.SaveChangesAsync(cancellationToken);
                        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                        return new MethodMoveResult(item.Id, item.StageId, stage.Id, false, true, null, null, "already_applied");
                    }
                }
                await WorkflowMoveGuard.EnsureAllowedAsync(item, stage, workflow, cancellationToken);
                var previousStage = item.StageId;
                item.StageId = stage.Id; item.WorkflowStatusId = stage.WorkflowStatusId;
                item.Position = request.Position; item.UpdatedAt = _clock.GetUtcNow();
                item.CompletedAt = stage.Category == StageCategory.Done ? item.CompletedAt ?? item.UpdatedAt : null;
                if (stage.Category == StageCategory.InProgress && item.StartDate is null)
                    item.StartDate = DateOnly.FromDateTime(item.UpdatedAt.UtcDateTime);
                db.MethodExecution = new MethodMoveExecution(request);
                await db.SaveChangesAsync(cancellationToken);
                var movement = db.MethodExecution.Result
                    ?? new MethodMoveResult(item.Id, previousStage, stage.Id, previousStage != stage.Id, false, null, null, "moved");
                if (request.GitFact is not null && !movement.Applied)
                    throw new MethodOperationException(409, "git_transition", "O fato não produziu transição válida no ciclo atual.");
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return movement;
            }
            catch
            {
                if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
                // O worker confirma falhas com o mesmo escopo. Nada da tentativa pode vazar nessa confirmação.
                db.ChangeTracker.Clear();
                throw;
            }
            finally
            {
                db.MethodExecution = null;
                if (transaction is not null) await transaction.DisposeAsync();
            }
        });
        try
        {
            if (!result.AlreadyApplied)
                await realtime.BoardChangedAsync(boardId, result.Applied ? "workItemMoved" : "workItemReordered", request.WorkItemId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning("Movimento confirmado; atualização em tempo real pendente para o quadro {BoardId}.", boardId);
        }
        return result;
    }
}
