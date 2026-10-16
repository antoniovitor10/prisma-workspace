using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodSecurity;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Authorization;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;

namespace Prisma.Workspace.Infrastructure.Identity.MethodTokens;

public sealed class MethodTokenService(AppDbContext db, IMethodAccessContext context,
    IPermissionService permissions, MethodPermissionEvaluator delegation, TimeProvider clock,
    IMethodFeatureConfiguration? feature = null) : IMethodTokenService
{
    public async Task<IssuedMethodTokenDto> IssueAsync(Guid projectId, IssueMethodTokenRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await RequireAdminUiAsync(projectId, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120
            || request.ValidDays is < 1 or > 90)
            throw Invalid("Informe nome de até 120 caracteres e validade de 1 a 90 dias.");
        MethodRole role;
        try { role = MethodRoleCodes.Parse(request.Role); }
        catch (ArgumentException) { throw Invalid("Papel do método inválido."); }
        var orgId = actor.OrganizationId!.Value;
        var now = clock.GetUtcNow();
        MethodPrincipal principal;
        HashSet<PlatformPermission> requested;
        if (request.PrincipalId is { } principalId)
        {
            principal = await db.Set<MethodPrincipal>().SingleOrDefaultAsync(x => x.Id == principalId
                && x.ProjectId == projectId && x.OrganizationId == orgId && x.IsActive, cancellationToken)
                ?? throw Forbidden("O principal não está ativo neste projeto.");
            // Checar o vínculo antes de qualquer atualização ou geração do segredo da rotação.
            if (principal.Role == MethodRole.Human && principal.HumanUserId != actor.ActorId)
                throw Forbidden("Um token humano só pode ser reemitido pela própria pessoa autenticada.");
            if (role != principal.Role || request.PrincipalDisplayName is not null
                && request.PrincipalDisplayName.Trim() != principal.DisplayName)
                throw Invalid("Rotação não altera papel nem nome do principal.");
            requested = MethodPermissionEvaluator.Ceiling(principal);
            if (request.Permissions is not null)
            {
                if (request.Permissions.Any(x => !Enum.IsDefined(x)) || !request.Permissions.All(requested.Contains))
                    throw Forbidden("Rotação não pode ampliar o teto de permissões do principal.");
                requested.IntersectWith(request.Permissions);
            }
        }
        else
        {
            var displayName = role == MethodRole.Human ? actor.DisplayName
                : request.PrincipalDisplayName?.Trim() ?? request.Name.Trim();
            if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 200)
                throw Invalid("Informe um nome de exibição de até 200 caracteres.");
            principal = new MethodPrincipal
            {
                Id = Guid.NewGuid(), OrganizationId = orgId, ProjectId = projectId, Role = role,
                DisplayName = displayName, HumanUserId = role == MethodRole.Human ? actor.ActorId : null,
                DelegatedByUserId = actor.ActorId, CreatedAtUtc = now
            };
            requested = request.Permissions?.ToHashSet()
                ?? Enum.GetValues<PlatformPermission>().Where(x => role == MethodRole.Human
                    || MethodPermissionEvaluator.RoleAllows(role, x, PermissionScope.Project)).ToHashSet();
            if (requested.Any(x => !Enum.IsDefined(x))) throw Invalid("Permissão desconhecida no teto do token.");
            if (requested.Any(x => !MethodPermissionEvaluator.RoleAllows(role, x, PermissionScope.Project)))
                throw Forbidden("Agente e sistema não herdam permissões administrativas do emissor.");
        }
        var effective = new HashSet<PlatformPermission>();
        foreach (var permission in requested)
        {
            if (await delegation.HasUserPermissionAsync(orgId, projectId, actor.ActorId, permission,
                    PermissionScope.Project, projectId, cancellationToken)
                && await delegation.HasUserPermissionAsync(orgId, projectId, principal.DelegatedByUserId, permission,
                    PermissionScope.Project, projectId, cancellationToken)) effective.Add(permission);
        }
        if (!effective.Contains(PlatformPermission.View))
            throw Forbidden("A delegação precisa de acesso de leitura vigente ao projeto.");
        // A rotação só mantém ou reduz o teto do principal, inclusive para tokens antigos ainda ativos.
        principal.PermissionCeilingJson = JsonSerializer.Serialize(effective.OrderBy(x => x));
        if (db.Entry(principal).State == EntityState.Detached) db.Set<MethodPrincipal>().Add(principal);
        var raw = MethodTokenFormat.Prefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(MethodTokenFormat.RandomByteLength))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var token = new MethodApiToken
        {
            Id = Guid.NewGuid(), PrincipalId = principal.Id, Principal = principal, ProjectId = projectId,
            Name = request.Name.Trim(), TokenHash = Hash(raw), TokenPrefix = raw[..12],
            IssuedByUserId = actor.ActorId, IssuedAtUtc = now, ExpiresAtUtc = now.AddDays(request.ValidDays)
        };
        db.Set<MethodApiToken>().Add(token);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new MethodOperationException(409, "principal_changed", "O principal mudou. Atualize os dados antes de reemitir."); }
        return new IssuedMethodTokenDto(Metadata(token, principal, effective), raw);
    }

    public async Task<IReadOnlyList<MethodTokenDto>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await RequireAdminUiAsync(projectId, cancellationToken);
        var rows = await db.Set<MethodApiToken>().AsNoTracking().Include(x => x.Principal)
            .Where(x => x.ProjectId == projectId).OrderByDescending(x => x.IssuedAtUtc).ToListAsync(cancellationToken);
        return rows.Select(x => Metadata(x, x.Principal, MethodPermissionEvaluator.Ceiling(x.Principal))).ToList();
    }

    public async Task RevokeAsync(Guid projectId, Guid tokenId, CancellationToken cancellationToken = default)
    {
        var actor = await RequireAdminUiAsync(projectId, cancellationToken);
        var token = await db.Set<MethodApiToken>().SingleOrDefaultAsync(x => x.Id == tokenId && x.ProjectId == projectId, cancellationToken)
            ?? throw new MethodOperationException(404, "token_not_found", "Token não encontrado neste projeto.");
        if (token.RevokedAtUtc is not null) return;
        token.RevokedAtUtc = clock.GetUtcNow();
        token.RevokedByUserId = actor.ActorId;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<MethodActorIdentity?> AuthenticateAsync(string token, string correlationId, CancellationToken cancellationToken = default)
    {
        if (feature?.Enabled == false || !MethodTokenFormat.IsWellFormed(token)) return null;
        var hash = Hash(token);
        // Única descoberta não filtrada por tenant: hash aleatório. Escopo é validado antes de qualquer acesso de negócio.
        var row = await db.Set<MethodApiToken>().IgnoreQueryFilters().AsNoTracking().Include(x => x.Principal)
            .SingleOrDefaultAsync(x => x.TokenHash.SequenceEqual(hash), cancellationToken);
        if (row is null || !CryptographicOperations.FixedTimeEquals(row.TokenHash, hash)) return null;
        return await ValidateSessionAsync(row, correlationId, true, cancellationToken);
    }

    public async Task<MethodActorIdentity?> RevalidateSessionAsync(Guid tokenId, string actorId, string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (feature?.Enabled == false) return null;
        var row = await db.Set<MethodApiToken>().IgnoreQueryFilters().AsNoTracking().Include(x => x.Principal)
            .SingleOrDefaultAsync(x => x.Id == tokenId, cancellationToken);
        if (row is null) return null;
        var expected = row.Principal.Role == MethodRole.Human ? row.Principal.HumanUserId : $"method:{row.PrincipalId:D}";
        return expected == actorId ? await ValidateSessionAsync(row, correlationId, false, cancellationToken) : null;
    }

    private async Task<MethodActorIdentity?> ValidateSessionAsync(MethodApiToken row, string correlationId, bool recordUsage,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (row.RevokedAtUtc is not null || row.ExpiresAtUtc <= now || row.IssuedAtUtc > now
            || !row.Principal.IsActive || row.Principal.ProjectId != row.ProjectId) return null;
        var principal = row.Principal;
        if (!Enum.IsDefined(principal.Role) || principal.Role == MethodRole.Human
            && (string.IsNullOrWhiteSpace(principal.HumanUserId) || principal.HumanUserId != principal.DelegatedByUserId)) return null;
        var settings = await db.Set<MethodProjectSettings>().IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ProjectId == row.ProjectId && x.OrganizationId == principal.OrganizationId && x.Enabled, cancellationToken);
        if (settings is null) return null;
        var ceiling = MethodPermissionEvaluator.Ceiling(principal);
        if (!ceiling.Contains(PlatformPermission.View)
            || !await delegation.HasUserPermissionAsync(principal.OrganizationId, principal.ProjectId, principal.DelegatedByUserId,
                PlatformPermission.View, PermissionScope.Project, principal.ProjectId, cancellationToken)
            || !await delegation.HasUserPermissionAsync(principal.OrganizationId, principal.ProjectId, row.IssuedByUserId,
                PlatformPermission.View, PermissionScope.Project, principal.ProjectId, cancellationToken)) return null;
        var actor = new MethodActorIdentity(MethodAuthenticationKind.MethodToken, principal.Role, principal.Id,
            principal.Role == MethodRole.Human ? principal.HumanUserId! : $"method:{principal.Id:D}", principal.DisplayName,
            principal.OrganizationId, principal.ProjectId, row.Id, row.ExpiresAtUtc,
            ceiling.ToFrozenSet(), MethodChannels.Mcp, correlationId);
        if (recordUsage && (row.LastUsedAtUtc is null || now - row.LastUsedAtUtc >= TimeSpan.FromMinutes(5)))
        {
            if (db.Database.IsRelational())
                await db.Set<MethodApiToken>().IgnoreQueryFilters().Where(x => x.Id == row.Id && x.RevokedAtUtc == null
                    && (x.LastUsedAtUtc == null || x.LastUsedAtUtc <= now.AddMinutes(-5)))
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.LastUsedAtUtc, now), cancellationToken);
            else
            {
                var tracked = await db.Set<MethodApiToken>().FindAsync([row.Id], cancellationToken);
                if (tracked is not null) { tracked.LastUsedAtUtc = now; await db.SaveChangesAsync(cancellationToken); }
            }
        }
        return actor;
    }

    private async Task<MethodActorIdentity> RequireAdminUiAsync(Guid projectId, CancellationToken ct)
    {
        var actor = context.Current;
        if (actor?.IsInteractiveHuman != true || actor.OrganizationId is null)
            throw Forbidden("Esta operação exige sessão humana interativa da UI.");
        if (!await db.Projects.AsNoTracking().AnyAsync(x => x.Id == projectId && x.OrganizationId == actor.OrganizationId && !x.IsArchived, ct))
            throw Forbidden("O projeto não está disponível na organização ativa.");
        await permissions.EnsureAsync(actor.ActorId, PlatformPermission.ManagePermissions, PermissionScope.Project, projectId, ct);
        if (!await db.Set<MethodProjectSettings>().AnyAsync(x => x.ProjectId == projectId && x.OrganizationId == actor.OrganizationId, ct))
            throw new MethodOperationException(409, "method_not_provisioned", "Provisione o método neste projeto antes de emitir tokens.");
        return actor;
    }

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
    private static MethodTokenDto Metadata(MethodApiToken token, MethodPrincipal principal, IEnumerable<PlatformPermission> ceiling)
        => new(token.Id, principal.Id, token.Name, principal.DisplayName, principal.Role.ToCode(), token.TokenPrefix,
            token.IssuedAtUtc, token.ExpiresAtUtc, token.RevokedAtUtc, token.LastUsedAtUtc, ceiling.OrderBy(x => x).ToList());
    private static MethodOperationException Invalid(string message) => new(400, "method_token_validation", message);
    private static MethodOperationException Forbidden(string message) => new(403, "method_token_forbidden", message);
}
