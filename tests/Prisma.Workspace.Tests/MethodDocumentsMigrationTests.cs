using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Persistence.Migrations;

namespace Prisma.Workspace.Tests;

/// <summary>HU-0008, T-0020: a migração Add_MethodDocuments só cria as duas tabelas e os índices (Up/Down/Up).</summary>
public class MethodDocumentsMigrationTests
{
    private const string Previous = "20261106120319_Add_MethodGit_BranchLinks_And_Polling";

    [Fact]
    public void Migration_OnlyCreatesTheTwoTablesAndDownDropsThem()
    {
        var migration = new Add_MethodDocuments();
        Assert.NotNull(migration.TargetModel.FindEntityType("Prisma.Workspace.Domain.Entities.MethodDocument"));
        Assert.NotNull(migration.TargetModel.FindEntityType("Prisma.Workspace.Domain.Entities.MethodDocumentRevision"));
        var up = migration.UpOperations;
        Assert.All(up, op => Assert.True(op is Microsoft.EntityFrameworkCore.Migrations.Operations.CreateTableOperation
            or Microsoft.EntityFrameworkCore.Migrations.Operations.CreateIndexOperation, op.GetType().Name));
        Assert.Equal(["MethodDocuments", "MethodDocumentRevisions"], up.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.CreateTableOperation>().Select(x => x.Name));
        Assert.All(migration.DownOperations, op => Assert.IsType<Microsoft.EntityFrameworkCore.Migrations.Operations.DropTableOperation>(op));
    }

    [Fact]
    public async Task Sql_UpDownUp_CreatesAndRemovesOnlyTheNewTables()
    {
        var value = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(value)) return; // opt-in, só em SQL Server descartável
        var builder = new SqlConnectionStringBuilder(value) { InitialCatalog = "DetranKanban_MigrationTest_" + Guid.NewGuid().ToString("N") };
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(builder.ConnectionString).Options);
        try
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(Previous);
            Assert.Equal(0, await TablesAsync(context));
            await migrator.MigrateAsync();
            Assert.Equal(2, await TablesAsync(context));
            await migrator.MigrateAsync(Previous);
            Assert.Equal(0, await TablesAsync(context));
            Assert.True(await context.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.tables WHERE name = 'WorkItems'").SingleAsync() == 1);
            await migrator.MigrateAsync();
            Assert.Equal(2, await TablesAsync(context));
            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    private static Task<int> TablesAsync(AppDbContext context)
        => context.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS [Value] FROM sys.tables WHERE name IN ('MethodDocuments', 'MethodDocumentRevisions')").SingleAsync();
}
