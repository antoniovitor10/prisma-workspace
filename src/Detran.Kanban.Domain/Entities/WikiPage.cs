using Detran.Kanban.Domain.Exceptions;

namespace Detran.Kanban.Domain.Entities;

/// <summary>
/// Página do wiki de um projeto. As páginas formam uma árvore
/// (<see cref="ParentPageId"/>) e trazem trava de edição, soft-delete e trilha
/// de auditoria. O conteúdo é HTML já sanitizado na camada de aplicação.
/// </summary>
public class WikiPage
{
    /// <summary>Trava de edição expira após este tempo sem heartbeat.</summary>
    public static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(3);

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }

    /// <summary>Página-pai na árvore. Nulo = página de primeiro nível.</summary>
    public Guid? ParentPageId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    /// <summary>Conteúdo em HTML sanitizado.</summary>
    public string ContentHtml { get; private set; } = string.Empty;

    /// <summary>Ordem entre irmãs (mesmo pai).</summary>
    public double Position { get; private set; }

    // ── Trava de edição ────────────────────────────────────────────────
    public string? LockedByUserId { get; private set; }
    public DateTimeOffset? LockedAt { get; private set; }

    // ── Soft-delete (lixeira) ───────────────────────────────────────────
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public string? DeletedByUserId { get; private set; }

    // ── Auditoria ───────────────────────────────────────────────────────
    public string CreatedByUserId { get; private set; } = string.Empty;
    public string UpdatedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Controle de concorrência otimista.</summary>
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    // ── Navegação ───────────────────────────────────────────────────────
    public Project Project { get; private set; } = null!;
    public WikiPage? Parent { get; private set; }
    public ICollection<WikiPage> Children { get; private set; } = new List<WikiPage>();

    private WikiPage() { }

    public static WikiPage Create(
        Guid projectId, Guid? parentPageId, string title, string userId, double position)
    {
        DomainException.Garantir(!string.IsNullOrWhiteSpace(title), "O título da página é obrigatório.");
        DomainException.Garantir(!string.IsNullOrWhiteSpace(userId), "Usuário obrigatório.");
        var now = DateTimeOffset.UtcNow;
        return new WikiPage
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ParentPageId = parentPageId,
            Title = title.Trim(),
            ContentHtml = string.Empty,
            Position = position,
            CreatedByUserId = userId,
            UpdatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Rename(string title, string userId)
    {
        DomainException.Garantir(!string.IsNullOrWhiteSpace(title), "O título da página é obrigatório.");
        Title = title.Trim();
        Touch(userId);
    }

    /// <summary>Grava o conteúdo (já sanitizado). Usado pelo autosave.</summary>
    public void SetContent(string sanitizedHtml, string userId)
    {
        ContentHtml = sanitizedHtml ?? string.Empty;
        Touch(userId);
    }

    public void MoveTo(Guid? parentPageId, double position, string userId)
    {
        DomainException.Garantir(parentPageId != Id, "Uma página não pode ser filha dela mesma.");
        ParentPageId = parentPageId;
        Position = position;
        Touch(userId);
    }

    // ── Trava ───────────────────────────────────────────────────────────
    public bool EstaTravada(DateTimeOffset now) =>
        LockedByUserId is not null && LockedAt is not null && now - LockedAt.Value < LockTimeout;

    public bool PodeEditar(string userId, DateTimeOffset now) =>
        !EstaTravada(now) || LockedByUserId == userId;

    /// <summary>Adquire/renova a trava para o usuário. Falha se travada por outro.</summary>
    public void AdquirirTrava(string userId, DateTimeOffset now)
    {
        DomainException.Garantir(PodeEditar(userId, now),
            "Esta página está sendo editada por outra pessoa.");
        LockedByUserId = userId;
        LockedAt = now;
    }

    public void LiberarTrava(string userId)
    {
        // Só quem detém a trava (ou uma liberação forçada) limpa.
        if (LockedByUserId == userId) LimparTrava();
    }

    /// <summary>Liberação forçada (gestão) independente de quem travou.</summary>
    public void ForcarLiberarTrava() => LimparTrava();

    private void LimparTrava()
    {
        LockedByUserId = null;
        LockedAt = null;
    }

    // ── Lixeira ─────────────────────────────────────────────────────────
    public void MoverParaLixeira(string userId)
    {
        DomainException.Garantir(!IsDeleted, "A página já está na lixeira.");
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        DeletedByUserId = userId;
        LimparTrava();
        Touch(userId);
    }

    public void Restaurar(string userId)
    {
        DomainException.Garantir(IsDeleted, "A página não está na lixeira.");
        IsDeleted = false;
        DeletedAt = null;
        DeletedByUserId = null;
        Touch(userId);
    }

    private void Touch(string userId)
    {
        UpdatedByUserId = string.IsNullOrWhiteSpace(userId) ? UpdatedByUserId : userId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
