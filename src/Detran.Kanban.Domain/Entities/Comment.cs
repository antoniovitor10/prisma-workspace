using System;

namespace Detran.Kanban.Domain.Entities;

/// <summary>
/// Comentário feito por um usuário em uma tarefa (WorkItem).
/// </summary>
public class Comment
{
    public Guid Id { get; set; }
    public Guid WorkItemId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    // Navegação
    public WorkItem WorkItem { get; set; } = null!;
}
