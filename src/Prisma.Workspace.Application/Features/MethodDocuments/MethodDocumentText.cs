using System.Security.Cryptography;
using System.Text;

namespace Prisma.Workspace.Application.Features.MethodDocuments;

/// <summary>
/// Regras de texto dos documentos do método: o servidor guarda o arquivo exato, só com o fim de linha em LF
/// (BOM mantido), lê do cabeçalho YAML apenas campos simples de topo e reescreve só <c>titulo:</c> e <c>status:</c> (D3, D4).
/// </summary>
public static class MethodDocumentText
{
    public static readonly IReadOnlyList<string> PlanningStatuses = ["aberta", "planejada", "concluida", "cancelada"];

    public static string Normalize(string content) => content.Replace("\r\n", "\n", StringComparison.Ordinal);

    public static string Hash(string content)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    /// <summary>Campos simples (<c>chave: valor</c>, sem recuo) do cabeçalho YAML; vazio se não houver cabeçalho.</summary>
    public static IReadOnlyDictionary<string, string> ParseHeader(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!TryHeaderRange(content, out var lines, out var start, out var end)) return result;
        for (var i = start; i < end; i++)
        {
            var line = lines[i];
            if (line.Length == 0 || char.IsWhiteSpace(line[0]) || line[0] == '#') continue;
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var key = line[..colon].Trim();
            if (key.Length == 0 || key.Any(char.IsWhiteSpace)) continue;
            result[key] = Unquote(line[(colon + 1)..]);
        }
        return result;
    }

    /// <summary>
    /// Reescreve só as linhas <c>titulo:</c> e <c>status:</c> do cabeçalho (acrescenta a linha que faltar antes do
    /// fechamento); o resto do texto não muda. Lança <see cref="ArgumentException"/> se não houver cabeçalho.
    /// </summary>
    public static string RewriteHeader(string content, string? title, string? status)
    {
        if (!TryHeaderRange(content, out var lines, out var start, out var end))
            throw new ArgumentException("O documento não tem cabeçalho YAML.");
        var titleDone = title is null;
        var statusDone = status is null;
        for (var i = start; i < end; i++)
        {
            if (!titleDone && IsKey(lines[i], "titulo")) { lines[i] = $"titulo: {Quote(title!)}"; titleDone = true; }
            else if (!statusDone && IsKey(lines[i], "status")) { lines[i] = $"status: {status}{TrailingComment(lines[i])}"; statusDone = true; }
        }
        var extra = new List<string>();
        if (!titleDone) extra.Add($"titulo: {Quote(title!)}");
        if (!statusDone) extra.Add($"status: {status}");
        lines.InsertRange(end, extra);
        return string.Join('\n', lines);
    }

    private static bool TryHeaderRange(string content, out List<string> lines, out int start, out int end)
    {
        lines = content.Split('\n').ToList();
        start = end = 0;
        if (lines.Count < 2 || lines[0].TrimStart('﻿').TrimEnd() != "---") return false;
        for (var i = 1; i < lines.Count; i++)
            if (lines[i].TrimEnd() == "---") { start = 1; end = i; return true; }
        return false;
    }

    private static bool IsKey(string line, string key)
        => line.StartsWith(key, StringComparison.Ordinal) && line.Length > key.Length
            && line[key.Length..].TrimStart(' ', '\t').StartsWith(':');

    private static string Unquote(string raw)
    {
        var value = raw.Trim();
        if (value.Length >= 2 && value[0] == '"')
        {
            var close = value.LastIndexOf('"');
            if (close > 0) return value[1..close].Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);
        }
        if (value.Length >= 2 && value[0] == '\'')
        {
            var close = value.LastIndexOf('\'');
            if (close > 0) return value[1..close].Replace("''", "'", StringComparison.Ordinal);
        }
        var comment = value.IndexOf(" #", StringComparison.Ordinal);
        return (comment >= 0 ? value[..comment] : value).Trim();
    }

    private static string TrailingComment(string line)
    {
        var colon = line.IndexOf(':');
        var rest = line[(colon + 1)..];
        if (rest.TrimStart().StartsWith('"') || rest.TrimStart().StartsWith('\'')) return string.Empty;
        var comment = rest.IndexOf(" #", StringComparison.Ordinal);
        if (comment < 0) return string.Empty;
        var start = comment;
        while (start > 0 && char.IsWhiteSpace(rest[start - 1])) start--;
        return rest[start..];
    }

    private static string Quote(string value)
        => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
