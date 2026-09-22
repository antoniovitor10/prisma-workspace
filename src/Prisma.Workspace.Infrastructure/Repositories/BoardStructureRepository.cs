using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Infrastructure.Persistence;

namespace Prisma.Workspace.Infrastructure.Repositories;

/// <summary>Serializa leitura, validação, transferência e retirada de estrutura na mesma transação.</summary>
public sealed class BoardStructureRepository(AppDbContext context, IUserDirectory users) : IBoardStructureRepository
{
    private async Task AtomicAsync(Func<Task> action, CancellationToken ct)
    {
        await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await action();
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }

    public Task UpdateStageAsync(Guid stageId, string name, StageCategory? category, string? color, bool confirmCategoryChange, string actorId, CancellationToken ct) => AtomicAsync(async () =>
    {
        DomainException.Garantir(!string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 200, "Informe um nome de coluna de até 200 caracteres.");
        DomainException.Garantir(!category.HasValue || Enum.IsDefined(category.Value), "Classificação de coluna inválida.");
        var stage = await context.Stages.Include(x => x.WorkflowStatus).SingleAsync(x => x.Id == stageId && x.BoardId != null, ct);
        var oldCategory = stage.Category;
        var targetCategory = category ?? oldCategory;
        var items = await context.WorkItems.IgnoreQueryFilters().Include(x => x.Board).Include(x => x.StageHistories)
            .Where(x => x.StageId == stage.Id && x.BoardId == stage.BoardId).ToListAsync(ct);
        DomainException.Garantir(targetCategory == oldCategory || items.Count == 0 || confirmCategoryChange,
            $"A alteração afeta {items.Count} tarefa(s). Confirme a mudança de classificação.");
        stage.Name = name.Trim();
        stage.Category = targetCategory;
        var status = WorkflowStatus.Create(stage.ProjectId, stage.Name, color ?? stage.WorkflowStatus?.Color ?? "#64748B",
            stage.Position, targetCategory, false, targetCategory == StageCategory.Done);
        context.WorkflowStatuses.Add(status);
        stage.WorkflowStatus = status;
        stage.WorkflowStatusId = status.Id;
        if (targetCategory != oldCategory) await MoveAsync(items, stage, actorId, ct);
    }, ct);

    public Task ReorderAsync(Guid boardId, IReadOnlyList<Guid> stageIds, CancellationToken ct) => AtomicAsync(async () =>
    {
        var stages = await context.Stages.Where(x => x.BoardId == boardId).ToListAsync(ct);
        DomainException.Garantir(stageIds.Count > 0 && stageIds.Distinct().Count() == stageIds.Count
            && stages.Select(x => x.Id).ToHashSet().SetEquals(stageIds),
            "Informe todas as colunas do quadro, sem duplicação ou colunas de outro quadro.");
        for (var index = 0; index < stageIds.Count; index++)
            stages.Single(x => x.Id == stageIds[index]).Position = (index + 1) * 100;
    }, ct);

    public Task DeleteStageAsync(Guid stageId, Guid? destinationStageId, string actorId, CancellationToken ct) => AtomicAsync(async () =>
    {
        var source = await context.Stages.SingleAsync(x => x.Id == stageId && x.BoardId != null, ct);
        DomainException.Garantir(destinationStageId != stageId, "A coluna de destino deve ser diferente.");
        var items = await context.WorkItems.IgnoreQueryFilters().Include(x => x.Board).Include(x => x.StageHistories)
            .Where(x => x.StageId == stageId && x.BoardId == source.BoardId).ToListAsync(ct);
        Stage? target = null;
        if (destinationStageId.HasValue)
        {
            target = await context.Stages.SingleOrDefaultAsync(x => x.Id == destinationStageId && x.BoardId == source.BoardId, ct);
            DomainException.Garantir(target != null, "A coluna de destino deve pertencer ao mesmo quadro.");
        }
        DomainException.Garantir(items.Count == 0 || target != null, "A coluna possui tarefas. Escolha a coluna de destino.");
        if (target != null) await MoveAsync(items, target, actorId, ct);
        // Retenção lógica: IDs referenciados por histórico nunca são removidos.
        source.BoardId = null;
        source.Board = null;
    }, ct);

    public Task DeleteBoardAsync(Guid boardId, Guid? destinationBoardId, Guid? destinationStageId, string actorId, CancellationToken ct) => AtomicAsync(async () =>
    {
        var source = await context.Boards.SingleAsync(x => x.Id == boardId, ct);
        DomainException.Garantir(destinationBoardId != boardId, "O quadro de destino deve ser diferente.");
        var items = await context.WorkItems.IgnoreQueryFilters().Include(x => x.Board).Include(x => x.StageHistories)
            .Where(x => x.BoardId == boardId).ToListAsync(ct);
        Stage? target = null;
        if (destinationBoardId.HasValue || destinationStageId.HasValue)
        {
            target = await context.Stages.Include(x => x.Board).SingleOrDefaultAsync(
                x => x.Id == destinationStageId && x.BoardId == destinationBoardId && x.BoardId != null, ct);
            DomainException.Garantir(target?.ProjectId == source.ProjectId, "Escolha quadro e coluna de destino do mesmo projeto.");
        }
        DomainException.Garantir(items.Count == 0 || target != null, "O quadro possui tarefas. Escolha quadro e coluna de destino.");
        if (target != null) await MoveAsync(items, target, actorId, ct);
        var stages = await context.Stages.Where(x => x.BoardId == boardId).ToListAsync(ct);
        foreach (var stage in stages) { stage.BoardId = null; stage.Board = null; }
        var project = await context.Projects.SingleAsync(x => x.Id == source.ProjectId, ct);
        if (project.DefaultBoardId == boardId) project.DefaultBoardId = destinationBoardId
            ?? await context.Boards.Where(x => x.ProjectId == source.ProjectId && x.Id != boardId)
                .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        // Persiste a realocação antes do DELETE; ambos continuam na transação.
        await context.SaveChangesAsync(ct);
        context.Boards.Remove(source);
    }, ct);

    public Task TransferAsync(Guid workItemId, Guid destinationBoardId, Guid destinationStageId, string actorId, CancellationToken ct) => AtomicAsync(async () =>
    {
        var item = await context.WorkItems.Include(x => x.Board).Include(x => x.StageHistories)
            .SingleAsync(x => x.Id == workItemId, ct);
        var target = await context.Stages.Include(x => x.Board).SingleOrDefaultAsync(
            x => x.Id == destinationStageId && x.BoardId == destinationBoardId, ct);
        DomainException.Garantir(target != null && target.ProjectId == item.Board.ProjectId,
            "Escolha uma coluna do quadro de destino, no mesmo projeto.");
        var tree = new List<WorkItem> { item };
        var visited = new HashSet<Guid> { item.Id };
        var frontier = new List<Guid> { item.Id };
        while (frontier.Count > 0)
        {
            // A transferência não pode abandonar descendentes retidos/arquivados.
            // IgnoreQueryFilters exige revalidar explicitamente o limite do projeto e tenant.
            var children = await context.WorkItems.IgnoreQueryFilters()
                .Include(x => x.Board).Include(x => x.StageHistories)
                .Where(x => x.ParentId.HasValue && frontier.Contains(x.ParentId.Value)).ToListAsync(ct);
            DomainException.Garantir(children.All(x => x.Board.ProjectId == item.Board.ProjectId
                && x.Board.OrganizationId == item.Board.OrganizationId),
                "A árvore contém descendentes de outro projeto ou organização. Corrija o vínculo antes de transferir.");
            var next = children.Where(x => visited.Add(x.Id)).ToList();
            tree.AddRange(next);
            frontier = next.Select(x => x.Id).ToList();
        }
        // Selecionar a árvore para transferência não constitui consentimento D62
        // para concluir descendentes abertos. O contrato atual não recebe esse consentimento.
        DomainException.Garantir(target!.Category != StageCategory.Done
            || tree.Where(x => x.Id != item.Id).All(x => x.CompletedAt != null),
            "Conclua as subtarefas abertas antes de transferir a árvore para uma coluna concluída.");
        await MoveAsync(tree, target, actorId, ct);
    }, ct);

    private async Task MoveAsync(IReadOnlyList<WorkItem> items, Stage destination, string actorId, CancellationToken ct)
    {
        var ids = items.Select(x => x.Id).ToHashSet();
        if (destination.Category == StageCategory.Done)
        {
            var pendingParents = ids.ToList();
            while (pendingParents.Count > 0)
            {
                var children = await context.WorkItems.IgnoreQueryFilters()
                    .Where(x => x.ParentId.HasValue && pendingParents.Contains(x.ParentId.Value))
                    .Select(x => new { x.Id, x.CompletedAt }).ToListAsync(ct);
                DomainException.Garantir(children.All(x => x.CompletedAt != null || ids.Contains(x.Id)),
                    "Conclua as subtarefas abertas antes de transferir para uma coluna concluída.");
                pendingParents = children.Select(x => x.Id).Except(ids).ToList();
                foreach (var child in children) ids.Add(child.Id);
            }
        }
        var names = await users.GetDisplayNamesAsync([actorId], ct);
        var actorName = names.GetValueOrDefault(actorId);
        var now = DateTimeOffset.UtcNow;
        foreach (var item in items)
        {
            var previousStageId = item.StageId;
            var previousBoardId = item.BoardId;
            foreach (var entry in item.StageHistories.Where(x => x.LeftAt == null)) entry.LeftAt = now;
            context.StageHistories.Add(new StageHistory { Id = Guid.NewGuid(), WorkItemId = item.Id,
                StageId = destination.Id, EnteredAt = now, ActorId = actorId, ActorName = actorName });
            item.BoardId = destination.BoardId!.Value;
            item.Board = await context.Boards.SingleAsync(x => x.Id == destination.BoardId, ct);
            item.StageId = destination.Id;
            item.Stage = destination;
            item.WorkflowStatusId = destination.WorkflowStatusId;
            item.CompletedAt = destination.Category == StageCategory.Done ? item.CompletedAt ?? now : null;
            item.UpdatedAt = now;
            context.TaskEvents.Add(new TaskEvent { Id = Guid.NewGuid(), WorkItemId = item.Id, ActorId = actorId,
                Kind = "stage_changed", CreatedAt = now, Payload = JsonSerializer.Serialize(new {
                    fromBoardId = previousBoardId, toBoardId = destination.BoardId, fromStageId = previousStageId,
                    toStageId = destination.Id, toStageName = destination.Name, actorId, actorName }) });
        }
    }
}
