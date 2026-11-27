namespace Prisma.Workspace.Application.Features.MethodGit.Repositories;

/// <summary>
/// Texto do PR aberto pelo Prisma (princípio 10 do espaço, ADR-0003): título com "[T-XXXX] " no início e corpo terminando
/// na linha "Task no Prisma: &lt;link do card&gt;". Usado ao abrir o PR (T-0027) e no link de comparação (T-0029).
/// </summary>
public static class MethodPullRequestText
{
    public const string CardLinePrefix = "Task no Prisma: ";

    /// <summary>Põe "[T-XXXX] " no início se o título não começar com "[T-XXXX]"; nunca duplica.</summary>
    public static string EnsureTaskPrefix(string methodId, string title)
    {
        var trimmed = (title ?? string.Empty).Trim();
        var tag = $"[{methodId}]";
        return trimmed.StartsWith(tag, StringComparison.Ordinal) ? trimmed : $"{tag} {trimmed}";
    }

    public static string CardLine(string cardUrl) => CardLinePrefix + cardUrl;

    /// <summary>Acrescenta a linha do link do card no fim do corpo (sem repetir se o corpo já termina nela).</summary>
    public static string AppendCardLine(string? body, string cardUrl)
    {
        var line = CardLine(cardUrl);
        var trimmed = (body ?? string.Empty).TrimEnd();
        if (trimmed.EndsWith(line, StringComparison.Ordinal)) return trimmed;
        return trimmed.Length == 0 ? line : $"{trimmed}\n\n{line}";
    }
}
