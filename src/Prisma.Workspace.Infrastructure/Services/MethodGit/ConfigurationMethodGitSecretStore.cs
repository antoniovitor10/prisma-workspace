using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Interfaces;

namespace Prisma.Workspace.Infrastructure.Services.MethodGit;

public sealed partial class ConfigurationMethodGitSecretStore(IConfiguration configuration) : IMethodGitSecretStore
{
    public Task<string> ResolveAsync(string reference, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (reference is null || !ReferencePattern().IsMatch(reference))
            throw new MethodOperationException(400, "invalid_secret_reference", "Referência de segredo inválida.");
        var value = configuration[$"MethodGit:Secrets:{reference}"];
        if (string.IsNullOrEmpty(value)) throw new MethodOperationException(503, "git_secret_unavailable", "Segredo da integração não configurado. Avise o administrador.");
        return Task.FromResult(value);
    }
    [GeneratedRegex("^[A-Za-z0-9_-]{1,128}$", RegexOptions.CultureInvariant)] private static partial Regex ReferencePattern();
}
