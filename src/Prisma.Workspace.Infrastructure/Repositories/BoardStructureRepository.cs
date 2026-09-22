using System.Data;
using System.Security.Cryptography;
using System.Text;
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

    public Task UpdateStageAsync(Guid stageId, string name, StageCategory? category, string? color, bool confirmCategoryChange, string actorId, CancellationToken ct, bool confirmDescendants = false, string? impactToken = null) => AtomicAsync(async () =>
    {
        DomainException.Garantir(!string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 200, "Informe um nome de coluna de até 200 caracteres.");
        DomainException.Garantir(!category.HasValue || Enum.IsDefined(category.Value), "Classificação de coluna inválida.");
        var stage = await context.Stages.Include(x => x.WorkflowStatus).SingleAsync(x => x.Id == stageId && x.BoardId != null, ct);
        var oldCategory = stage.Category;
        var targetCategory = category ?? oldCategory;
        var (items, descendants, impact) = await LoadImpactAsync(stage, targetCategory, ct);
        var reclassifying = targetCategory != oldCategory;
        DomainException.Garantir(!reclassifying || items.Count == 0 || confirmCategoryChange,
            $"A alteração afeta {items.Count} tarefa(s). Confirme a mudança de classificação.");
        DomainException.Garantir((!reclassifying || items.Count == 0) && impactToken == null || impactToken == impact.SnapshotToken,
            "O impacto da classificação mudou. Recarregue as contagens e confirme novamente.");
        DomainException.Garantir(!reclassifying || impact.OpenDescendants == 0 || confirmDescendants,
            $"Confirme separadamente a conclusão de {impact.OpenDescendants} descendente(s) aberto(s).");
        stage.Name = name.Trim();
        stage.Category = targetCategory;
        var status = WorkflowStatus.Create(stage.ProjectId, stage.Name, color ?? stage.WorkflowStatus?.Color ?? "#64748B",
            stage.Position, targetCategory, false, targetCategory == StageCategory.Done);
        context.WorkflowStatuses.Add(status);
        stage.WorkflowStatus = status;
        stage.WorkflowStatusId = status.Id;
        if (reclassifying)
        {
            var toDone = targetCategory == StageCategory.Done;
            var affected = items.Where(x => (x.CompletedAt != null) != toDone)
                .Concat(toDone ? descendants.Where(x => x.CompletedAt == null) : [])
                .DistinctBy(x => x.Id).ToList();
            var names = await users.GetDisplayNamesAsync([actorId], ct);
            var actorName = names.GetValueOrDefault(actorId);
            var now = DateTimeOffset.UtcNow;
            foreach (var item in affected)
            {
                // D62: descendentes conservam quadro e coluna; somente seu estado muda.
                item.CompletedAt = toDone ? now : null;
                item.UpdatedAt = now;
                if (item.StageId == stage.Id) item.WorkflowStatusId = stage.WorkflowStatusId;
                foreach (var entry in item.StageHistories.Where(x => x.LeftAt == null)) entry.LeftAt = now;
                context.StageHistories.Add(new StageHistory { Id=Guid.NewGuid(), WorkItemId=item.Id,
                    StageId=item.StageId!.Value, EnteredAt=now, ActorId=actorId, ActorName=actorName });
                context.TaskEvents.Add(new TaskEvent { Id=Guid.NewGuid(), WorkItemId=item.Id, ActorId=actorId,
                    Kind="stage_reclassified", CreatedAt=now, Payload=JsonSerializer.Serialize(new {
                        reason="column_reclassification", reclassifiedStageId=stage.Id, stageName=stage.Name,
                        completed=toDone, recursive=item.StageId != stage.Id, actorId, actorName }) });
            }
        }
    }, ct);

    public async Task<BoardStageImpactDto> GetStageImpactAsync(Guid stageId, StageCategory category, CancellationToken ct)
    {
        BoardStageImpactDto? result = null;
        await AtomicAsync(async () =>
        {
            var stage = await context.Stages.SingleAsync(x => x.Id == stageId && x.BoardId != null, ct);
            (_, _, result) = await LoadImpactAsync(stage, category, ct);
        }, ct);
        return result!;
    }

    private async Task<(List<WorkItem> Items, List<WorkItem> Descendants, BoardStageImpactDto Impact)> LoadImpactAsync(
        Stage stage, StageCategory target, CancellationToken ct)
    {
        DomainException.Garantir(Enum.IsDefined(target), "Classificação de coluna inválida.");
        var items = await context.WorkItems.IgnoreQueryFilters().Include(x => x.Board).Include(x => x.StageHistories)
            .Where(x => x.StageId == stage.Id && x.BoardId == stage.BoardId).ToListAsync(ct);
        var toDone = target == StageCategory.Done;
        var changed = items.Where(x => (x.CompletedAt != null) != toDone).ToList();
        var descendants = new List<WorkItem>();
        var itemIds = items.Select(x => x.Id).ToHashSet();
        var visited = changed.Select(x => x.Id).ToHashSet();
        var organizationId = await context.Boards.Where(x => x.Id == stage.BoardId).Select(x => x.OrganizationId).SingleAsync(ct);
        var frontier = toDone ? changed.Select(x => x.Id).ToList() : [];
        while (frontier.Count > 0)
        {
            var children = await context.WorkItems.IgnoreQueryFilters().Include(x => x.Board).Include(x => x.StageHistories)
                .Where(x => x.ParentId.HasValue && frontier.Contains(x.ParentId.Value)).ToListAsync(ct);
            DomainException.Garantir(children.All(x => x.Board.ProjectId == stage.ProjectId
                && x.Board.OrganizationId == organizationId),
                "A descendência contém vínculo fora do projeto ou organização.");
            var next = children.Where(x => visited.Add(x.Id)).ToList();
            descendants.AddRange(next.Where(x => !itemIds.Contains(x.Id)));
            frontier = next.Select(x => x.Id).ToList();
        }
        var snapshot = JsonSerializer.Serialize(new { stage.Id, stage.Name, stage.Category, target,
            stage.BoardId, stage.Position, stage.WorkflowStatusId,
            Items = items.Concat(descendants).OrderBy(x => x.Id).Select(x => new {
                x.Id, x.ParentId, x.StageId, x.BoardId, x.CompletedAt, x.IsArchived, x.RowVersion }) });
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
        return (items, descendants, new BoardStageImpactDto(stage.Id, stage.Name, items.Count, changed.Count,
            descendants.Count(x => x.CompletedAt == null), token));
    }

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
        var rules = await context.AutomationRules.Where(x => x.BoardId == source.BoardId).ToListAsync(ct);
        DomainException.Garantir(!rules.Any(x => x.TriggerStageId == stageId
            || x.ActionType == AutomationActionType.MoveToStage && Guid.TryParse(x.ActionValue, out var targetId) && targetId == stageId),
            "A coluna é usada como gatilho ou destino de automação. Reconfigure ou exclua essas regras antes de excluir a coluna.");
        var forms = await context.ExternalForms.IgnoreQueryFilters()
            .Where(x => x.ExternalPortal.BoardId == source.BoardId)
            .Select(x => new { x.InitialStageId, x.AssignmentRulesJson }).ToListAsync(ct);
        foreach (var form in forms)
        {
            const string referenced = "A coluna é usada como fila inicial ou destino de regra de formulário externo. Reconfigure o formulário antes de excluir a coluna.";
            const string invalid = "Há regras JSON inválidas ou ambíguas em formulário deste quadro. Reconfigure o formulário antes de excluir a coluna.";
            DomainException.Garantir(form.InitialStageId != stageId, referenced);
            DomainException.Garantir(!string.IsNullOrWhiteSpace(form.AssignmentRulesJson), invalid);
            try
            {
                using var json = JsonDocument.Parse(form.AssignmentRulesJson);
                DomainException.Garantir(json.RootElement.ValueKind == JsonValueKind.Array, invalid);
                foreach (var rule in json.RootElement.EnumerateArray())
                {
                    DomainException.Garantir(rule.ValueKind == JsonValueKind.Object, invalid);
                    var stageProperties = rule.EnumerateObject()
                        .Where(x => string.Equals(x.Name, "StageId", StringComparison.OrdinalIgnoreCase)).ToList();
                    DomainException.Garantir(stageProperties.Count <= 1, invalid);
                    foreach (var property in stageProperties)
                    {
                        if (property.Value.ValueKind == JsonValueKind.Null) continue;
                        DomainException.Garantir(property.Value.ValueKind == JsonValueKind.String, invalid);
                        DomainException.Garantir(Guid.TryParse(property.Value.GetString(), out var formStageId), invalid);
                        DomainException.Garantir(formStageId != stageId, referenced);
                    }
                }
            }
            catch (JsonException)
            {
                throw new DomainException(invalid);
            }
        }
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
        var hasPortal = await context.ExternalPortals.IgnoreQueryFilters().AnyAsync(x => x.BoardId == boardId, ct);
        var hasForm = await context.ExternalForms.IgnoreQueryFilters().AnyAsync(x => x.ExternalPortal.BoardId == boardId, ct);
        DomainException.Garantir(!hasPortal && !hasForm,
            "O quadro está vinculado a um portal ou formulário externo. Reconfigure esses vínculos antes de excluir o quadro.");
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
