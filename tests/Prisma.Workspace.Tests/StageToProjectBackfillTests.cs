using Microsoft.Data.SqlClient;

namespace Prisma.Workspace.Tests;

/// <summary>
/// Prova o SQL de backfill Stage→Project. Roda contra o SQL Server E2E
/// (.env.e2e.connection). Com BoardId ainda presente: valida o UPDATE em transação.
/// Após a migration: valida que não há ProjectId nulo.
/// </summary>
public class StageToProjectBackfillTests
{
    /// <summary>Mesma fórmula usada na migration Move_Stage_BoardId_To_ProjectId.</summary>
    public const string BackfillSql = """
        UPDATE s
        SET s.ProjectId = b.ProjectId
        FROM Stages s
        INNER JOIN Boards b ON b.Id = s.BoardId
        WHERE s.ProjectId IS NULL
        """;

    [Fact]
    public async Task Stages_HaveProjectId_OrBackfillCopiesFromBoard()
    {
        await using var connection = await OpenE2eConnectionAsync();
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = "SELECT COL_LENGTH('dbo.Stages', 'BoardId')";
        var boardIdLen = await cmd.ExecuteScalarAsync();
        var hasBoardId = boardIdLen is not null && boardIdLen != DBNull.Value;

        cmd.CommandText = "SELECT COL_LENGTH('dbo.Stages', 'ProjectId')";
        var projectIdLen = await cmd.ExecuteScalarAsync();
        var hasProjectId = projectIdLen is not null && projectIdLen != DBNull.Value;

        cmd.CommandText = "SELECT COUNT(*) FROM Stages";
        var stageCount = (int)(await cmd.ExecuteScalarAsync())!;
        Assert.True(stageCount > 0, "Banco E2E precisa ter Stages.");

        if (hasBoardId && !hasProjectId)
        {
            // Pré-migration: prova o SQL sem alterar o schema permanente
            await using var tx = (SqlTransaction)await connection.BeginTransactionAsync();
            try
            {
                await using (var alter = connection.CreateCommand())
                {
                    alter.Transaction = tx;
                    alter.CommandText = "ALTER TABLE dbo.Stages ADD ProjectId uniqueidentifier NULL;";
                    await alter.ExecuteNonQueryAsync();
                }

                await using (var backfill = connection.CreateCommand())
                {
                    backfill.Transaction = tx;
                    backfill.CommandText = BackfillSql;
                    await backfill.ExecuteNonQueryAsync();
                }

                await using (var assert = connection.CreateCommand())
                {
                    assert.Transaction = tx;
                    assert.CommandText = "SELECT COUNT(*) FROM Stages WHERE ProjectId IS NULL";
                    Assert.Equal(0, (int)(await assert.ExecuteScalarAsync())!);

                    assert.CommandText = """
                        SELECT COUNT(*) FROM Stages s
                        INNER JOIN Boards b ON b.Id = s.BoardId
                        WHERE s.ProjectId <> b.ProjectId
                        """;
                    Assert.Equal(0, (int)(await assert.ExecuteScalarAsync())!);
                }
            }
            finally
            {
                await tx.RollbackAsync();
            }
            return;
        }

        Assert.True(hasProjectId, "Stages.ProjectId deveria existir após a migration.");
        cmd.CommandText = "SELECT COUNT(*) FROM Stages WHERE ProjectId IS NULL";
        Assert.Equal(0, (int)(await cmd.ExecuteScalarAsync())!);

        cmd.CommandText = """
            SELECT COUNT(*) FROM Stages s
            WHERE NOT EXISTS (SELECT 1 FROM Projects p WHERE p.Id = s.ProjectId)
            """;
        Assert.Equal(0, (int)(await cmd.ExecuteScalarAsync())!);

        cmd.CommandText = "SELECT COUNT(*) FROM Boards WHERE ProjectId IS NULL";
        Assert.Equal(0, (int)(await cmd.ExecuteScalarAsync())!);
    }

    private static async Task<SqlConnection> OpenE2eConnectionAsync()
    {
        var connectionString = ResolveE2eConnectionString();
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static string ResolveE2eConnectionString()
    {
        var fromEnv = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv;

        var root = FindRepoRoot();
        var path = Path.Combine(root, ".env.e2e.connection");
        Assert.True(File.Exists(path),
            "Arquivo .env.e2e.connection não encontrado. Suba o SQL E2E antes deste teste.");
        return File.ReadAllText(path).Trim();
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, ".env.e2e.connection"))
                || File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar a raiz do repositório.");
    }
}
