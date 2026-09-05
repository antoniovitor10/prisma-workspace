namespace Detran.Kanban.Application.Interfaces;

/// <summary>
/// Consulta de usuários do Identity a partir da Application, sem acoplar a
/// camada ao ASP.NET Identity. Implementado na Infrastructure.
/// </summary>
public interface IUserDirectory
{
    /// <summary>Nome funcional resolvido no contexto da organização por id de usuário.</summary>
    Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken = default);

    /// <summary>Todos os usuários, ordenados por e-mail.</summary>
    Task<IReadOnlyList<UserSummary>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Usuários que pertencem ao tenant atual, incluindo inativos quando solicitado.</summary>
    Task<IReadOnlyList<UserSummary>> GetByIdsAsync(
        IEnumerable<string> userIds,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<UserSummary?> GetByIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca o usuário SEM o filtro de organização. Necessário nos fluxos em que a
    /// pessoa ainda não pertence a nenhuma organização (ex.: aceitar convite).
    /// </summary>
    Task<UserSummary?> GetByIdUnscopedAsync(string userId, CancellationToken cancellationToken = default);

    Task<UserSummary?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
}

/// <summary>Resumo de usuário para listagens.</summary>
public record UserSummary(string Id, string? Email, string? UserName, string DisplayName);

/// <summary>Aplica o fallback temporário definido em D46 para membros legados.</summary>
public static class UserDisplayName
{
    public static string Resolve(string userId, string? displayName, string? userName, string? email)
    {
        if (!string.IsNullOrWhiteSpace(displayName)) return displayName.Trim();

        var normalizedUserName = userName?.Trim();
        var normalizedEmail = email?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedUserName)
            && !LooksLikeEmail(normalizedUserName)
            && !string.Equals(normalizedUserName, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            return normalizedUserName;

        var normalizedId = userId.Trim();
        var shortId = normalizedId[..Math.Min(8, normalizedId.Length)];
        return $"Usuário {shortId}";
    }

    private static bool LooksLikeEmail(string value)
    {
        var at = value.IndexOf('@');
        return at > 0 && at < value.Length - 1 && value.IndexOf('.', at) > at + 1;
    }
}
