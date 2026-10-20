using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Features.Organizations;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Repositories;

namespace Prisma.Workspace.Tests;

public sealed class OrganizationCreationTests
{
    private static async Task<AppDbContext> SeedAsync()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"organization-creation-{Guid.NewGuid()}").Options);
        var admin = new IdentityUser { Id = "platform", UserName = "platform@example.invalid" };
        db.Users.AddRange(admin, new IdentityUser { Id = "member", UserName = "member@example.invalid" },
            new IdentityUser { Id = "newcomer", UserName = "newcomer@example.invalid" });
        db.Entry(admin).Property("IsPlatformAdministrator").CurrentValue = true;
        await db.SaveChangesAsync();
        var repository = new OrganizationRepository(db);
        var handler = new CreateOrganizationCommandHandler(repository);
        await handler.Handle(new CreateOrganizationCommand("Detran", null, "platform"), default);
        await handler.Handle(new CreateOrganizationCommand("Equipe", null, "member"), default);
        return db;
    }

    [Fact]
    public async Task FirstOrganization_RemainsOpenToAnyUser()
    {
        await using var db = await SeedAsync();
        var repository = new OrganizationRepository(db);

        Assert.True((await new GetOrganizationCreationQueryHandler(repository)
            .Handle(new GetOrganizationCreationQuery("newcomer"), default)).CanCreate);
        var created = await new CreateOrganizationCommandHandler(repository)
            .Handle(new CreateOrganizationCommand("Minha", null, "newcomer"), default);
        Assert.Equal("Minha", created.Name);
    }

    [Fact]
    public async Task AdditionalOrganization_IsRefusedForCommonMember()
    {
        await using var db = await SeedAsync();
        var repository = new OrganizationRepository(db);

        Assert.False((await new GetOrganizationCreationQueryHandler(repository)
            .Handle(new GetOrganizationCreationQuery("member"), default)).CanCreate);
        await Assert.ThrowsAsync<AcessoNegadoException>(() => new CreateOrganizationCommandHandler(repository)
            .Handle(new CreateOrganizationCommand("Outra", null, "member"), default));
        Assert.Single(await repository.GetForUserAsync("member"));
    }

    [Fact]
    public async Task AdditionalOrganization_IsAllowedForPlatformAdministrator()
    {
        await using var db = await SeedAsync();
        var repository = new OrganizationRepository(db);

        Assert.True((await new GetOrganizationCreationQueryHandler(repository)
            .Handle(new GetOrganizationCreationQuery("platform"), default)).CanCreate);
        await new CreateOrganizationCommandHandler(repository)
            .Handle(new CreateOrganizationCommand("Pessoal", null, "platform"), default);
        Assert.Equal(2, (await repository.GetForUserAsync("platform")).Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InactiveMembershipOrOrganization_DoesNotReopenFirstOrganization(bool deactivateMember)
    {
        await using var db = await SeedAsync();
        var organization = await db.Organizations.IgnoreQueryFilters()
            .Include(x => x.Members).SingleAsync(x => x.Members.Any(m => m.UserId == "member"));
        if (deactivateMember) organization.Members.Single().IsActive = false;
        else organization.IsActive = false;
        await db.SaveChangesAsync();
        var repository = new OrganizationRepository(db);

        Assert.Empty(await repository.GetForUserAsync("member"));
        Assert.False((await new GetOrganizationCreationQueryHandler(repository)
            .Handle(new GetOrganizationCreationQuery("member"), default)).CanCreate);
        await Assert.ThrowsAsync<AcessoNegadoException>(() => new CreateOrganizationCommandHandler(repository)
            .Handle(new CreateOrganizationCommand("Outra", null, "member"), default));
        Assert.Single(await db.OrganizationMembers.IgnoreQueryFilters().Where(x => x.UserId == "member").ToListAsync());
    }
}
