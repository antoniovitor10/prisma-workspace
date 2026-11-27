using FluentValidation;
using MediatR;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Features.MethodGit.Repositories;

/// <summary>
/// HU-0007 · T-0029 (CA-9): link da página de comparação do GitHub para repositório em modo consulta (sem App).
/// Mesmo texto do PR aberto pelo Prisma ("[T-XXXX] " no título e a linha do card no fim do corpo), base = principal
/// cadastrada. Não grava nada: o vínculo vem depois pela consulta (T-0008).
/// </summary>
public sealed class GitPullRequestCompareLinks(IMethodGitStore store, GitRepositoryOperations projects, GitPullRequestOperations tasks,
    IMethodAccessContext access, IPermissionService permissions, IMethodCardLinks cardLinks)
{
    /// <summary>Acima disso o GitHub pode responder 414 URI Too Long.</summary>
    public const int MaxUrlLength = 7_500;
    public const string TruncatedNote = "(corpo completo na task do Prisma)";

    public async Task<MethodPullRequestCompareUrlDto> BuildAsync(Guid projectId, string methodId, MethodPullRequestCompareUrlRequest request,
        string actorId, CancellationToken ct)
    {
        await projects.EnsureAccessAsync(projectId, actorId, false, ct);
        var task = await tasks.SingleTaskAsync(projectId, methodId, ct);
        var actor = access.Current;
        if (actor is null || actor.ActorId != actorId || actor.Role == MethodRole.System)
            throw new MethodOperationException(403, "git_compare_permission", "O principal não pode montar o link do PR.");
        // A mesma regra que mostra o botão: quem pode editar a task.
        await permissions.EnsureAsync(actorId, PlatformPermission.Edit, PermissionScope.WorkItem, task.WorkItemId, ct);
        var link = (await store.GetTaskLinksAsync(task.WorkItemId, ct)).SingleOrDefault(x => x.Id == request.LinkId)
            ?? throw new MethodOperationException(404, "git_link_not_found", "Vínculo de branch não encontrado nesta task.");
        if (link.ObservationId is not null || string.IsNullOrEmpty(link.BranchName))
            throw new MethodOperationException(409, "git_link_not_branch", "O vínculo escolhido não é de branch.");
        if (link.BranchState != GitBranchStates.Remote)
            throw new MethodOperationException(409, "git_branch_not_pushed", $"A branch {link.BranchName} ainda não está no GitHub. Faça o push antes de abrir o PR.");
        var repository = await store.GetRepositoryAsync(link.IntegrationId, ct);
        if (repository is null || repository.ProjectId != projectId || !repository.Enabled)
            throw new MethodOperationException(409, "git_repository_disabled", "O repositório da branch não está habilitado neste projeto.");
        if (repository.Mode != GitIntegrationModes.Poll)
            throw new MethodOperationException(409, "git_repository_has_app", "Este repositório tem a GitHub App: use Abrir PR para o Prisma abrir o PR.");
        return Build(repository.RepositoryFullName, repository.DefaultBranch, link.BranchName,
            MethodPullRequestText.EnsureTaskPrefix(methodId, request.Title), request.Body, cardLinks.CardUrl(projectId, task.WorkItemId));
    }

    /// <summary>Monta a URL; se passar de <see cref="MaxUrlLength"/>, corta o corpo, põe a nota e mantém a linha do card no fim.</summary>
    public static MethodPullRequestCompareUrlDto Build(string repositoryFullName, string baseBranch, string branch, string title, string? body, string cardUrl)
    {
        var prefix = $"https://github.com/{EscapePath(repositoryFullName)}/compare/{EscapePath(baseBranch)}...{EscapePath(branch)}"
            + $"?quick_pull=1&title={Uri.EscapeDataString(title)}&body=";
        var url = prefix + Uri.EscapeDataString(MethodPullRequestText.AppendCardLine(body, cardUrl));
        if (url.Length <= MaxUrlLength) return new(url, false);
        var source = (body ?? string.Empty).TrimEnd();
        string Cut(int length) => MethodPullRequestText.AppendCardLine($"{SafeSubstring(source, length).TrimEnd()}\n\n{TruncatedNote}", cardUrl);
        // Busca binária do maior pedaço do corpo que cabe na URL já codificada.
        int low = 0, high = source.Length;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            if ((prefix + Uri.EscapeDataString(Cut(mid))).Length <= MaxUrlLength) low = mid; else high = mid - 1;
        }
        return new(prefix + Uri.EscapeDataString(Cut(low)), true);
    }

    // Não corta no meio de um par substituto (emoji, por exemplo).
    private static string SafeSubstring(string value, int length)
    {
        if (length <= 0) return string.Empty;
        if (length >= value.Length) return value;
        if (char.IsHighSurrogate(value[length - 1])) length--;
        return value[..length];
    }

    private static string EscapePath(string value) => string.Join('/', value.Split('/').Select(Uri.EscapeDataString));
}

public sealed record BuildMethodPullRequestCompareUrlQuery(Guid ProjectId, string MethodId, MethodPullRequestCompareUrlRequest Data, string ActorId)
    : IRequest<MethodPullRequestCompareUrlDto>;
public sealed class BuildMethodPullRequestCompareUrlHandler(GitPullRequestCompareLinks links)
    : IRequestHandler<BuildMethodPullRequestCompareUrlQuery, MethodPullRequestCompareUrlDto>
{
    public Task<MethodPullRequestCompareUrlDto> Handle(BuildMethodPullRequestCompareUrlQuery r, CancellationToken ct) => links.BuildAsync(r.ProjectId, r.MethodId, r.Data, r.ActorId, ct);
}

public sealed class BuildMethodPullRequestCompareUrlQueryValidator : AbstractValidator<BuildMethodPullRequestCompareUrlQuery>
{
    public BuildMethodPullRequestCompareUrlQueryValidator()
    {
        RuleFor(x => x.Data).NotNull().WithMessage("Informe o vínculo de branch, o título e o corpo do PR.");
        RuleFor(x => x.Data.LinkId).NotEmpty().WithMessage("Informe o vínculo de branch da task.").When(x => x.Data is not null);
        // O limite de 256 vale para o título final, já com o prefixo "[T-XXXX] ".
        RuleFor(x => x).Cascade(CascadeMode.Stop)
            .Must(x => !string.IsNullOrWhiteSpace(x.Data.Title)).WithMessage("Informe o título do PR.")
            .Must(x => MethodPullRequestText.EnsureTaskPrefix(x.MethodId ?? string.Empty, x.Data.Title).Length <= MethodPullRequestLimits.TitleMaxLength)
            .WithMessage($"O título do PR deve ter até {MethodPullRequestLimits.TitleMaxLength} caracteres, contando o prefixo [T-XXXX].")
            .OverridePropertyName("title")
            .When(x => x.Data is not null);
        RuleFor(x => x.Data.Body).Must(body => (body?.Length ?? 0) <= MethodPullRequestLimits.BodyMaxLength)
            .WithMessage("O corpo do PR deve ter até 60.000 caracteres.").When(x => x.Data is not null);
    }
}
