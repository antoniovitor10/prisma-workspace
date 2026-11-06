using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Infrastructure.Persistence;

namespace Prisma.Workspace.Infrastructure.Services;

internal static class PendingRegistrationName
{
    internal const string ClaimType = "prisma:pending-full-name";

    // Copia e retira a identificação provisória no mesmo SaveChanges da associação.
    internal static async Task ApplyAsync(AppDbContext db, IEnumerable<OrganizationMember> members, CancellationToken ct)
    {
        var targets = members.ToList();
        var ids = targets.Select(member => member.UserId).Distinct().ToArray();
        if (ids.Length == 0) return;
        var claims = await db.UserClaims.Where(claim => ids.Contains(claim.UserId) && claim.ClaimType == ClaimType).ToListAsync(ct);
        foreach (var member in targets)
        {
            var name = claims.FirstOrDefault(claim => claim.UserId == member.UserId)?.ClaimValue;
            if (string.IsNullOrWhiteSpace(member.DisplayName) && !string.IsNullOrWhiteSpace(name)) member.UpdateDisplayName(name);
        }
        db.UserClaims.RemoveRange(claims);
    }
}
