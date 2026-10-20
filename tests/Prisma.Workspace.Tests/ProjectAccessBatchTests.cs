using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Identity;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Repositories;

namespace Prisma.Workspace.Tests;

public class ProjectAccessBatchTests
{
    [Theory]
    [InlineData(OrganizationRole.Administrator)]
    [InlineData(OrganizationRole.Manager)]
    [InlineData(OrganizationRole.ProjectManager)]
    [InlineData(OrganizationRole.TeamMember)]
    [InlineData(OrganizationRole.Viewer)]
    [InlineData(OrganizationRole.Client)]
    [InlineData(OrganizationRole.ExternalRequester)]
    public async Task BatchMatchesIndividualAccessIncludingDenyAndMembership(OrganizationRole role)
    {
        await using var f = await Fixture.CreateAsync(role);
        var ids = f.Projects.Select(project => project.Id).Append(Guid.Empty).Append(Guid.NewGuid()).ToArray();
        var service = new ProjectAccessService(f.Db, f.Permissions);
        var expected = new HashSet<Guid>();
        foreach (var id in ids)
            if (await service.GetRoleAsync(id, "actor") is not null) expected.Add(id);
        Assert.True(expected.SetEquals(await service.GetAccessibleProjectIdsAsync(ids.Concat(ids), "actor")));
        Assert.DoesNotContain(f.Projects[1].Id, expected);
        if (role is not (OrganizationRole.Administrator or OrganizationRole.Manager or OrganizationRole.ProjectManager))
            Assert.DoesNotContain(f.Projects[2].Id, expected);
    }

    [Fact]
    public async Task PermissionBatchMatchesSingleChecksForEveryRoleAndPermission()
    {
        foreach (var role in Enum.GetValues<OrganizationRole>())
        {
            await using var f = await Fixture.CreateAsync(role);
            var ids = f.Projects.Select(project => project.Id).ToArray();
            foreach (var permission in Enum.GetValues<PlatformPermission>())
            {
                var expected = new HashSet<Guid>();
                foreach (var id in ids)
                    if (await f.Permissions.HasAsync("actor", permission, PermissionScope.Project, id)) expected.Add(id);
                Assert.True(expected.SetEquals(await f.Permissions.GetAllowedProjectIdsAsync("actor", permission, ids)),
                    $"Permissão em lote divergiu: {role}/{permission}.");
            }
        }
    }

    [Fact]
    public async Task OrganizationDenyWinsOverProjectAllowAndInactiveMembership()
    {
        await using var f = await Fixture.CreateAsync(OrganizationRole.TeamMember);
        f.Db.PermissionGrants.Add(new PermissionGrant { Id = Guid.NewGuid(), OrganizationId = f.Org,
            UserId = "actor", Permission = PlatformPermission.View, Scope = PermissionScope.Organization, IsAllowed = false });
        f.Db.PermissionGrants.Add(new PermissionGrant { Id = Guid.NewGuid(), OrganizationId = f.Org,
            UserId = "actor", Permission = PlatformPermission.View, Scope = PermissionScope.Project,
            ScopeId = f.Projects[0].Id, IsAllowed = true });
        await f.Db.SaveChangesAsync();
        var ids = f.Projects.Select(project => project.Id).ToArray();
        Assert.Empty(await new ProjectAccessService(f.Db, f.Permissions).GetAccessibleProjectIdsAsync(ids, "actor"));
        f.Db.PermissionGrants.RemoveRange(f.Db.PermissionGrants);
        (await f.Db.OrganizationMembers.SingleAsync()).IsActive = false;
        await f.Db.SaveChangesAsync();
        Assert.Empty(await new ProjectAccessService(f.Db, f.Permissions).GetAccessibleProjectIdsAsync(ids, "actor"));
    }

    [Fact]
    public async Task GlobalAdministratorKeepsExistingAccessAndEmptyBatchIsEmpty()
    {
        await using var f = await Fixture.CreateAsync(OrganizationRole.TeamMember);
        f.Db.Roles.Add(new IdentityRole { Id = "global", Name = "GlobalAdmin" });
        f.Db.UserRoles.Add(new IdentityUserRole<string> { UserId = "actor", RoleId = "global" });
        await f.Db.SaveChangesAsync();
        var service = new ProjectAccessService(f.Db, f.Permissions);
        var ids = f.Projects.Select(project => project.Id).ToHashSet();
        Assert.True(ids.SetEquals(await service.GetAccessibleProjectIdsAsync(ids, "actor")));
        Assert.Empty(await service.GetAccessibleProjectIdsAsync([], "actor"));
    }

    [Fact]
    public async Task BoardSelectorOmitsArchivedProjectsAndRestoresWithoutDeletingBoards()
    {
        await using var f = await Fixture.CreateAsync(OrganizationRole.Administrator);
        var project = f.Projects[0];
        var board = new Board { Id = Guid.NewGuid(), OrganizationId = f.Org, ProjectId = project.Id,
            Project = project, OwnerId = "actor", Name = "Quadro preservado", CreatedAt = DateTimeOffset.UtcNow };
        f.Db.Boards.Add(board);
        await f.Db.SaveChangesAsync();
        var repository = new BoardRepository(f.Db);
        Assert.Equal(board.Id, Assert.Single(await repository.GetAllAsync()).Id);
        project.IsArchived = true;
        await f.Db.SaveChangesAsync();
        Assert.Empty(await repository.GetAllAsync());
        Assert.Equal(board.Id, (await repository.GetByIdAsync(board.Id))!.Id);
        project.IsArchived = false;
        await f.Db.SaveChangesAsync();
        Assert.Equal(board.Id, Assert.Single(await repository.GetAllAsync()).Id);
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        public Guid Org { get; } = Guid.NewGuid();
        public AppDbContext Db { get; private set; } = null!;
        public PermissionService Permissions => new(Db, new Tenant(Org));
        public List<Project> Projects { get; } = [];
        public static async Task<Fixture> CreateAsync(OrganizationRole role)
        {
            var f = new Fixture();
            f.Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new Tenant(f.Org));
            f.Db.Organizations.Add(new Organization { Id = f.Org, Name = "Oficina", Slug = f.Org.ToString("N") });
            f.Db.OrganizationMembers.Add(OrganizationMember.Create(f.Org, "actor", role));
            for (var i = 0; i < 6; i++)
            {
                var project = Project.Criar("P" + i, "Projeto " + i, i == 0 ? "actor" : "other", WorkNature.Project, WorkType.Development);
                project.OrganizationId = f.Org;
                if (i is 1 or 3) project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = "actor", Role = ProjectRole.Member });
                if (i is 4 or 5)
                {
                    var team = Team.Criar("Equipe " + i); team.OrganizationId = f.Org;
                    team.AdicionarMembro("actor", null); team.IsActive = i == 4;
                    project.Teams.Add(new ProjectTeam { ProjectId = project.Id, TeamId = team.Id, Team = team });
                }
                f.Projects.Add(project); f.Db.Projects.Add(project);
            }
            // A negação deve vencer até para administrador da organização.
            f.Db.PermissionGrants.Add(new PermissionGrant { Id = Guid.NewGuid(), OrganizationId = f.Org,
                UserId = "actor", Permission = PlatformPermission.View, Scope = PermissionScope.Project,
                ScopeId = f.Projects[1].Id, IsAllowed = false });
            // Sem vínculo: nem permissão concedida abre acesso implicitamente para membro comum.
            await f.Db.SaveChangesAsync();
            return f;
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
    internal sealed class Tenant(Guid id) : IOrganizationContext { public Guid? OrganizationId => id; }
}
