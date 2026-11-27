using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Services;
using Prisma.Workspace.Infrastructure.Persistence;

namespace Prisma.Workspace.Infrastructure.Services.WorkItems;

/// <summary>Modo da rotina de correção da T-0019 (configuração <c>Maintenance:WorkItemKindRepair</c>).</summary>
public enum WorkItemKindRepairMode { Off, DryRun, Apply }

/// <summary>
/// Uma linha do relatório da correção. Só identificadores: sem título, descrição nem nome de pessoa
/// (princípio 2 e skill dados).
/// </summary>
public sealed record WorkItemKindRepairEntry(
    Guid WorkItemId, long Number, Guid OrganizationId, Guid ProjectId, WorkItemKind? ParentKind,
    WorkItemKind KindBefore, WorkItemKind KindAfter, Guid SourceEventId, bool Repaired, string? NotRepairedReason);

public sealed record WorkItemKindRepairResult(bool Applied, IReadOnlyList<WorkItemKindRepairEntry> Entries)
{
    public int RepairedCount => Entries.Count(x => x.Repaired);
}

/// <summary>
/// T-0019: devolve o tipo Tarefa aos itens que a edição pela gaveta transformou em Subtarefa.
/// Só toca o item cujo histórico tem o evento <c>updated</c> com Kind 5 → 6 (o bug), que continua Subtarefa,
/// cujo pai não é Tarefa e aceita Tarefa pela hierarquia, e que não teve outra troca de tipo depois.
/// Percorre todas as organizações (por isso <c>IgnoreQueryFilters</c>, justificado na T-0019) e é idempotente:
/// o evento <c>work_item_kind_repaired</c> marca o item corrigido. O modo ensaio só calcula a lista.
/// </summary>
public sealed class WorkItemKindRepairRoutine(AppDbContext db, TimeProvider? clock = null)
{
    public const string RepairedEventKind = "work_item_kind_repaired";
    public const string SystemActorId = "system";
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<WorkItemKindRepairResult> RunAsync(bool apply, CancellationToken ct = default)
    {
        // Pré-filtro no banco (texto); a decisão final lê o JSON do evento em memória.
        var events = await db.TaskEvents.IgnoreQueryFilters().AsNoTracking()
            .Where(x => (x.Kind == "updated" && x.Payload != null && x.Payload.Contains("\"Kind\":6"))
                || x.Kind == RepairedEventKind)
            .Select(x => new { x.Id, x.WorkItemId, x.Kind, x.Payload, x.CreatedAt })
            .ToListAsync(ct);
        var bugEvents = events.Where(x => x.Kind == "updated" && IsTaskToSubtask(x.Payload)).ToList();
        if (bugEvents.Count == 0) return new(apply, []);

        var itemIds = bugEvents.Select(x => x.WorkItemId).Distinct().ToArray();
        var repaired = events.Where(x => x.Kind == RepairedEventKind).Select(x => x.WorkItemId).ToHashSet();
        // Toda troca de tipo posterior ao evento do bug, para não desfazer uma escolha feita depois.
        var kindChanges = await db.TaskEvents.IgnoreQueryFilters().AsNoTracking()
            .Where(x => itemIds.Contains(x.WorkItemId) && x.Kind == "updated")
            .Select(x => new { x.Id, x.WorkItemId, x.Payload, x.CreatedAt })
            .ToListAsync(ct);
        var items = await db.WorkItems.IgnoreQueryFilters()
            .Include(x => x.Parent).Include(x => x.Board)
            .Where(x => itemIds.Contains(x.Id))
            .ToListAsync(ct);

        var entries = new List<WorkItemKindRepairEntry>();
        foreach (var item in items.OrderBy(x => x.Board.OrganizationId).ThenBy(x => x.Number))
        {
            if (repaired.Contains(item.Id)) continue; // já corrigido numa execução anterior
            var source = bugEvents.Where(x => x.WorkItemId == item.Id).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Last();
            var parentKind = item.Parent?.Kind;
            if (item.Kind != WorkItemKind.Subtask || item.ParentId is null || parentKind == WorkItemKind.Task)
            {
                // Subtarefa válida sob Tarefa nunca é tocada; o resto aparece na lista com o motivo.
                if (parentKind == WorkItemKind.Task && item.Kind == WorkItemKind.Subtask) continue;
                entries.Add(Entry(item, parentKind, source.Id, false,
                    item.Kind != WorkItemKind.Subtask ? "mudou de tipo depois do evento" : "sem pai"));
                continue;
            }
            var changedLater = kindChanges.Any(x => x.WorkItemId == item.Id && x.Id != source.Id
                && x.CreatedAt > source.CreatedAt && ChangesKind(x.Payload));
            if (changedLater)
            {
                entries.Add(Entry(item, parentKind, source.Id, false, "mudou de tipo depois do evento"));
                continue;
            }
            if (!WorkItemHierarchyRules.IsAllowed(parentKind!.Value, WorkItemKind.Task))
            {
                entries.Add(Entry(item, parentKind, source.Id, false, $"o pai ({parentKind}) não aceita Tarefa"));
                continue;
            }
            entries.Add(Entry(item, parentKind, source.Id, true, null));
            if (!apply) continue;
            item.Kind = WorkItemKind.Task;
            item.UpdatedAt = _clock.GetUtcNow();
            var repairEvent = TaskEvent.Registrar(item.Id, SystemActorId, RepairedEventKind, JsonSerializer.Serialize(new
            {
                actorName = "Sistema",
                previousKind = (int)WorkItemKind.Subtask,
                currentKind = (int)WorkItemKind.Task,
                reason = "T-0019",
                sourceEventId = source.Id,
            }));
            repairEvent.CreatedAt = _clock.GetUtcNow();
            db.TaskEvents.Add(repairEvent);
        }
        if (apply && entries.Any(x => x.Repaired)) await db.SaveChangesAsync(ct);
        return new(apply, entries);
    }

    private static WorkItemKindRepairEntry Entry(WorkItem item, WorkItemKind? parentKind, Guid sourceEventId, bool repaired, string? reason)
        => new(item.Id, item.Number, item.Board.OrganizationId, item.Board.ProjectId, parentKind, item.Kind,
            repaired ? WorkItemKind.Task : item.Kind, sourceEventId, repaired, reason);

    private static bool IsTaskToSubtask(string? payload)
        => TryKinds(payload, out var previous, out var current) && previous == 5 && current == 6;

    private static bool ChangesKind(string? payload)
        => TryKinds(payload, out var previous, out var current) && previous != current;

    private static bool TryKinds(string? payload, out int previous, out int current)
    {
        previous = current = -1;
        if (string.IsNullOrWhiteSpace(payload)) return false;
        try
        {
            using var json = JsonDocument.Parse(payload);
            if (!json.RootElement.TryGetProperty("previous", out var before) || before.ValueKind != JsonValueKind.Object
                || !json.RootElement.TryGetProperty("current", out var after) || after.ValueKind != JsonValueKind.Object
                || !before.TryGetProperty("Kind", out var beforeKind) || !after.TryGetProperty("Kind", out var afterKind)
                || !beforeKind.TryGetInt32(out previous) || !afterKind.TryGetInt32(out current)) return false;
            return true;
        }
        catch (JsonException) { return false; }
    }

    public static WorkItemKindRepairMode ParseMode(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "dry-run" or "dryrun" or "ensaio" => WorkItemKindRepairMode.DryRun,
        "apply" or "corrigir" => WorkItemKindRepairMode.Apply,
        _ => WorkItemKindRepairMode.Off,
    };
}
