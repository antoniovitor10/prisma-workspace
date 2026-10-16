namespace Prisma.Workspace.Domain.Enums;

public enum MethodRole : byte { Human = 1, Agent = 2, System = 3 }
public enum MethodAuthenticationKind : byte { Unknown = 0, UiSession = 1, MethodToken = 2, IntegrationWorker = 3 }
public enum MethodGitPhase : byte { NoGit = 0, Webhooks = 1, Reconciliation = 2, Deployment = 3 }

public static class MethodStatusCodes
{
    public const string Backlog = "backlog";
    public const string Ready = "pronta";
    public const string InDevelopment = "em-desenvolvimento";
    public const string InReview = "em-revisao";
    public const string InTest = "em-teste";
    public const string Completed = "concluida";
    public const string Blocked = "bloqueada";
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[]
        { Backlog, Ready, InDevelopment, InReview, InTest, Completed, Blocked });

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}

public static class MethodRoleCodes
{
    public static string ToCode(this MethodRole role) => role switch
    {
        MethodRole.Human => "humano", MethodRole.Agent => "agente", MethodRole.System => "sistema",
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };
    public static MethodRole Parse(string role) => role switch
    {
        "humano" => MethodRole.Human, "agente" => MethodRole.Agent, "sistema" => MethodRole.System,
        _ => throw new ArgumentException("Papel do método inválido.", nameof(role))
    };
}

public static class MethodTokenFormat
{
    public const string Prefix = "pmt_";
    public const int RandomByteLength = 32;
    public const int ValueLength = 47;

    public static bool IsWellFormed(string? value) => value is not null
        && value.Length == ValueLength && value.StartsWith(Prefix, StringComparison.Ordinal)
        && value[Prefix.Length..].All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z'
            or >= '0' and <= '9' or '-' or '_');
}
