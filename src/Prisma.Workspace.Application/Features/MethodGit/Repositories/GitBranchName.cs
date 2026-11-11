using System.Text.RegularExpressions;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;

namespace Prisma.Workspace.Application.Features.MethodGit.Repositories;

/// <summary>Nome curto de branch conforme as regras de ref do Git (git check-ref-format --branch), até 255 caracteres.</summary>
public static partial class GitBranchName
{
    public const int MaxLength = 255;

    public static bool IsValid(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > MaxLength) return false;
        if (name == "@" || name == "HEAD" || name.StartsWith('-') || name.StartsWith('/') || name.EndsWith('/')
            || name.EndsWith('.') || name.Contains("..", StringComparison.Ordinal) || name.Contains("@{", StringComparison.Ordinal)
            || name.Contains("//", StringComparison.Ordinal)) return false;
        foreach (var c in name)
            if (c < 0x20 || c == 0x7F || c is ' ' or '~' or '^' or ':' or '?' or '*' or '[' or '\\') return false;
        foreach (var component in name.Split('/'))
            if (component.StartsWith('.') || component.EndsWith(".lock", StringComparison.Ordinal)) return false;
        return true;
    }

    /// <summary>Verdadeiro quando o nome cita um ID de task (T-0000) diferente da task alvo; sem ID ou com o próprio ID é aceito.</summary>
    public static bool ReferencesOtherTask(string name, string methodTaskId)
        => TaskIdPattern().Matches(name).Any(match => !string.Equals(match.Value, methodTaskId, StringComparison.Ordinal));

    public static MethodOperationException InvalidError() => new(400, "invalid_git_branch",
        "Nome de branch inválido: use um nome de branch do Git, com até 255 caracteres, sem espaços, '..', '~', '^', ':', '?', '*', '[' ou barra invertida.");

    public static MethodOperationException OtherTaskError(string methodTaskId) => new(400, "git_branch_other_task",
        $"A branch cita outra task; vincule a {methodTaskId} só a uma branch sem ID ou com o próprio ID.");

    [GeneratedRegex("T-[0-9]{4,}", RegexOptions.CultureInvariant)] private static partial Regex TaskIdPattern();
}
