using System.Security.Cryptography;
using System.Text;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Detran.Kanban.Infrastructure.Services;

public sealed class RefreshTokenService : IRefreshTokenService
{
    private readonly AppDbContext _db;
    private readonly TimeSpan _lifetime;
    // Janela de tolerancia para refresh concorrente: se um token ja rotacionado
    // e reapresentado dentro deste intervalo, tratamos como corrida (varias abas
    // ou requisicoes em paralelo), nao como reuso malicioso. Assim nao derrubamos
    // a sessao inteira por causa de dois refreshes quase simultaneos.
    private readonly TimeSpan _reuseGraceWindow;
    public RefreshTokenService(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _lifetime = TimeSpan.FromDays(Math.Clamp(
            configuration.GetValue("Jwt:RefreshTokenDays", 30), 1, 90));
        _reuseGraceWindow = TimeSpan.FromSeconds(Math.Clamp(
            configuration.GetValue("Jwt:RefreshReuseGraceSeconds", 30), 0, 300));
    }

    public async Task<RefreshTokenIssue> IssueAsync(
        string userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var result = Create(userId, Guid.NewGuid(), ipAddress);
        _db.RefreshTokens.Add(result.Entity);
        await _db.SaveChangesAsync(cancellationToken);
        return result.Issue;
    }

    public async Task<RefreshTokenIssue?> RotateAsync(
        string token, string? ipAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var hash = Hash(token);
        var current = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (current is null) return null;

        var now = DateTimeOffset.UtcNow;
        if (!current.IsActive(now))
        {
            // Token ja rotacionado (tem substituto): pode ser corrida de refresh
            // concorrente ou reuso malicioso. Distinguimos pelo tempo desde a
            // revogacao.
            if (current.RevokedAt.HasValue && current.ReplacedByTokenId.HasValue)
            {
                var revokedAgo = now - current.RevokedAt.Value;
                if (revokedAgo <= _reuseGraceWindow)
                {
                    // Corrida: emite um novo token na MESMA familia, mantendo a
                    // sessao viva para o pedido concorrente (sem revogar nada).
                    var grace = Create(current.UserId, current.FamilyId, ipAddress);
                    _db.RefreshTokens.Add(grace.Entity);
                    await _db.SaveChangesAsync(cancellationToken);
                    return grace.Issue;
                }

                // Reapresentacao tardia = reuso: revoga a familia inteira.
                await RevokeFamilyByIdAsync(current.FamilyId, ipAddress, cancellationToken);
            }
            return null;
        }

        var replacement = Create(current.UserId, current.FamilyId, ipAddress);
        current.Revoke(ipAddress, replacement.Entity.Id);
        _db.RefreshTokens.Add(replacement.Entity);
        await _db.SaveChangesAsync(cancellationToken);
        return replacement.Issue;
    }

    public async Task RevokeFamilyAsync(
        string token, string? ipAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        var hash = Hash(token);
        var current = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (current is not null)
            await RevokeFamilyByIdAsync(current.FamilyId, ipAddress, cancellationToken);
    }

    public async Task RevokeAllForUserAsync(
        string userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var active = await _db.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in active) token.Revoke(ipAddress);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task RevokeFamilyByIdAsync(
        Guid familyId, string? ipAddress, CancellationToken cancellationToken)
    {
        var active = await _db.RefreshTokens
            .Where(x => x.FamilyId == familyId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in active) token.Revoke(ipAddress);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private (RefreshToken Entity, RefreshTokenIssue Issue) Create(
        string userId, Guid familyId, string? ipAddress)
    {
        var plainText = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId,
            UserId = userId,
            TokenHash = Hash(plainText),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(_lifetime),
            CreatedByIp = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress
        };
        return (entity, new RefreshTokenIssue(plainText, userId, familyId, entity.ExpiresAt));
    }

    internal static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
