using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Prisma.Workspace.Infrastructure.Persistence.Migrations;

namespace Prisma.Workspace.Tests;

public class IndependentBoardColumnsMigrationTests
{
    [Fact]
    public void Migration_ExposesTargetModelWithBoardAndLegacyReferences()
    {
        var migration = new Add_Independent_Board_Columns();
        var stage = migration.TargetModel.FindEntityType("Prisma.Workspace.Domain.Entities.Stage");
        Assert.NotNull(stage);
        Assert.NotNull(stage.FindProperty("BoardId"));
        Assert.NotNull(stage.FindProperty("LegacyStageId"));
    }

    [Fact]
    public async Task Backfill_ClonesPerBoardPreservesTasksAndHistoricalIds()
    {
        await using var connection = await OpenAsync();
        await CreateFixtureAsync(connection, false);
        await using var command = connection.CreateCommand();
        command.CommandText = TemporarySql();
        await command.ExecuteNonQueryAsync();
        command.CommandText = """
            SELECT COUNT(*) FROM #D89Stages WHERE BoardId IS NOT NULL;
            SELECT COUNT(*) FROM #D89WorkItems wi JOIN #D89Stages s ON s.Id=wi.StageId
                WHERE s.BoardId=wi.BoardId AND s.LegacyStageId='00000000-0000-0000-0000-000000000004';
            SELECT COUNT(*) FROM #D89StageHistories WHERE StageId='00000000-0000-0000-0000-000000000004';
            SELECT COUNT(*) FROM #D89TaskEvents WHERE Payload='preservado';
            """;
        await using var reader = await command.ExecuteReaderAsync();
        foreach (var expected in new[] { 2, 2, 1, 1 })
        {
            Assert.True(await reader.ReadAsync());
            Assert.Equal(expected, reader.GetInt32(0));
            await reader.NextResultAsync();
        }
    }

    [Fact]
    public async Task Backfill_RejectsMissingStageBeforeCloning()
    {
        await using var connection = await OpenAsync();
        await CreateFixtureAsync(connection, true);
        await using var command = connection.CreateCommand();
        command.CommandText = TemporarySql();
        var error = await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(50011, error.Number);
        command.CommandText = "SELECT COUNT(*) FROM #D89Stages WHERE BoardId IS NOT NULL";
        Assert.Equal(0, (int)(await command.ExecuteScalarAsync())!);
    }

    private static string TemporarySql() => new Add_Independent_Board_Columns().UpOperations
        .OfType<SqlOperation>().Single().Sql.Replace("StageHistories", "#D89StageHistories")
        .Replace("WorkItems", "#D89WorkItems").Replace("TaskEvents", "#D89TaskEvents")
        .Replace("Stages", "#D89Stages").Replace("Boards", "#D89Boards");

    private static async Task<SqlConnection> OpenAsync()
    {
        var value = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        Assert.False(string.IsNullOrWhiteSpace(value), "Configure o banco E2E exclusivo.");
        var builder = new SqlConnectionStringBuilder(value);
        Assert.StartsWith("Prisma_BoardColumns_", builder.InitialCatalog);
        var connection = new SqlConnection(value);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task CreateFixtureAsync(SqlConnection connection, bool missingStage)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE #D89Boards(Id uniqueidentifier PRIMARY KEY, ProjectId uniqueidentifier NOT NULL);
            CREATE TABLE #D89Stages(Id uniqueidentifier PRIMARY KEY, BoardId uniqueidentifier NULL,
                LegacyStageId uniqueidentifier NULL, ProjectId uniqueidentifier NOT NULL,
                WorkflowStatusId uniqueidentifier NULL, Name nvarchar(200), Position float,
                Category int, CreatedAt datetimeoffset);
            CREATE TABLE #D89WorkItems(Id uniqueidentifier PRIMARY KEY, BoardId uniqueidentifier, StageId uniqueidentifier NULL);
            CREATE TABLE #D89StageHistories(Id uniqueidentifier PRIMARY KEY, StageId uniqueidentifier);
            CREATE TABLE #D89TaskEvents(Id uniqueidentifier PRIMARY KEY, Payload nvarchar(max));
            INSERT INTO #D89Boards VALUES ('00000000-0000-0000-0000-000000000002','00000000-0000-0000-0000-000000000001'),
                ('00000000-0000-0000-0000-000000000003','00000000-0000-0000-0000-000000000001');
            INSERT INTO #D89Stages VALUES ('00000000-0000-0000-0000-000000000004',NULL,NULL,
                '00000000-0000-0000-0000-000000000001',NULL,N'Revisão livre',175,3,SYSDATETIMEOFFSET());
            INSERT INTO #D89WorkItems VALUES(NEWID(),'00000000-0000-0000-0000-000000000002',
                '00000000-0000-0000-0000-000000000004'),
                (NEWID(),'00000000-0000-0000-0000-000000000003','00000000-0000-0000-0000-000000000004');
            INSERT INTO #D89StageHistories VALUES(NEWID(),'00000000-0000-0000-0000-000000000004');
            INSERT INTO #D89TaskEvents VALUES(NEWID(),'preservado');
            """;
        if (missingStage) command.CommandText += "UPDATE #D89WorkItems SET StageId=NULL;";
        await command.ExecuteNonQueryAsync();
    }
}
