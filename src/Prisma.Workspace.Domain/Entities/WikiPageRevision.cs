namespace Prisma.Workspace.Domain.Entities;

/// <summary>
/// Versão histórica de uma página do wiki. Uma revisão representa uma sessão
/// de edição: enquanto o mesmo autor edita dentro da janela de sessão, a mesma
/// revisão é atualizada; um novo autor (ou uma pausa) inicia uma nova revisão.
/// </summary>
public class WikiPageRevision
{
    /// <summary>Janela em que edições do mesmo autor pertencem à mesma sessão.</summary>
    public static readonly TimeSpan SessionWindow = TimeSpan.FromMinutes(15);

    public Guid Id { get; private set; }
    public Guid WikiPageId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string ContentHtml { get; private set; } = string.Empty;
    public string AuthorUserId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public WikiPage Page { get; private set; } = null!;

    private WikiPageRevision() { }

    public static WikiPageRevision Create(Guid pageId, string title, string html, string authorUserId)
    {
        var now = DateTimeOffset.UtcNow;
        return new WikiPageRevision
        {
            Id = Guid.NewGuid(),
            WikiPageId = pageId,
            Title = title,
            ContentHtml = html ?? string.Empty,
            AuthorUserId = authorUserId,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>Atualiza a revisão da sessão em andamento com o estado mais recente.</summary>
    public void Amend(string title, string html)
    {
        Title = title;
        ContentHtml = html ?? string.Empty;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool MesmaSessao(string authorUserId, DateTimeOffset now)
        => AuthorUserId == authorUserId && now - UpdatedAt < SessionWindow;
}
