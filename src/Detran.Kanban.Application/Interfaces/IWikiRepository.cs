using Detran.Kanban.Domain.Entities;

namespace Detran.Kanban.Application.Interfaces;

public interface IWikiRepository
{
    /// <summary>Todas as páginas do projeto (para montar a árvore).</summary>
    Task<IReadOnlyList<WikiPage>> GetTreeAsync(Guid projectId, bool includeDeleted, CancellationToken cancellationToken = default);

    /// <summary>Página rastreada por id (respeita isolamento de organização).</summary>
    Task<WikiPage?> GetByIdAsync(Guid pageId, CancellationToken cancellationToken = default);

    Task<bool> HasAnyAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<double> GetNextPositionAsync(Guid projectId, Guid? parentPageId, CancellationToken cancellationToken = default);

    Task AddAsync(WikiPage page, CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);

    // ── Revisões (histórico) ────────────────────────────────────────────
    Task<WikiPageRevision?> GetLatestRevisionAsync(Guid pageId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WikiPageRevision>> GetRevisionsAsync(Guid pageId, CancellationToken cancellationToken = default);
    Task<WikiPageRevision?> GetRevisionAsync(Guid revisionId, Guid pageId, CancellationToken cancellationToken = default);
    void AddRevision(WikiPageRevision revision);

    // ── Anexos ──────────────────────────────────────────────────────────
    Task<IReadOnlyList<WikiAttachment>> GetAttachmentsAsync(Guid pageId, CancellationToken cancellationToken = default);
    Task<WikiAttachment?> GetAttachmentAsync(Guid attachmentId, Guid pageId, CancellationToken cancellationToken = default);
    void AddAttachment(WikiAttachment attachment);
    void RemoveAttachment(WikiAttachment attachment);

    // ── Vínculos página↔tarefa ──────────────────────────────────────────
    Task<WorkItem?> GetProjectWorkItemByNumberAsync(long number, Guid projectId, CancellationToken cancellationToken = default);
    Task<WikiPageWorkItemLink?> GetLinkAsync(Guid pageId, Guid workItemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WikiPageWorkItemLink>> GetLinksByPageAsync(Guid pageId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WikiPageWorkItemLink>> GetLinksByWorkItemAsync(Guid workItemId, CancellationToken cancellationToken = default);
    void AddLink(WikiPageWorkItemLink link);
    void RemoveLink(WikiPageWorkItemLink link);
}
