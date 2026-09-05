namespace Detran.Kanban.Domain.Entities;

/// <summary>
/// Associação N:N entre <see cref="WorkItem"/> e <see cref="Tag"/>.
/// </summary>
public class WorkItemTag
{
    public Guid WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;

    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
