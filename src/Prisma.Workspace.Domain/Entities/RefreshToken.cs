namespace Prisma.Workspace.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public string? CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(string? ipAddress, Guid? replacementId = null)
    {
        RevokedAt ??= DateTimeOffset.UtcNow;
        RevokedByIp = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress;
        ReplacedByTokenId = replacementId;
    }
}
