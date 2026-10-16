using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Identity;
using Prisma.Workspace.Infrastructure.Identity.MethodTokens;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Tenancy;

namespace Prisma.Workspace.Tests;

public sealed class MethodTokenTests
{
    [Fact]
    public async Task Issuance_StoresOnlyHashAndUsesIndependentStableActor()
    {
        await using var f = await Fixture.CreateAsync();
        var issued = await f.Service().IssueAsync(f.ProjectId, new("Executor", "agente"));
        Assert.True(MethodTokenFormat.IsWellFormed(issued.Token));
        Assert.Equal(47, issued.Token.Length);
        var row = await f.Db.Set<MethodApiToken>().SingleAsync();
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(issued.Token)), row.TokenHash);
        Assert.DoesNotContain(issued.Token, JsonSerializer.Serialize(row));
        Assert.Equal(30, (row.ExpiresAtUtc - row.IssuedAtUtc).TotalDays);
        var actor = await f.Service().AuthenticateAsync(issued.Token, "request-1");
        Assert.NotNull(actor);
        Assert.Equal($"method:{issued.Metadata.PrincipalId:D}", actor.ActorId);
        Assert.NotEqual(Fixture.HumanA, actor.ActorId);
        Assert.Equal(MethodAuthenticationKind.MethodToken, actor.AuthenticationKind);
        Assert.Equal("Executor", actor.DisplayName);
        Assert.Equal(f.ProjectId, actor.ProjectId);
        Assert.DoesNotContain(PlatformPermission.ManagePermissions, actor.PermissionCeiling);
        var metadata = JsonSerializer.Serialize(await f.Service().ListAsync(f.ProjectId));
        Assert.DoesNotContain(issued.Token, metadata);
        Assert.DoesNotContain("TokenHash", metadata);
    }

    [Fact]
    public async Task HumanPrincipal_IsAlwaysTheAuthenticatedIssuer()
    {
        await using var f = await Fixture.CreateAsync();
        var issued = await f.Service().IssueAsync(f.ProjectId, new("Pessoal", "humano"));
        var principal = await f.Db.Set<MethodPrincipal>().SingleAsync();
        Assert.Equal(Fixture.HumanA, principal.HumanUserId);
        Assert.Equal(Fixture.HumanA, principal.DelegatedByUserId);
        var actor = await f.Service().AuthenticateAsync(issued.Token, "human-token");
        Assert.NotNull(actor);
        Assert.Equal(Fixture.HumanA, actor.ActorId);
        Assert.False(actor.IsInteractiveHuman);
    }

    [Fact]
    public async Task RotationOfAnotherHumansPrincipal_Is403AndCreatesNothing()
    {
        await using var f = await Fixture.CreateAsync();
        var issued = await f.Service().IssueAsync(f.ProjectId, new("Pessoal", "humano"));
        var error = await Assert.ThrowsAsync<MethodOperationException>(() => f.Service(Fixture.HumanB)
            .IssueAsync(f.ProjectId, new("Personificação", "humano", PrincipalId: issued.Metadata.PrincipalId)));
        Assert.Equal(403, error.StatusCode);
        Assert.Single(await f.Db.Set<MethodApiToken>().ToListAsync());
        Assert.Single(await f.Db.Set<MethodPrincipal>().ToListAsync());
    }

    [Fact]
    public async Task RotationOfPrincipalFromAnotherProject_Is403()
    {
        await using var f = await Fixture.CreateAsync();
        var issued = await f.Service().IssueAsync(f.ProjectId, new("Executor", "agente"));
        var otherProject = await f.AddProjectAsync();
        var error = await Assert.ThrowsAsync<MethodOperationException>(() => f.Service()
            .IssueAsync(otherProject, new("Outro projeto", "agente", PrincipalId: issued.Metadata.PrincipalId)));
        Assert.Equal(403, error.StatusCode);
        Assert.Single(await f.Db.Set<MethodApiToken>().ToListAsync());
    }

    [Fact]
    public async Task Rotation_PreservesPrincipalAndNarrowsCeilingToNewIssuersCurrentPermissions()
    {
        await using var f = await Fixture.CreateAsync();
        var old = await f.Service().IssueAsync(f.ProjectId, new("Executor", "agente"));
        Assert.Contains(PlatformPermission.Edit, old.Metadata.Permissions);
        await f.AddLimitedIssuerAsync();
        var rotated = await f.Service(Fixture.LimitedHuman).IssueAsync(f.ProjectId,
            new("Rotação", "agente", PrincipalId: old.Metadata.PrincipalId));
        Assert.Equal(old.Metadata.PrincipalId, rotated.Metadata.PrincipalId);
        Assert.NotEqual(old.Token, rotated.Token);
        Assert.DoesNotContain(PlatformPermission.Edit, rotated.Metadata.Permissions);
        Assert.All(rotated.Metadata.Permissions, p => Assert.Contains(p, old.Metadata.Permissions));
        var oldActor = await f.Service().AuthenticateAsync(old.Token, "old-after-rotation");
        var newActor = await f.Service().AuthenticateAsync(rotated.Token, "new-after-rotation");
        Assert.NotNull(oldActor);
        Assert.NotNull(newActor);
        Assert.Equal(oldActor.ActorId, newActor.ActorId);
        Assert.DoesNotContain(PlatformPermission.Edit, oldActor.PermissionCeiling);
        Assert.False(await f.Evaluator.HasAsync(newActor, PlatformPermission.Edit, PermissionScope.Project, f.ProjectId));
    }

    [Theory]
    [InlineData("humano", null)]
    [InlineData("agente", "Nome diferente")]
    public async Task Rotation_CannotChangeRoleOrPrincipalName(string role, string? displayName)
    {
        await using var f = await Fixture.CreateAsync();
        var old = await f.Service().IssueAsync(f.ProjectId, new("Executor", "agente"));
        var error = await Assert.ThrowsAsync<MethodOperationException>(() => f.Service().IssueAsync(f.ProjectId,
            new("Rotação", role, PrincipalId: old.Metadata.PrincipalId, PrincipalDisplayName: displayName)));
        Assert.Equal(400, error.StatusCode);
        Assert.Single(await f.Db.Set<MethodApiToken>().ToListAsync());
    }

    [Fact]
    public async Task Rotation_CannotWidenPermissionsOrReuseInactivePrincipal()
    {
        await using var f = await Fixture.CreateAsync();
        var old = await f.Service().IssueAsync(f.ProjectId,
            new("Executor", "agente", Permissions: [PlatformPermission.View]));
        var wider = await Assert.ThrowsAsync<MethodOperationException>(() => f.Service().IssueAsync(f.ProjectId,
            new("Ampliação", "agente", PrincipalId: old.Metadata.PrincipalId,
                Permissions: [PlatformPermission.View, PlatformPermission.Edit])));
        Assert.Equal(403, wider.StatusCode);
        var principal = await f.Db.Set<MethodPrincipal>().SingleAsync();
        principal.IsActive = false;
        await f.Db.SaveChangesAsync();
        var inactive = await Assert.ThrowsAsync<MethodOperationException>(() => f.Service().IssueAsync(f.ProjectId,
            new("Inativo", "agente", PrincipalId: principal.Id)));
        Assert.Equal(403, inactive.StatusCode);
        Assert.Null(await f.Service().AuthenticateAsync(old.Token, "inactive"));
    }

    [Theory]
    [InlineData("agente")]
    [InlineData("humano")]
    [InlineData("sistema")]
    public async Task NoMethodTokenCanIssueListOrRevokeTokens(string role)
    {
        await using var f = await Fixture.CreateAsync();
        var issued = await f.Service().IssueAsync(f.ProjectId, new("Método", role));
        var actor = await f.Service().AuthenticateAsync(issued.Token, "method" );
        Assert.NotNull(actor);
        var service = f.ServiceFor(actor);
        var issue = await Assert.ThrowsAsync<MethodOperationException>(() => service.IssueAsync(f.ProjectId, new("Outro", "humano")));
        var list = await Assert.ThrowsAsync<MethodOperationException>(() => service.ListAsync(f.ProjectId));
        var revoke = await Assert.ThrowsAsync<MethodOperationException>(() => service.RevokeAsync(f.ProjectId, issued.Metadata.Id));
        Assert.Equal(403, issue.StatusCode);
        Assert.Equal(403, list.StatusCode);
        Assert.Equal(403, revoke.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(91)]
    public async Task ValidityOutsideOneToNinetyDays_Is400(int days)
    {
        await using var f = await Fixture.CreateAsync();
        var error = await Assert.ThrowsAsync<MethodOperationException>(() => f.Service().IssueAsync(f.ProjectId,
            new("Inválido", "agente", days)));
        Assert.Equal(400, error.StatusCode);
        Assert.Empty(await f.Db.Set<MethodApiToken>().ToListAsync());
    }

    [Fact]
    public async Task ExpirationRevocationAndLastUsedWindow_AreIndependent()
    {
        await using var f = await Fixture.CreateAsync();
        var issued = await f.Service().IssueAsync(f.ProjectId, new("Executor", "agente", 90));
        Assert.NotNull(await f.Service().AuthenticateAsync(issued.Token, "first"));
        var firstUsed = (await f.Db.Set<MethodApiToken>().AsNoTracking().SingleAsync()).LastUsedAtUtc;
        f.Clock.Advance(TimeSpan.FromMinutes(4));
        Assert.NotNull(await f.Service().AuthenticateAsync(issued.Token, "four-minutes"));
        Assert.Equal(firstUsed, (await f.Db.Set<MethodApiToken>().AsNoTracking().SingleAsync()).LastUsedAtUtc);
        f.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.NotNull(await f.Service().AuthenticateAsync(issued.Token, "five-minutes"));
        Assert.Equal(f.Clock.GetUtcNow(), (await f.Db.Set<MethodApiToken>().AsNoTracking().SingleAsync()).LastUsedAtUtc);
        await f.Service().RevokeAsync(f.ProjectId, issued.Metadata.Id);
        Assert.Null(await f.Service().AuthenticateAsync(issued.Token, "revoked-before-window"));
        var other = await f.Service().IssueAsync(f.ProjectId, new("Expira", "agente", 1));
        f.Clock.Advance(TimeSpan.FromDays(1));
        Assert.Null(await f.Service().AuthenticateAsync(other.Token, "at-expiration"));
    }

    [Fact]
    public async Task DelegationLossAndCrossProjectAccess_AreDeniedWithoutBorrowingAdmin()
    {
        await using var f = await Fixture.CreateAsync();
        var issued = await f.Service().IssueAsync(f.ProjectId, new("Executor", "agente"));
        var actor = (await f.Service().AuthenticateAsync(issued.Token, "scope"))!;
        Assert.True(await f.Evaluator.HasAsync(actor, PlatformPermission.Edit, PermissionScope.Project, f.ProjectId));
        Assert.False(await f.Evaluator.HasAsync(actor, PlatformPermission.ManagePermissions, PermissionScope.Project, f.ProjectId));
        Assert.False(await f.Evaluator.HasAsync(actor, PlatformPermission.Create, PermissionScope.Organization, f.OrgId));
        Assert.False(await f.Evaluator.HasAsync(actor, PlatformPermission.View, PermissionScope.Project, Guid.NewGuid()));
        var member = await f.Db.OrganizationMembers.SingleAsync(x => x.UserId == Fixture.HumanA);
        member.IsActive = false;
        await f.Db.SaveChangesAsync();
        Assert.Null(await f.Service().AuthenticateAsync(issued.Token, "delegation-removed"));
        Assert.False(await f.Evaluator.HasAsync(actor, PlatformPermission.Edit, PermissionScope.Project, f.ProjectId));
    }

    [Fact]
    public async Task PausedProjectSuspendsMethodTokenWithoutChangingHumanPermissions()
    {
        await using var f = await Fixture.CreateAsync();
        var issued = await f.Service().IssueAsync(f.ProjectId, new("Executor", "agente"));
        var project = await f.Db.Projects.SingleAsync(x => x.Id == f.ProjectId);
        project.Status = ProjectStatus.OnHold;
        await f.Db.SaveChangesAsync();
        Assert.Null(await f.Service().AuthenticateAsync(issued.Token, "paused-project"));
        // O serviço existente de permissões humanas não foi alterado pela política do método.
        Assert.NotEmpty(await f.Service().ListAsync(f.ProjectId));
    }

    [Theory]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.payload.signature")]
    [InlineData("pmt_not-a-valid-token")]
    [InlineData("Bearer pmt_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task LoginJwtAndMalformedCredentials_AreNeverMethodIdentities(string raw)
    {
        await using var f = await Fixture.CreateAsync();
        Assert.Null(await f.Service().AuthenticateAsync(raw, "invalid"));
    }

    [Fact]
    public void ContextIdentity_CannotBeReplacedOrMutatedViaOriginalSet()
    {
        var permissions = new HashSet<PlatformPermission> { PlatformPermission.View };
        var actor = new MethodActorIdentity(MethodAuthenticationKind.MethodToken, MethodRole.Agent, Guid.NewGuid(),
            "method:actor", "Executor", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, permissions, MethodChannels.Mcp, "test");
        var context = new MethodAccessContext();
        context.Initialize(actor);
        permissions.Add(PlatformPermission.ManagePermissions);
        Assert.DoesNotContain(PlatformPermission.ManagePermissions, context.Current!.PermissionCeiling);
        Assert.Throws<InvalidOperationException>(() => context.Initialize(actor with { AuthenticationKind = MethodAuthenticationKind.UiSession }));
    }

    internal sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 16, 7, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan value) => _now += value;
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        private static readonly InMemoryDatabaseRoot SharedDatabaseRoot = new();
        internal const string HumanA = "human-a";
        internal const string HumanB = "human-b";
        internal const string LimitedHuman = "limited-human";
        internal Guid OrgId { get; } = Guid.NewGuid();
        internal Guid ProjectId { get; private set; }
        internal AppDbContext Db { get; }
        internal DbContextOptions<AppDbContext> Options { get; }
        internal TestClock Clock { get; } = new();
        internal MethodPermissionEvaluator Evaluator { get; }
        private readonly CurrentOrganizationContext _organization = new();

        private Fixture()
        {
            _organization.Set(OrgId);
            Options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"method-token-{Guid.NewGuid()}", SharedDatabaseRoot).Options;
            Db = new AppDbContext(Options, _organization);
            Evaluator = new MethodPermissionEvaluator(Db, Clock);
        }

        internal static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture();
            await f.Db.Database.EnsureCreatedAsync();
            f.Db.Organizations.Add(new Organization { Id = f.OrgId, Name = "Org método", Slug = "method-test" });
            foreach (var userId in new[] { HumanA, HumanB })
            {
                f.Db.Users.Add(new IdentityUser { Id = userId, UserName = userId, EmailConfirmed = true });
                f.Db.OrganizationMembers.Add(new OrganizationMember
                { OrganizationId = f.OrgId, UserId = userId, Role = OrganizationRole.Administrator, IsActive = true });
            }
            await f.Db.SaveChangesAsync();
            f.ProjectId = await f.AddProjectAsync();
            return f;
        }

        internal async Task<Guid> AddProjectAsync()
        {
            var projectId = Guid.NewGuid();
            Db.Projects.Add(new Project { Id = projectId, OrganizationId = OrgId, OwnerId = HumanA, Name = "Método", Key = "M" });
            Db.Set<MethodProjectSettings>().Add(new MethodProjectSettings
            { ProjectId = projectId, OrganizationId = OrgId, BoardId = Guid.NewGuid(), Enabled = true });
            await Db.SaveChangesAsync();
            return projectId;
        }

        internal async Task AddLimitedIssuerAsync()
        {
            Db.Users.Add(new IdentityUser { Id = LimitedHuman, UserName = LimitedHuman, EmailConfirmed = true });
            Db.OrganizationMembers.Add(new OrganizationMember
            { OrganizationId = OrgId, UserId = LimitedHuman, Role = OrganizationRole.Developer, IsActive = true });
            Db.ProjectMembers.Add(new ProjectMember { ProjectId = ProjectId, UserId = LimitedHuman, Role = ProjectRole.Member });
            Db.PermissionGrants.AddRange(
                new PermissionGrant { Id = Guid.NewGuid(), OrganizationId = OrgId, UserId = LimitedHuman,
                    Scope = PermissionScope.Project, ScopeId = ProjectId, Permission = PlatformPermission.ManagePermissions, IsAllowed = true },
                new PermissionGrant { Id = Guid.NewGuid(), OrganizationId = OrgId, UserId = LimitedHuman,
                    Scope = PermissionScope.Project, ScopeId = ProjectId, Permission = PlatformPermission.Edit, IsAllowed = false });
            await Db.SaveChangesAsync();
        }

        internal MethodTokenService Service(string userId = HumanA) => ServiceFor(new MethodActorIdentity(
            MethodAuthenticationKind.UiSession, MethodRole.Human, null, userId, userId, OrgId, null, null, null,
            Enum.GetValues<PlatformPermission>().ToHashSet(), MethodChannels.Ui, "ui-test"));

        internal MethodTokenService ServiceFor(MethodActorIdentity actor)
        {
            var context = new MethodAccessContext();
            context.Initialize(actor);
            return new MethodTokenService(Db, context, new PermissionService(Db, _organization), Evaluator, Clock);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
