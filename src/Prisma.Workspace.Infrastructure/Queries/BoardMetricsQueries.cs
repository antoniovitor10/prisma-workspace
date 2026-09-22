using Prisma.Workspace.Application.Features.BoardMetrics;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Queries;

/// <summary>
/// Lado de leitura do dashboard do quadro: 6 métricas fixas calculadas
/// em memória sobre os dados do quadro (volume pequeno por quadro).
/// </summary>
public class BoardMetricsQueries : IBoardMetricsQueries
{
    private readonly AppDbContext _context;

    public BoardMetricsQueries(AppDbContext context) => _context = context;

    public async Task<BoardMetricsDto> GetAsync(Guid boardId, CancellationToken ct = default)
    {
        var board = await _context.Boards.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == boardId, ct);
        if (board is null)
            return new BoardMetricsDto();

        var stages = await _context.Stages.AsNoTracking()
            .Where(s => s.ProjectId == board.ProjectId)
            .OrderBy(s => s.Position)
            .Select(s => new { s.Id, s.Name })
            .ToListAsync(ct);
        var lastStageId = stages.LastOrDefault()?.Id;

        var items = await _context.WorkItems.AsNoTracking()
            .Include(w => w.TaskType)
            .Include(w => w.StageHistories)
            .Where(w => w.BoardId == boardId && w.ParentId == null)
            .ToListAsync(ct);

        var entries = await _context.TimeEntries.AsNoTracking()
            .Where(e => e.WorkItem.BoardId == boardId)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        // 1. Tarefas por etapa
        var byStage = stages
            .Select(s => new StageCountDto(s.Name, items.Count(w => w.StageId == s.Id)))
            .ToList();

        // 2. Atrasadas (vencidas e fora da última etapa)
        var late = items.Count(w => w.DueDate.HasValue && w.DueDate.Value < today && w.StageId != lastStageId);

        // 3. Entregues na semana (entraram na última etapa nesta semana)
        var dow = ((int)now.UtcDateTime.DayOfWeek + 6) % 7;
        var weekStart = new DateTimeOffset(now.UtcDateTime.Date.AddDays(-dow), TimeSpan.Zero);
        var deliveredThisWeek = lastStageId is null ? 0 : items.Count(w =>
            w.StageHistories.Any(h => h.StageId == lastStageId && h.EnteredAt >= weekStart));

        // 4. Horas por pessoa
        var userIds = entries.Select(e => e.UserId).Distinct().ToList();
        var users = await _context.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToListAsync(ct);
        var hoursByUser = entries
            .GroupBy(e => e.UserId)
            .Select(g => new UserHoursDto(
                users.FirstOrDefault(u => u.Id == g.Key)?.Email ?? g.Key,
                Math.Round(g.Sum(e => Math.Max(0, ((e.EndedAt ?? now) - e.StartedAt).TotalSeconds)) / 3600.0, 1)))
            .OrderByDescending(x => x.Hours)
            .ToList();

        // 5. Burndown simples: tarefas abertas por dia (últimos 14 dias)
        var burndown = new List<BurndownPointDto>();
        for (var i = 13; i >= 0; i--)
        {
            var day = now.UtcDateTime.Date.AddDays(-i);
            var dayEnd = new DateTimeOffset(day.AddDays(1), TimeSpan.Zero);
            var open = items.Count(w =>
                w.CreatedAt < dayEnd &&
                (lastStageId is null || !w.StageHistories.Any(h => h.StageId == lastStageId && h.EnteredAt < dayEnd && h.LeftAt == null)));
            burndown.Add(new BurndownPointDto(day.ToString("yyyy-MM-dd"), open));
        }

        // 6. Tarefas por tipo
        var byType = items
            .GroupBy(w => new { Name = w.TaskType?.Name ?? "Sem tipo", Color = w.TaskType?.Color ?? "#94A3B8" })
            .Select(g => new TypeCountDto(g.Key.Name, g.Key.Color, g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        return new BoardMetricsDto
        {
            TasksByStage = byStage,
            LateCount = late,
            DeliveredThisWeek = deliveredThisWeek,
            HoursByUser = hoursByUser,
            Burndown = burndown,
            TasksByType = byType
        };
    }
}
