using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Infrastructure.Persistence;

namespace Prisma.Workspace.Infrastructure.Services.MethodGit;

/// <summary>Propósito único do Data Protection para os tokens de leitura do modo consulta.</summary>
public static class GitReadCredentialProtection
{
    public const string Purpose = "Prisma.MethodGit.ReadCredentials.v1";
}

/// <summary>
/// Tokens de leitura por organização GitHub (HU-0002, D2). Grava só o valor cifrado e um prefixo de 16
/// caracteres ("github_pat_" + 5); nenhuma leitura devolve o valor, e o valor nunca vai para log ou erro.
/// </summary>
public sealed partial class EfGitReadCredentialStore(AppDbContext db, IOrganizationContext organization,
    IDataProtectionProvider protection, IUserDirectory users, TimeProvider? clock = null) : IGitReadCredentialStore
{
    private readonly IDataProtector _protector = protection.CreateProtector(GitReadCredentialProtection.Purpose);
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private Guid Tenant => organization.RequireOrganizationId();
    private IQueryable<GitReadCredential> Credentials => db.Set<GitReadCredential>().Where(x => x.OrganizationId == Tenant);

    public async Task<IReadOnlyList<GitReadCredentialDto>> ListAsync(Guid projectId, CancellationToken ct = default)
    {
        var rows = await Credentials.AsNoTracking().Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.RevokedAtUtc != null).ThenBy(x => x.GitHubOwner).ThenByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        return await DtosAsync(projectId, rows, ct);
    }

    public async Task<GitReadCredentialDto> SaveAsync(Guid projectId, string gitHubOwner, string token, string actorUserId, CancellationToken ct = default)
    {
        var owner = (gitHubOwner ?? string.Empty).Trim();
        if (!OwnerPattern().IsMatch(owner))
            throw new MethodOperationException(400, "invalid_git_owner", "Informe a organização ou o usuário dono no GitHub (letras, números e hífen, até 39 caracteres).");
        // Só o token fine-grained do GitHub (D2): nunca ecoa o valor recebido na mensagem.
        if (token is null || !TokenPattern().IsMatch(token))
            throw new MethodOperationException(400, "invalid_git_read_token", "Use um token fine-grained do GitHub (começa com github_pat_), com acesso só de leitura a Pull requests e Contents.");
        if (!await db.Set<MethodProjectSettings>().AsNoTracking().AnyAsync(x => x.ProjectId == projectId && x.OrganizationId == Tenant, ct))
            throw new MethodOperationException(404, "method_not_provisioned", "Método não provisionado neste projeto.");
        var now = _clock.GetUtcNow();
        var row = await Credentials.SingleOrDefaultAsync(x => x.ProjectId == projectId && x.GitHubOwner == owner && x.RevokedAtUtc == null, ct);
        if (row is null)
        {
            row = new GitReadCredential { Id = Guid.NewGuid(), OrganizationId = Tenant, ProjectId = projectId, GitHubOwner = owner };
            db.Add(row);
        }
        // Troca no lugar: os repositórios continuam apontando para a mesma credencial ativa.
        row.ProtectedValue = _protector.Protect(token);
        row.TokenPrefix = token[..16];
        row.CreatedAtUtc = now; row.CreatedByUserId = actorUserId;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            throw new MethodOperationException(409, "git_read_credential_changed", "O token desta organização foi alterado por outra operação. Atualize a tela.");
        }
        // Repositórios do mesmo dono no modo consulta que apontavam para um token revogado passam ao token ativo.
        var revoked = Credentials.Where(x => x.ProjectId == projectId && x.GitHubOwner == owner && x.RevokedAtUtc != null).Select(x => (Guid?)x.Id);
        await db.Set<GitRepositoryIntegration>().Where(x => x.OrganizationId == Tenant && x.ProjectId == projectId
                && x.Mode == GitIntegrationModes.Poll && revoked.Contains(x.ReadCredentialId))
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.ReadCredentialId, row.Id), ct);
        db.ChangeTracker.Clear();
        return (await DtosAsync(projectId, [row], ct))[0];
    }

    public async Task<GitReadCredentialDto> RevokeAsync(Guid projectId, Guid credentialId, string actorUserId, CancellationToken ct = default)
    {
        var row = await Credentials.SingleOrDefaultAsync(x => x.Id == credentialId && x.ProjectId == projectId, ct)
            ?? throw new MethodOperationException(404, "git_read_credential_not_found", "Token de leitura não encontrado neste projeto.");
        if (row.RevokedAtUtc is null)
        {
            row.RevokedAtUtc = _clock.GetUtcNow(); row.RevokedByUserId = actorUserId;
            // O valor cifrado deixa de existir: revogar no Prisma não depende de lembrar de revogar no GitHub.
            row.ProtectedValue = "revoked";
            await db.SaveChangesAsync(ct);
        }
        db.ChangeTracker.Clear();
        return (await DtosAsync(projectId, [row], ct))[0];
    }

    public Task<Guid?> GetActiveIdAsync(Guid projectId, string gitHubOwner, CancellationToken ct = default)
        => Credentials.AsNoTracking().Where(x => x.ProjectId == projectId && x.GitHubOwner == gitHubOwner && x.RevokedAtUtc == null)
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);

    private async Task<IReadOnlyList<GitReadCredentialDto>> DtosAsync(Guid projectId, IReadOnlyList<GitReadCredential> rows, CancellationToken ct)
    {
        var ids = rows.Select(x => (Guid?)x.Id).ToArray();
        var counts = await db.Set<GitRepositoryIntegration>().AsNoTracking().Where(x => x.OrganizationId == Tenant && x.ProjectId == projectId
                && ids.Contains(x.ReadCredentialId)).GroupBy(x => x.ReadCredentialId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key!.Value, x => x.Count, ct);
        var names = await users.GetDisplayNamesAsync(rows.Select(x => x.CreatedByUserId).Distinct().ToArray(), ct);
        return rows.Select(x => new GitReadCredentialDto(x.Id, x.GitHubOwner, x.TokenPrefix, x.CreatedAtUtc,
            names.GetValueOrDefault(x.CreatedByUserId), x.RevokedAtUtc, counts.GetValueOrDefault(x.Id))).ToArray();
    }

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$", RegexOptions.CultureInvariant)] private static partial Regex OwnerPattern();
    [GeneratedRegex("^github_pat_[A-Za-z0-9_]{20,240}$", RegexOptions.CultureInvariant)] private static partial Regex TokenPattern();
}

/// <summary>Única porta de saída do valor decifrado: usada só pelo provedor GitHub, dentro da requisição de leitura.</summary>
public interface IGitReadTokenSource
{
    Task<string> ResolveAsync(Guid credentialId, Guid projectId, Guid organizationId, CancellationToken cancellationToken = default);
}

/// <summary>Lê a credencial num escopo próprio (o provedor é singleton) e confere organização, projeto e revogação.</summary>
public sealed class GitReadTokenSource(IServiceScopeFactory scopes, IDataProtectionProvider protection) : IGitReadTokenSource
{
    private readonly IDataProtector _protector = protection.CreateProtector(GitReadCredentialProtection.Purpose);
    public async Task<string> ResolveAsync(Guid credentialId, Guid projectId, Guid organizationId, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Sem tenant no escopo novo: a busca é pontual e confere organização e projeto explicitamente.
        var row = await db.Set<GitReadCredential>().IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == credentialId && x.ProjectId == projectId && x.OrganizationId == organizationId)
            .Select(x => new { x.ProtectedValue, x.RevokedAtUtc }).SingleOrDefaultAsync(ct);
        if (row is null || row.RevokedAtUtc is not null) throw new GitHubUnavailableException("git_credential_revoked");
        try { return _protector.Unprotect(row.ProtectedValue); }
        catch (System.Security.Cryptography.CryptographicException) { throw new GitHubUnavailableException("git_credential_unreadable"); }
    }
}
