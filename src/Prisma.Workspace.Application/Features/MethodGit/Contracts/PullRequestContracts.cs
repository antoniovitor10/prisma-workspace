namespace Prisma.Workspace.Application.Features.MethodGit.Contracts;

/// <summary>Limites do rascunho e do PR aberto pelo Prisma (HU-0007, PRD "Dados").</summary>
public static class MethodPullRequestLimits
{
    public const int TitleMaxLength = 256;
    // Sobra espaço para a linha "Task no Prisma: <link>" que o Prisma acrescenta ao abrir o PR.
    public const int BodyMaxLength = 60_000;
}

/// <summary>Rascunho de título e corpo do PR gravado pelo agente (ou por uma pessoa) para a task.</summary>
public sealed record SaveMethodPullRequestDraftRequest(string Title, string? Body);

/// <summary>
/// Rascunho atual da task: o último evento <c>git_pr_draft_saved</c>. <see cref="Changed"/> só vem na gravação:
/// false quando o texto era igual ao atual e nenhum evento novo foi criado.
/// </summary>
public sealed record MethodPullRequestDraftDto(string Title, string Body, DateTimeOffset UpdatedAtUtc, string? ActorName, bool? Changed = null);

/// <summary>Pedido da UI para abrir o PR da branch vinculada (<see cref="LinkId"/> é o vínculo de branch da task).</summary>
public sealed record OpenMethodPullRequestRequest(Guid LinkId, string Title, string? Body, bool Draft = false);

/// <summary>Resultado de abrir o PR: <c>opened</c> (criado pelo Prisma) ou <c>linked_existing</c> (já existia e foi vinculado).</summary>
public sealed record OpenMethodPullRequestResult(string Outcome, int Number, string Url, bool Moved, string Message);

public static class MethodPullRequestOutcomes
{
    public const string Opened = "opened";
    public const string LinkedExisting = "linked_existing";
}

/// <summary>PR a criar no GitHub: head é a branch da task e base a principal cadastrada do repositório.</summary>
public sealed record GitPullRequestCreation(string Head, string Base, string Title, string Body, bool Draft);

/// <summary>Registro do PR aberto ou vinculado pelo botão da UI; vira o evento <c>git_pr_opened</c> (sem token e sem corpo).</summary>
public sealed record GitPullRequestOpenedRecord(Guid IntegrationId, string RepositoryFullName, int Number, string Url,
    bool Draft, string Outcome, bool Moved);

/// <summary>
/// Recusa do GitHub ao listar ou abrir PR, já classificada em código fixo (<c>git_pr_*</c>, <c>git_rate_limited</c>,
/// <c>git_timeout</c>, <c>git_api_unavailable</c>). Nunca carrega texto bruto do GitHub nem o token.
/// </summary>
public sealed class GitPullRequestRejectedException(string code, DateTimeOffset? retryAtUtc = null)
    : Exception("O GitHub recusou a operação de PR.")
{
    public string Code { get; } = code;
    public DateTimeOffset? RetryAtUtc { get; } = retryAtUtc;
}

public static class GitPullRequestErrorCodes
{
    public const string PermissionMissing = "git_pr_permission_missing";
    public const string AlreadyExists = "git_pr_already_exists";
    public const string NothingToCompare = "git_pr_nothing_to_compare";
    public const string HeadMissing = "git_pr_head_missing";
    public const string DraftUnsupported = "git_pr_draft_unsupported";
    public const string Invalid = "git_pr_invalid";
    public const string LinkFailed = "git_pr_link_failed";
    public const string RateLimited = "git_rate_limited";
    public const string Timeout = "git_timeout";
    public const string Unavailable = "git_api_unavailable";
}

/// <summary>Pedido do link de comparação do GitHub (repositório em modo consulta, sem App): o corpo vai no body, nunca na URL do Prisma.</summary>
public sealed record MethodPullRequestCompareUrlRequest(Guid LinkId, string Title, string? Body);

/// <summary>Link de comparação montado pelo servidor; <see cref="Truncated"/> indica que o corpo foi cortado para caber na URL.</summary>
public sealed record MethodPullRequestCompareUrlDto(string Url, bool Truncated);

/// <summary>Resumo do rascunho para a aba Git: quando foi gravado e por quem (sem o corpo).</summary>
public sealed record TaskGitPullRequestDraftDto(DateTimeOffset UpdatedAtUtc, string? ActorName);
