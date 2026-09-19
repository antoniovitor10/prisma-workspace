using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.Organizations;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Services;

namespace Prisma.Workspace.Tests;

public class InvitationOnboardingTests
{
    [Theory]
    [InlineData("expired")]
    [InlineData("canceled")]
    [InlineData("consumed")]
    [InlineData("inactive")]
    [InlineData("missing")]
    public async Task Preview_RejectsUnavailableInvitation(string state)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var org = Organization.Create("Equipe", "equipe", "admin");
        org.IsActive = state != "inactive";
        db.Organizations.Add(org);
        if (state != "missing") db.OrganizationInvitations.Add(new OrganizationInvitation {
            Id = Guid.NewGuid(), OrganizationId = org.Id, Organization = org,
            Email = "invite@example.test", InvitedBy = "admin",
            TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("test-token"))).ToLowerInvariant(),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(state == "expired" ? -1 : 1),
            CanceledAt = state == "canceled" ? DateTimeOffset.UtcNow : null,
            AcceptedAt = state == "consumed" ? DateTimeOffset.UtcNow : null
        });
        await db.SaveChangesAsync();
        var service = new InvitationOnboardingService(db, null!);
        await Assert.ThrowsAsync<DomainException>(() => service.PreviewAsync("test-token", default));
    }

    [Theory]
    [InlineData(null, "same", "Informe seu nome e sobrenome.")]
    [InlineData("Vitor", "same", "Informe seu nome e sobrenome.")]
    [InlineData("Vitor Teste", "different", "As senhas não coincidem.")]
    public async Task Complete_ValidatesBeforeWriting(string? name, string confirmation, string message)
    {
        var service = new InvitationOnboardingService(null!, null!);
        var error = await Assert.ThrowsAsync<DomainException>(() => service.CompleteAsync(
            new CompleteInvitationCommand("token", "same", true, name, confirmation), default));
        Assert.Equal(message, error.Message);
    }
}
