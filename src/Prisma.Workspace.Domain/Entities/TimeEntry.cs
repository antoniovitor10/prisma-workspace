using Prisma.Workspace.Domain.Exceptions;

namespace Prisma.Workspace.Domain.Entities;

/// <summary>
/// Lançamento de horas trabalhadas em um item de trabalho.
/// Um lançamento com <see cref="EndedAt"/> nulo é o cronômetro em andamento;
/// para fechá-lo use <see cref="Encerrar"/>.
/// </summary>
public class TimeEntry
{
    public Guid Id { get; private set; }

    /// <summary>Identificador do item de trabalho.</summary>
    public Guid WorkItemId { get; private set; }

    /// <summary>Identificador do usuário que registrou o tempo.</summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>Data/hora de início do período trabalhado.</summary>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>Data/hora de fim do período trabalhado. Nulo = em andamento.</summary>
    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>Observação opcional sobre o período trabalhado.</summary>
    public string? Note { get; private set; }

    /// <summary>
    /// Verdadeiro quando o lançamento foi informado manualmente (início e fim
    /// digitados); falso quando veio do cronômetro (registro automático).
    /// </summary>
    public bool IsManual { get; private set; }

    /// <summary>Data/hora de criação do registro.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Duração em segundos. Nulo enquanto o cronômetro está aberto.</summary>
    public int? DurationSeconds =>
        EndedAt.HasValue
            ? (int?)(EndedAt.Value - StartedAt).TotalSeconds
            : null;

    /// <summary>Se este lançamento é o cronômetro em andamento.</summary>
    public bool EstaAberto => EndedAt is null;

    // ── Navegação ──────────────────────────────────────────────────────

    /// <summary>Item de trabalho associado.</summary>
    public WorkItem WorkItem { get; set; } = null!;

    // Construtor sem parâmetros exigido pelo EF Core.
    private TimeEntry()
    {
    }

    /// <summary>Abre um cronômetro agora para o usuário na tarefa.</summary>
    public static TimeEntry IniciarAgora(Guid workItemId, string userId, string? note)
    {
        var now = DateTimeOffset.UtcNow;
        return new TimeEntry
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItemId,
            UserId = userId,
            StartedAt = now,
            Note = Normalizar(note),
            IsManual = false,
            CreatedAt = now
        };
    }

    /// <summary>Cria um lançamento manual (início e fim informados).</summary>
    public static TimeEntry Manual(Guid workItemId, string userId, DateTimeOffset startedAt, DateTimeOffset endedAt, string? note)
    {
        DomainException.Garantir(endedAt > startedAt, "O fim deve ser maior que o início.");
        return new TimeEntry
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItemId,
            UserId = userId,
            StartedAt = startedAt,
            EndedAt = endedAt,
            Note = Normalizar(note),
            IsManual = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>Fecha o cronômetro. Opcionalmente substitui a observação.</summary>
    public void Encerrar(string? note = null)
    {
        DomainException.Garantir(EstaAberto, "Este lançamento já foi encerrado.");
        EndedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(note)) Note = note.Trim();
    }

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
