using Detran.Kanban.Domain.Exceptions;

namespace Detran.Kanban.Domain.Entities;

/// <summary>
/// Item de checklist de uma tarefa (marcável). Contagem n/m no modal.
/// </summary>
public class ChecklistItem
{
    /// <summary>Espaçamento entre posições — permite inserir no meio sem reindexar.</summary>
    public const double PassoDePosicao = 100;

    /// <summary>Identificador único.</summary>
    public Guid Id { get; set; }

    /// <summary>Tarefa dona do item.</summary>
    public Guid WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;

    /// <summary>Texto do item.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Se o item está concluído.</summary>
    public bool Done { get; set; }

    /// <summary>Posição ordinal na lista.</summary>
    public double Position { get; set; }

    /// <summary>Data/hora de criação.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Cria um item no fim da lista (após a maior posição atual).</summary>
    public static ChecklistItem Criar(Guid workItemId, string texto, double maiorPosicaoAtual)
    {
        DomainException.Garantir(!string.IsNullOrWhiteSpace(texto), "Texto obrigatório.");
        return new ChecklistItem
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItemId,
            Text = texto.Trim(),
            Done = false,
            Position = maiorPosicaoAtual + PassoDePosicao,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Marcar(bool done) => Done = done;
}
