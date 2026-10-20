using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Identity;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Repositories;

namespace Prisma.Workspace.Tests;

public class ProjectAccessBatchSqlTests
{
    [Fact]
    public async Task SelectorAndAccessUseConstantQueriesAndPreserveTenantAndDeny()
    {
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException("Configure o banco SQL E2E exclusivo.");
        var connection = new SqlConnectionStringBuilder(configured);
        Assert.StartsWith("Prisma_BoardColumns_", connection.InitialCatalog);
        connection.InitialCatalog = "Prisma_BoardColumns_Oficina_Access_" + Guid.NewGuid().ToString("N");
        var count = new QueryCounter();
        var org = Guid.NewGuid(); var foreignOrg = Guid.NewGuid();
        var tenant = new ProjectAccessBatchTests.Tenant(org);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection.ConnectionString)
            .AddInterceptors(count).Options;
        await using var db = new AppDbContext(options, tenant);
        try
        {
            await db.Database.EnsureCreatedAsync();
            db.Organizations.AddRange(new Organization { Id = org, Name = "Oficina", Slug = "oficina" },
                new Organization { Id = foreignOrg, Name = "Outro tenant", Slug = "outro" });
            db.OrganizationMembers.Add(OrganizationMember.Create(org, "actor", OrganizationRole.TeamMember));
            var ids = new List<Guid>();
            for (var i = 0; i < 24; i++)
            {
                var project = Project.Criar("P" + i, "Oficina " + i, "actor", WorkNature.Project, WorkType.Development);
                project.OrganizationId = org; project.IsArchived = i == 23;
                db.Projects.Add(project); ids.Add(project.Id);
                db.Boards.Add(new Board { Id = Guid.NewGuid(), OrganizationId = org,
                    ProjectId = project.Id, Project = project, OwnerId = "actor", Name = "Quadro " + i, CreatedAt = DateTimeOffset.UtcNow });
            }
            var foreign = Project.Criar("FOR", "Outro tenant", "actor", WorkNature.Project, WorkType.Development);
            foreign.OrganizationId = foreignOrg;
            await using (var foreignDb = new AppDbContext(options, new ProjectAccessBatchTests.Tenant(foreignOrg)))
            {
                // Cada tenant grava por seu próprio contexto, sem desativar a barreira de escrita.
                foreignDb.Projects.Add(foreign);
                foreignDb.Boards.Add(new Board { Id = Guid.NewGuid(), OrganizationId = foreignOrg,
                    ProjectId = foreign.Id, Project = foreign, OwnerId = "actor", Name = "Outro tenant", CreatedAt = DateTimeOffset.UtcNow });
                // A organização referenciada precisa existir antes deste contexto gravar o projeto.
                await db.SaveChangesAsync();
                await foreignDb.SaveChangesAsync();
            }
            db.PermissionGrants.Add(new PermissionGrant { Id = Guid.NewGuid(), OrganizationId = org,
                UserId = "actor", Permission = PlatformPermission.View, Scope = PermissionScope.Project,
                ScopeId = ids[1], IsAllowed = false, GrantedBy = "actor" });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var permissions = new PermissionService(db, tenant);
            var access = new ProjectAccessService(db, permissions);
            count.Readers = 0;
            Assert.Single(await access.GetAccessibleProjectIdsAsync([ids[0]], "actor"));
            var one = count.Readers;
            count.Readers = 0;
            var many = await access.GetAccessibleProjectIdsAsync(ids.Append(foreign.Id), "actor");
            var all = count.Readers;
            Assert.Equal(23, many.Count);
            Assert.DoesNotContain(ids[1], many);
            Assert.DoesNotContain(foreign.Id, many);
            Assert.Equal(one, all);
            Assert.InRange(all, 1, 5);
            count.Readers = 0;
            var boards = await new BoardRepository(db).GetAllAsync();
            Assert.Equal(23, boards.Count);
            Assert.DoesNotContain(boards, board => board.ProjectId == ids[23] || board.ProjectId == foreign.Id);
            Assert.Equal(1, count.Readers);
        }
        finally
        {
            // Banco novo com GUID próprio; não remove o banco configurado nem dados existentes.
            await db.Database.EnsureDeletedAsync();
        }
    }

    private sealed class QueryCounter : DbCommandInterceptor
    {
        public int Readers { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Readers++;
            return ValueTask.FromResult(result);
        }
    }
}
