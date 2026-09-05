namespace Detran.Kanban.Domain.Entities;

/// <summary>Vínculo entre uma página do wiki e uma tarefa (item de trabalho).</summary>
public class WikiPageWorkItemLink
{
    public Guid Id { get; private set; }
    public Guid WikiPageId { get; private set; }
    public Guid WorkItemId { get; private set; }
    public string CreatedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public WikiPage Page { get; private set; } = null!;
    public WorkItem WorkItem { get; private set; } = null!;

    private WikiPageWorkItemLink() { }

    public static WikiPageWorkItemLink Create(Guid pageId, Guid workItemId, string userId)
        => new()
        {
            Id = Guid.NewGuid(),
            WikiPageId = pageId,
            WorkItemId = workItemId,
            CreatedByUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
}
