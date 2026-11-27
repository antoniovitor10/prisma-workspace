using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;

namespace Prisma.Workspace.Infrastructure.Services.MethodSecurity;

/// <summary>
/// Regra única de "task do método elegível" (HU-0008, D7; ADR-0106): item de topo ou filho de uma História do
/// método (UserStory com documento Hu) no mesmo quadro. As demais condições (ID T-, quadro do método,
/// não arquivado) continuam em quem chama.
/// </summary>
internal static class MethodEligibility
{
    /// <summary>Expressão traduzível para SQL; lê documentos sem filtro de tenant porque o pai já está no mesmo quadro.</summary>
    internal static Expression<Func<WorkItem, bool>> HasEligibleParent(AppDbContext db)
        => item => item.ParentId == null
            || item.Parent!.Kind == WorkItemKind.UserStory && item.Parent.BoardId == item.BoardId && !item.Parent.IsArchived
                && db.Set<MethodDocument>().IgnoreQueryFilters()
                    .Any(d => d.WorkItemId == item.ParentId && d.Kind == MethodDocumentKind.Hu);

    /// <summary>
    /// Verdadeiro se <paramref name="parentId"/> é nulo ou uma História do método no quadro <paramref name="boardId"/>.
    /// Considera também o que está pendente no rastreador (História e documento criados na mesma gravação).
    /// </summary>
    internal static async Task<bool> HasEligibleParentAsync(AppDbContext db, Guid? parentId, Guid boardId, CancellationToken ct)
        => parentId is null || await IsMethodStoryAsync(db, parentId.Value, boardId, ct);

    internal static async Task<bool> IsMethodStoryAsync(AppDbContext db, Guid storyId, Guid boardId, CancellationToken ct)
    {
        var local = db.WorkItems.Local.SingleOrDefault(x => x.Id == storyId);
        var story = local is not null
            ? new { local.Kind, local.BoardId, local.IsArchived }
            : await db.WorkItems.IgnoreQueryFilters().AsNoTracking().Where(x => x.Id == storyId)
                .Select(x => new { x.Kind, x.BoardId, x.IsArchived }).SingleOrDefaultAsync(ct);
        if (story is null || story.Kind != WorkItemKind.UserStory || story.BoardId != boardId || story.IsArchived) return false;
        if (db.ChangeTracker.Entries<MethodDocument>().Any(x => x.State != EntityState.Deleted
            && x.Entity.WorkItemId == storyId && x.Entity.Kind == MethodDocumentKind.Hu)) return true;
        return await db.Set<MethodDocument>().IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(x => x.WorkItemId == storyId && x.Kind == MethodDocumentKind.Hu, ct);
    }
}
