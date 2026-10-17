using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;
using Xunit;

namespace Prisma.Workspace.Tests;

[Collection("MethodGit SQL")]
public sealed class MethodMigrationSqlTests
{
    [MethodGitSqlFact]
    public async Task AdditiveMigrationUpDownUpPreservesLegacyColumnsAndHumanCards()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PRISMA_METHOD_GIT_SQL"));
        Assert.True(connection.DataSource is "127.0.0.1,3132" or "localhost,3132", "Migration só pode executar no SQL próprio3132.");
        connection.InitialCatalog = "Prisma_MethodGit_Migration_" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var db = new AppDbContext(options);
        try
        {
            var migrations = db.Database.GetMigrations().ToArray();
            Assert.EndsWith("Add_MethodGit_Security_And_Webhook_Tables", migrations[^1]);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(migrations[^2]);
            var before = await LegacyColumns(db);
            var human = "migration-human-" + Guid.NewGuid().ToString("N");
            var org = new Organization { Id = Guid.NewGuid(), Name = "Cópia própria", Slug = "migration-" + Guid.NewGuid().ToString("N") };
            var project = Project.Criar("MIG", "Projeto humano anterior", human, WorkNature.Project, WorkType.Development);
            project.OrganizationId = org.Id;
            var board = new Board { Id = Guid.NewGuid(), OrganizationId = org.Id, ProjectId = project.Id, OwnerId = human, Name = "Quadro humano anterior" };
            var stage = new Stage { Id = Guid.NewGuid(), BoardId = board.Id, ProjectId = project.Id, Name = "Coluna humana", Category = StageCategory.InProgress };
            var item = new WorkItem { Id = Guid.NewGuid(), BoardId = board.Id, StageId = stage.Id, Title = "Card humano preservado", Position = 123 };
            db.AddRange(new IdentityUser { Id = human, UserName = human }, org, project, board, stage, item);
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            await migrator.MigrateAsync();
            Assert.Equal(before, await LegacyColumns(db));
            Assert.Equal(11, await CountMethodTables(db));
            var stored = await db.WorkItems.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == item.Id);
            Assert.Equal(stage.Id, stored.StageId); Assert.Equal("Card humano preservado", stored.Title); Assert.Equal(123, stored.Position);
            Assert.Empty(await db.Set<MethodTaskState>().IgnoreQueryFilters().ToArrayAsync());
            Assert.Empty(await db.Set<MethodAuthorizationEvent>().IgnoreQueryFilters().ToArrayAsync());
            await migrator.MigrateAsync(migrations[^2]);
            Assert.Equal(0, await CountMethodTables(db)); Assert.Equal(before, await LegacyColumns(db));
            await migrator.MigrateAsync();
            Assert.Equal(11, await CountMethodTables(db)); Assert.Equal(before, await LegacyColumns(db));
            Assert.True(await db.WorkItems.IgnoreQueryFilters().AnyAsync(x => x.Id == item.Id && x.StageId == stage.Id));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private static async Task<int> CountMethodTables(AppDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE name IN ('MethodPrincipals','MethodProjectSettings','MethodApiTokens','MethodAuthorizationEvents','MethodStageMappings','MethodTaskStates','GitRepositoryIntegrations','GitPullRequestObservations','GitWebhookDeliveries','GitAppliedFacts','TaskGitLinks')";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
    private static async Task<string> LegacyColumns(AppDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT t.name, c.name, ty.name, c.max_length, c.is_nullable, c.is_identity, c.is_computed
            FROM sys.tables t JOIN sys.columns c ON t.object_id=c.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id
            WHERE t.name NOT IN ('__EFMigrationsHistory','MethodPrincipals','MethodProjectSettings','MethodApiTokens','MethodAuthorizationEvents','MethodStageMappings','MethodTaskStates','GitRepositoryIntegrations','GitPullRequestObservations','GitWebhookDeliveries','GitAppliedFacts','TaskGitLinks')
            ORDER BY t.name,c.column_id
            """;
        var rows = new List<object?[]>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) { var values = new object[reader.FieldCount]; reader.GetValues(values); rows.Add(values); }
        return JsonSerializer.Serialize(rows);
    }
}
