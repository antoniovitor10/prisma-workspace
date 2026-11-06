using System.Security.Claims;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Prisma.Workspace.Application;
using Prisma.Workspace.Application.Features.Auth;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Identity;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Repositories;
using Prisma.Workspace.Infrastructure.Services;

namespace Prisma.Workspace.Tests;

public class PublicRegistrationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ana")]
    [InlineData(" Ana   ")]
    public async Task InvalidNameIsRejectedBeforeCreatingAnyIdentity(string name)
    {
        var registrations = new RegistrationFake();
        using var provider = new ServiceCollection().AddLogging().AddApplication()
            .AddSingleton<IUserRegistrationService>(registrations).BuildServiceProvider();
        await Assert.ThrowsAsync<ValidationException>(() => provider.GetRequiredService<IMediator>()
            .Send(new RegisterUserCommand(name, "qa@example.test", "Aa1!test-password")));
        Assert.Equal(0, registrations.Calls);
    }

    [Theory]
    [InlineData(null, "Ana Alves")]
    [InlineData("Ana Costa", "Ana Costa")]
    public async Task FullNameSurvivesRegistrationAndBecomesTheSingleFunctionalMembershipName(string? assignedName, string expected)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        using var manager = new RegistrationUserManager(db);
        var password = "Aa1!" + Guid.NewGuid().ToString("N");
        var registered = await new RegisterUserCommandHandler(new UserRegistrationService(db, manager))
            .Handle(new(" Ana Alves ", " ana@example.test ", password), default);
        Assert.True(registered.Succeeded);
        var user = await db.Users.SingleAsync();
        Assert.False(user.EmailConfirmed);
        Assert.True(await manager.CheckPasswordAsync(user, password));
        Assert.Equal("Ana Alves", (await db.UserClaims.SingleAsync()).ClaimValue);
        await manager.AddClaimAsync(user, new Claim("test:retained", "unchanged"));
        var org = Organization.Create("Organização", "qa", user.Id);
        if (assignedName is not null) org.Members.Single().UpdateDisplayName(assignedName);
        await new OrganizationRepository(db).AddAsync(org);
        Assert.Equal(expected, (await db.OrganizationMembers.IgnoreQueryFilters().SingleAsync()).DisplayName);
        Assert.Equal("test:retained", (await db.UserClaims.SingleAsync()).ClaimType);
        var tenant = new Tenant(org.Id);
        await using var scoped = new AppDbContext(options, tenant);
        var names = await new UserDirectory(scoped, tenant).GetDisplayNamesAsync([user.Id]);
        Assert.Equal(expected, names[user.Id]);
        Assert.Equal("ana@example.test", user.Email);
        Assert.DoesNotContain(password, new RegisterUserCommand("Ana Alves", user.Email!, password).ToString());
        Assert.DoesNotContain(registered.ConfirmationToken!, registered.ToString());
    }

    internal sealed class RegistrationUserManager(AppDbContext db, bool refuseClaim = false)
        : UserManager<IdentityUser>(new UserStore<IdentityUser>(db),
            Microsoft.Extensions.Options.Options.Create(new IdentityOptions { User = { RequireUniqueEmail = true } }),
            new PasswordHasher<IdentityUser>(), [new UserValidator<IdentityUser>()], [new PasswordValidator<IdentityUser>()],
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<IdentityUser>>.Instance)
    {
        public override Task<string> GenerateEmailConfirmationTokenAsync(IdentityUser user) => Task.FromResult("test-confirmation-token");
        public override Task<IdentityResult> AddClaimAsync(IdentityUser user, Claim claim)
            => refuseClaim ? Task.FromResult(IdentityResult.Failed(new IdentityError { Code = "ClaimRefused", Description = "Nome não persistido." }))
                : base.AddClaimAsync(user, claim);
    }
    private sealed class RegistrationFake : IUserRegistrationService
    {
        public int Calls { get; private set; }
        public Task<UserRegistrationResult> RegisterAsync(string fullName, string email, string password, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new UserRegistrationResult("user", "test-token", []));
        }
    }
    private sealed class Tenant(Guid id) : IOrganizationContext { public Guid? OrganizationId => id; }
}
