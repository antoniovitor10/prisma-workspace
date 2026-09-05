using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Infrastructure.Identity;
using Detran.Kanban.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Detran.Kanban.Tests;

public class UserDirectoryTests
{
    private sealed class OrganizationContext(Guid? organizationId) : IOrganizationContext
    {
        public Guid? OrganizationId { get; } = organizationId;
    }

    [Fact]
    public async Task DisplayName_IsResolvedPerOrganization_WithoutLeakingAnotherTenant()
    {
        var database = $"user-directory-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(database)
            .Options;
        var organizationA = Guid.NewGuid();
        var organizationB = Guid.NewGuid();

        await using (var seed = new AppDbContext(options))
        {
            seed.Users.AddRange(
                NewUser("user-1", "login.legado", "legacy@detran.se.gov.br"),
                NewUser("user-2", "inativo.legado", "inactive@detran.se.gov.br"));
            seed.Organizations.AddRange(NewOrganization(organizationA, "a"), NewOrganization(organizationB, "b"));
            seed.OrganizationMembers.AddRange(
                NewMember(organizationA, "user-1", "Maria A"),
                NewMember(organizationB, "user-1", "Maria B"),
                NewMember(organizationA, "user-2", "Pessoa Inativa", isActive: false));
            await seed.SaveChangesAsync();
        }

        await using var contextA = new AppDbContext(options, new OrganizationContext(organizationA));
        var directoryA = new UserDirectory(contextA, new OrganizationContext(organizationA));
        var namesA = await directoryA.GetDisplayNamesAsync(["user-1"]);
        var summaryA = Assert.Single(await directoryA.GetAllAsync());

        Assert.Equal("Maria A", namesA["user-1"]);
        Assert.Equal("Maria A", summaryA.DisplayName);
        Assert.Equal("legacy@detran.se.gov.br", summaryA.Email);
        Assert.Empty(await directoryA.GetByIdsAsync(["user-2"]));
        var inactive = Assert.Single(await directoryA.GetByIdsAsync(["user-2"], includeInactive: true));
        Assert.Equal("Pessoa Inativa", inactive.DisplayName);
        Assert.Equal("inactive@detran.se.gov.br", inactive.Email);

        await using var contextB = new AppDbContext(options, new OrganizationContext(organizationB));
        var directoryB = new UserDirectory(contextB, new OrganizationContext(organizationB));
        var namesB = await directoryB.GetDisplayNamesAsync(["user-1"]);

        Assert.Equal("Maria B", namesB["user-1"]);
    }

    [Fact]
    public void Resolve_NeverUsesEmailAsThePrimaryLabel()
    {
        Assert.Equal("Nome configurado", UserDisplayName.Resolve("user-1", " Nome configurado ", "login", "mail@test"));
        Assert.Equal("login", UserDisplayName.Resolve("user-1", null, "login", "mail@test"));
        Assert.Equal("Usuário user-1", UserDisplayName.Resolve("user-1", null, null, "mail@test.com"));
        Assert.Equal("Usuário abcdefgh", UserDisplayName.Resolve(
            "abcdefgh-1234", null, "mail@test.com", "mail@test.com"));
        Assert.Equal("Usuário abcdefgh", UserDisplayName.Resolve("abcdefgh-1234", null, null, null));
    }

    private static IdentityUser NewUser(string id, string userName, string email) => new()
    {
        Id = id,
        UserName = userName,
        NormalizedUserName = userName.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant()
    };

    private static Organization NewOrganization(Guid id, string slug) => new()
    {
        Id = id,
        Name = slug.ToUpperInvariant(),
        Slug = slug,
        IsActive = true,
        Locale = "pt-BR",
        TimeZone = "America/Sao_Paulo",
        WeekStartDay = DayOfWeek.Monday,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static OrganizationMember NewMember(
        Guid organizationId, string userId, string displayName, bool isActive = true)
    {
        var member = OrganizationMember.Create(organizationId, userId, OrganizationRole.TeamMember);
        member.UpdateDisplayName(displayName);
        if (!isActive) member.Configure(member.Role, false);
        return member;
    }
}
