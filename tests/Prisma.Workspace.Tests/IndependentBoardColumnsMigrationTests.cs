using Microsoft.Data.SqlClient;
using System.Reflection;
using System.Text.Json;
using Prisma.Workspace.Application.Features.ExternalPortal;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
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

    [Fact]
    public async Task Backfill_MapsAutomationTriggerAndMoveTargetByBoardAndReversesImmediately()
    {
        await using var connection = await OpenAsync();
        await CreateFixtureAsync(connection, false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO #D89AutomationRules SELECT NEWID(), Id,
                '00000000-0000-0000-0000-000000000004',2,'00000000-0000-0000-0000-000000000004'
            FROM #D89Boards;
            """;
        await command.ExecuteNonQueryAsync();
        command.CommandText = TemporarySql();
        await command.ExecuteNonQueryAsync();
        command.CommandText = """
            SELECT COUNT(*) FROM #D89AutomationRules r
            JOIN #D89Stages triggerStage ON triggerStage.Id=r.TriggerStageId AND triggerStage.BoardId=r.BoardId
            JOIN #D89Stages targetStage ON targetStage.Id=TRY_CONVERT(uniqueidentifier,r.ActionValue) AND targetStage.BoardId=r.BoardId
            WHERE triggerStage.LegacyStageId='00000000-0000-0000-0000-000000000004'
                AND targetStage.LegacyStageId=triggerStage.LegacyStageId;
            """;
        Assert.Equal(2, (int)(await command.ExecuteScalarAsync())!);
        command.CommandText = new Add_Independent_Board_Columns().DownOperations.OfType<SqlOperation>().Single().Sql
            .Replace("StageHistories", "#D89StageHistories").Replace("WorkItems", "#D89WorkItems")
            .Replace("Stages", "#D89Stages").Replace("AutomationRules", "#D89AutomationRules")
            .Replace("ExternalForms", "#D89ExternalForms").Replace("ExternalPortals", "#D89ExternalPortals");
        await command.ExecuteNonQueryAsync();
        command.CommandText = """
            SELECT COUNT(*) FROM #D89AutomationRules
            WHERE TriggerStageId='00000000-0000-0000-0000-000000000004'
                AND ActionValue='00000000-0000-0000-0000-000000000004';
            """;
        Assert.Equal(2, (int)(await command.ExecuteScalarAsync())!);
    }

    [Theory]
    [InlineData(true, 50021)]
    [InlineData(false, 50022)]
    public async Task Backfill_RejectsInvalidAutomationReferencesBeforeCloning(bool invalidTrigger, int expectedError)
    {
        await using var connection = await OpenAsync();
        await CreateFixtureAsync(connection, false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO #D89AutomationRules VALUES(NEWID(),'00000000-0000-0000-0000-000000000002',
                @trigger,2,@target);
            """;
        command.Parameters.AddWithValue("@trigger", invalidTrigger ? Guid.NewGuid() : Guid.Parse("00000000-0000-0000-0000-000000000004"));
        command.Parameters.AddWithValue("@target", invalidTrigger ? "00000000-0000-0000-0000-000000000004" : "invalid");
        await command.ExecuteNonQueryAsync();
        command.CommandText = TemporarySql();
        var error = await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(expectedError, error.Number);
        command.CommandText = "SELECT COUNT(*) FROM #D89Stages WHERE BoardId IS NOT NULL";
        Assert.Equal(0, (int)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Backfill_MapsFormInitialAndJsonRules_ThenSubmissionUsesBoardClone()
    {
        await using var connection = await OpenAsync();
        await CreateFixtureAsync(connection, false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO #D89Stages VALUES ('00000000-0000-0000-0000-000000000005',NULL,NULL,
                '00000000-0000-0000-0000-000000000001',NULL,N'Fila roteada',275,3,SYSDATETIMEOFFSET());
            INSERT INTO #D89ExternalPortals SELECT Id, Id FROM #D89Boards;
            INSERT INTO #D89ExternalForms SELECT NEWID(), Id,'00000000-0000-0000-0000-000000000004',
                '[{"fieldKey":"category","operator":0,"StageId":"00000000-0000-0000-0000-000000000005","position":0,"extra":"preservado"},{"fieldKey":"other","operator":1,"expectedValue":"never","stageId":null,"position":1}]'
                FROM #D89ExternalPortals;
            """;
        await command.ExecuteNonQueryAsync();
        command.CommandText = TemporarySql();
        await command.ExecuteNonQueryAsync();
        command.CommandText = """
            SELECT COUNT(*) FROM #D89ExternalForms f JOIN #D89ExternalPortals p ON p.Id=f.ExternalPortalId
            JOIN #D89Stages initialStage ON initialStage.Id=f.InitialStageId AND initialStage.BoardId=p.BoardId
            JOIN #D89Stages ruleStage ON ruleStage.Id=TRY_CONVERT(uniqueidentifier,JSON_VALUE(f.AssignmentRulesJson,'$[0].StageId'))
                AND ruleStage.BoardId=p.BoardId
            WHERE initialStage.LegacyStageId='00000000-0000-0000-0000-000000000004'
                AND ruleStage.LegacyStageId='00000000-0000-0000-0000-000000000005'
                AND JSON_VALUE(f.AssignmentRulesJson,'$[0].extra')='preservado';
            """;
        Assert.Equal(2, (int)(await command.ExecuteScalarAsync())!);
        command.CommandText = """
            SELECT TOP 1 f.InitialStageId, f.AssignmentRulesJson, p.BoardId FROM #D89ExternalForms f
            JOIN #D89ExternalPortals p ON p.Id=f.ExternalPortalId;
            """;
        Guid initialId, boardId;string rules;
        await using (var reader = await command.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            initialId=reader.GetGuid(0);rules=reader.GetString(1);boardId=reader.GetGuid(2);
        }
        var project=Project.Criar("D89FORM", "Formulário migrado", "test", WorkNature.Project, WorkType.Development);
        var organizationId=Guid.NewGuid();
        var board=new Board { Id=boardId, ProjectId=project.Id, OrganizationId=organizationId, Name="Formulário" };
        var ruleId=JsonDocument.Parse(rules).RootElement[0].GetProperty("StageId").GetGuid();
        project.Stages.Add(new Stage { Id=initialId, ProjectId=project.Id, BoardId=boardId, Name="Inicial", Category=StageCategory.Backlog });
        project.Stages.Add(new Stage { Id=ruleId, ProjectId=project.Id, BoardId=boardId, Name="Roteada", Category=StageCategory.InProgress });
        var portal=ExternalPortal.Create(organizationId, project.Id, boardId, "d89-formulario", false, false, ExternalPortalAccessMode.PublicLink);
        portal.Project=project;portal.Board=board;
        var form=ExternalForm.CreateDefault(portal.Id, project.Name);
        form.ExternalPortal=portal;form.InitialStageId=initialId;form.AssignmentRulesJson=rules;
        var repository=DispatchProxy.Create<IExternalPortalRepository, FormRepositoryProxy>();
        var proxy=(FormRepositoryProxy)(object)repository;
        proxy.Form=form;
        var handler=new SubmitExternalFormCommandHandler(repository, null!, null!);
        var request=new SubmitExternalFormCommand(portal.PublicSlug, form.PublicSlug,
            new Dictionary<string,string?> { ["requesterName"]="Teste", ["requesterEmail"]="test@example.invalid",
                ["subject"]="Submissão migrada", ["description"]="Descrição sintética da solicitação", ["category"]="infra" }, [],
            DateTimeOffset.UtcNow.AddSeconds(-5), null, null, null, null, null);
        await handler.Handle(request, default);
        Assert.NotNull(proxy.Created);
        Assert.Equal(boardId, proxy.Created.BoardId);
        Assert.Equal(ruleId, proxy.Created.StageId);

        command.CommandText = new Add_Independent_Board_Columns().DownOperations.OfType<SqlOperation>().Single().Sql
            .Replace("StageHistories", "#D89StageHistories").Replace("WorkItems", "#D89WorkItems")
            .Replace("Stages", "#D89Stages").Replace("AutomationRules", "#D89AutomationRules")
            .Replace("ExternalForms", "#D89ExternalForms").Replace("ExternalPortals", "#D89ExternalPortals");
        await command.ExecuteNonQueryAsync();
        command.CommandText = """
            SELECT COUNT(*) FROM #D89ExternalForms WHERE InitialStageId='00000000-0000-0000-0000-000000000004'
                AND JSON_VALUE(AssignmentRulesJson,'$[0].StageId')='00000000-0000-0000-0000-000000000005'
                AND JSON_VALUE(AssignmentRulesJson,'$[0].extra')='preservado';
            """;
        Assert.Equal(2, (int)(await command.ExecuteScalarAsync())!);
    }

    [Theory]
    [InlineData(0, 50025)]
    [InlineData(1, 50027)]
    [InlineData(2, 50028)]
    public async Task Backfill_RejectsInvalidOrAmbiguousFormRulesBeforeCloning(int scenario, int expectedError)
    {
        await using var connection=await OpenAsync();
        await CreateFixtureAsync(connection, false);
        await using var command=connection.CreateCommand();
        var json=scenario switch
        {
            0 => "{invalid",
            1 => """[{"stageId":null,"StageId":"00000000-0000-0000-0000-000000000004"}]""",
            _ => """[{"stageId":"00000000-0000-0000-0000-000000000099"}]"""
        };
        command.CommandText="""
            INSERT INTO #D89ExternalPortals SELECT Id,Id FROM #D89Boards;
            INSERT INTO #D89ExternalForms SELECT NEWID(),Id,'00000000-0000-0000-0000-000000000004',@rules
            FROM #D89ExternalPortals;
            """;
        command.Parameters.AddWithValue("@rules",json);
        await command.ExecuteNonQueryAsync();
        command.CommandText=TemporarySql();
        var error=await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteNonQueryAsync());
        Assert.Equal(expectedError,error.Number);
        command.CommandText="SELECT COUNT(*) FROM #D89Stages WHERE BoardId IS NOT NULL";
        Assert.Equal(0,(int)(await command.ExecuteScalarAsync())!);
    }

    public class FormRepositoryProxy : DispatchProxy
    {
        public ExternalForm Form { get; set; } = null!;
        public WorkItem? Created { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "GetPublicFormAsync") return Task.FromResult<ExternalForm?>(Form);
            if (method.Name == "GetNextBacklogRankAsync") return Task.FromResult(100m);
            if (method.Name == "CreateRequestAsync")
            {
                Created=(WorkItem)args![0]!;
                return Task.FromResult((ExternalRequest)args[1]!);
            }
            throw new NotSupportedException(method.Name);
        }
    }

    private static string TemporarySql() => new Add_Independent_Board_Columns().UpOperations
        .OfType<SqlOperation>().Single().Sql.Replace("StageHistories", "#D89StageHistories")
        .Replace("WorkItems", "#D89WorkItems").Replace("TaskEvents", "#D89TaskEvents")
        .Replace("Stages", "#D89Stages").Replace("Boards", "#D89Boards")
        .Replace("AutomationRules", "#D89AutomationRules")
        .Replace("ExternalForms", "#D89ExternalForms").Replace("ExternalPortals", "#D89ExternalPortals");

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
            CREATE TABLE #D89ExternalPortals(Id uniqueidentifier PRIMARY KEY, BoardId uniqueidentifier);
            CREATE TABLE #D89ExternalForms(Id uniqueidentifier PRIMARY KEY, ExternalPortalId uniqueidentifier,
                InitialStageId uniqueidentifier NULL, AssignmentRulesJson nvarchar(max));
            CREATE TABLE #D89TaskEvents(Id uniqueidentifier PRIMARY KEY, Payload nvarchar(max));
            CREATE TABLE #D89AutomationRules(Id uniqueidentifier PRIMARY KEY, BoardId uniqueidentifier,
                TriggerStageId uniqueidentifier, ActionType int, ActionValue nvarchar(500));
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
