using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Prisma.Workspace.Application.Features.Organizations;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Services;

namespace Prisma.Workspace.Tests;

public class PublicRegistrationSqlTests
{
    [Fact]
    public async Task FirstInvitationConsumesPendingNameInTheSameMembershipCommit()
    {
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException("Configure o SQL E2E exclusivo.");
        var connection = new SqlConnectionStringBuilder(configured);
        Assert.StartsWith("Prisma_BoardColumns_", connection.InitialCatalog);
        connection.InitialCatalog = "Prisma_BoardColumns_Oficina_Registration_" + Guid.NewGuid().ToString("N");
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connection.ConnectionString).Options);
        try
        {
            await db.Database.EnsureCreatedAsync();
            using var manager = new PublicRegistrationTests.RegistrationUserManager(db);
            var password = "Aa1!" + Guid.NewGuid().ToString("N");
            var registered = await new UserRegistrationService(db, manager)
                .RegisterAsync("Ana Alves", "ana@example.test", password, default);
            Assert.True(registered.Succeeded);
            var person = (await manager.FindByIdAsync(registered.UserId!))!;
            Assert.True((await manager.AddClaimAsync(person, new Claim("test:retained", "unchanged"))).Succeeded);
            var owner = new IdentityUser { UserName = "owner@example.test", Email = "owner@example.test" };
            Assert.True((await manager.CreateAsync(owner, password)).Succeeded);
            var organization = Organization.Create("Equipe", "equipe", owner.Id);
            db.Organizations.Add(organization);
            const string token = "own-test-invitation";
            var invitation = new OrganizationInvitation { Id = Guid.NewGuid(), OrganizationId = organization.Id,
                Organization = organization, InvitedBy = owner.Id, Email = person.Email!, Role = OrganizationRole.TeamMember,
                TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant(),
                CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) };
            db.OrganizationInvitations.Add(invitation);
            await db.SaveChangesAsync();
            var completed = await new InvitationOnboardingService(db, manager)
                .CompleteAsync(new CompleteInvitationCommand(token, password, false, null, null), default);
            db.ChangeTracker.Clear();
            Assert.Equal(person.Id, completed.UserId);
            Assert.Equal("Ana Alves", (await db.OrganizationMembers.IgnoreQueryFilters()
                .SingleAsync(member => member.UserId == person.Id)).DisplayName);
            Assert.Equal("test:retained", (await db.UserClaims.SingleAsync(claim => claim.UserId == person.Id)).ClaimType);
            Assert.NotNull((await db.OrganizationInvitations.IgnoreQueryFilters().SingleAsync()).AcceptedAt);
            Assert.True((await db.Users.SingleAsync(user => user.Id == person.Id)).EmailConfirmed);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IdentityAndPendingFullNameAreCommittedTogether(bool refuseClaim)
    {
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException("Configure o SQL E2E exclusivo.");
        var connection = new SqlConnectionStringBuilder(configured);
        Assert.StartsWith("Prisma_BoardColumns_", connection.InitialCatalog);
        connection.InitialCatalog = "Prisma_BoardColumns_Oficina_Registration_" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var db = new AppDbContext(options);
        try
        {
            await db.Database.EnsureCreatedAsync();
            using var manager = new PublicRegistrationTests.RegistrationUserManager(db, refuseClaim);
            var result = await new UserRegistrationService(db, manager).RegisterAsync("Ana Alves", "qa@example.test",
                "Aa1!" + Guid.NewGuid().ToString("N"), default);
            db.ChangeTracker.Clear();
            Assert.Equal(!refuseClaim, result.Succeeded);
            Assert.Equal(refuseClaim ? 0 : 1, await db.Users.CountAsync());
            Assert.Equal(refuseClaim ? 0 : 1, await db.UserClaims.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
