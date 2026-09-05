using System;

namespace Detran.Kanban.Domain.Entities;

/// <summary>
/// Evento de sistema (audit trail) de uma tarefa: mudanca de etapa,
/// alocacao/desalocacao, mudanca de estimativa etc.
/// </summary>
public class TaskEvent
{
    public Guid Id { get; set; }
    public Guid WorkItemId { get; set; }

    /// <summary>Usuario que causou o evento.</summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>Tipo do evento (stage_changed, assigned, unassigned, estimate_changed...).</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Payload JSON com detalhes do evento (nomes de etapas, usuario alvo etc.).</summary>
    public string? Payload { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // Navegação
    public WorkItem WorkItem { get; set; } = null!;

    /// <summary>Cria um evento de auditoria carimbado com a hora atual.</summary>
    public static TaskEvent Registrar(Guid workItemId, string actorId, string kind, string? payloadJson = null)
    {
        return new TaskEvent
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItemId,
            ActorId = actorId,
            Kind = kind,
            Payload = payloadJson,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
