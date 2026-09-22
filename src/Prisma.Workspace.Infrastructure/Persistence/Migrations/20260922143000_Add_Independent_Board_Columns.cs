using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Prisma.Workspace.Infrastructure.Persistence;

#nullable disable

namespace Prisma.Workspace.Infrastructure.Persistence.Migrations;

/// <summary>
/// Cria colunas independentes por quadro sem reescrever o histórico existente.
/// As etapas anteriores ficam sem BoardId e as clones apontam para elas por LegacyStageId.
/// </summary>
public partial class Add_Independent_Board_Columns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "BoardId",
            table: "Stages",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "LegacyStageId",
            table: "Stages",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Stages_BoardId",
            table: "Stages",
            column: "BoardId");

        migrationBuilder.CreateIndex(
            name: "IX_Stages_LegacyStageId",
            table: "Stages",
            column: "LegacyStageId");

        migrationBuilder.CreateIndex(
            name: "IX_Stages_BoardId_Position",
            table: "Stages",
            columns: new[] { "BoardId", "Position" });

        migrationBuilder.Sql("""
            DECLARE @workItemCount bigint = (SELECT COUNT_BIG(*) FROM WorkItems);
            DECLARE @stageHistoryCount bigint = (SELECT COUNT_BIG(*) FROM StageHistories);
            DECLARE @taskEventCount bigint = (SELECT COUNT_BIG(*) FROM TaskEvents);
            DECLARE @legacyStageCount bigint = (SELECT COUNT_BIG(*) FROM Stages);
            DECLARE @formCount bigint = (SELECT COUNT_BIG(*) FROM ExternalForms);
            DECLARE @initialStageCount bigint = (SELECT COUNT_BIG(*) FROM ExternalForms WHERE InitialStageId IS NOT NULL);
            DECLARE @automationCount bigint = (SELECT COUNT_BIG(*) FROM AutomationRules);
            DECLARE @moveActionCount bigint = (SELECT COUNT_BIG(*) FROM AutomationRules WHERE ActionType = 2);

            IF EXISTS (SELECT 1 FROM Stages WHERE BoardId IS NOT NULL OR LegacyStageId IS NOT NULL)
                THROW 50010, 'Stages já possui vínculo de quadro ou legado; abortando para evitar backfill ambíguo.', 1;

            IF EXISTS (
                SELECT 1
                FROM WorkItems wi
                LEFT JOIN Boards b ON b.Id = wi.BoardId
                LEFT JOIN Stages s ON s.Id = wi.StageId
                WHERE b.Id IS NULL
                   OR wi.StageId IS NULL OR s.Id IS NULL OR s.ProjectId <> b.ProjectId)
                THROW 50011, 'WorkItem/Board/Stage inconsistente; inventário e decisão humana são obrigatórios antes do backfill.', 1;

            DECLARE @stageMap TABLE
            (
                LegacyStageId uniqueidentifier NOT NULL,
                BoardId uniqueidentifier NOT NULL,
                CloneStageId uniqueidentifier NOT NULL,
                PRIMARY KEY (LegacyStageId, BoardId)
            );

            INSERT INTO @stageMap (LegacyStageId, BoardId, CloneStageId)
            SELECT s.Id, b.Id, NEWID()
            FROM Boards b
            INNER JOIN Stages s ON s.ProjectId = b.ProjectId
            WHERE s.BoardId IS NULL
              AND s.LegacyStageId IS NULL;

            IF EXISTS (
                SELECT 1 FROM AutomationRules r LEFT JOIN @stageMap m
                    ON m.BoardId = r.BoardId AND m.LegacyStageId = r.TriggerStageId
                WHERE m.CloneStageId IS NULL)
                THROW 50021, 'Automação com gatilho sem coluna válida no quadro; abortando backfill.', 1;

            IF EXISTS (
                SELECT 1 FROM AutomationRules r LEFT JOIN @stageMap m
                    ON m.BoardId = r.BoardId AND m.LegacyStageId = TRY_CONVERT(uniqueidentifier, r.ActionValue)
                WHERE r.ActionType = 2 AND (LEN(r.ActionValue) <> 36 OR m.CloneStageId IS NULL))
                THROW 50022, 'Automação MoveToStage com destino inválido/ambíguo no quadro; abortando backfill.', 1;

            IF EXISTS (SELECT 1 FROM ExternalForms WHERE ISJSON(AssignmentRulesJson) <> 1
                OR AssignmentRulesJson IS NULL OR LEFT(LTRIM(AssignmentRulesJson), 1) <> '[')
                THROW 50025, 'Formulário com regras JSON inválidas; abortando backfill.', 1;
            IF EXISTS (SELECT 1 FROM ExternalForms f CROSS APPLY OPENJSON(f.AssignmentRulesJson) r WHERE r.type <> 5)
                THROW 50025, 'Regras do formulário devem ser objetos JSON; abortando backfill.', 1;
            IF EXISTS (
                SELECT 1 FROM ExternalForms f LEFT JOIN ExternalPortals p ON p.Id=f.ExternalPortalId
                LEFT JOIN Boards b ON b.Id=p.BoardId
                LEFT JOIN @stageMap m ON m.BoardId=p.BoardId AND m.LegacyStageId=f.InitialStageId
                WHERE b.Id IS NULL OR (f.InitialStageId IS NOT NULL AND m.CloneStageId IS NULL))
                THROW 50026, 'Formulário com portal/quadro/coluna inicial incompatível; abortando backfill.', 1;
            IF EXISTS (
                SELECT f.Id, r.[key] FROM ExternalForms f CROSS APPLY OPENJSON(f.AssignmentRulesJson) r
                CROSS APPLY OPENJSON(r.value) property WHERE LOWER(property.[key]) = 'stageid'
                GROUP BY f.Id, r.[key] HAVING COUNT(*) > 1)
                THROW 50027, 'Regra JSON possui propriedades StageId duplicadas/ambíguas; abortando backfill.', 1;
            IF EXISTS (
                SELECT 1 FROM ExternalForms f JOIN ExternalPortals p ON p.Id=f.ExternalPortalId
                CROSS APPLY OPENJSON(f.AssignmentRulesJson) r CROSS APPLY OPENJSON(r.value) property
                LEFT JOIN @stageMap m ON m.BoardId=p.BoardId AND m.LegacyStageId=TRY_CONVERT(uniqueidentifier, property.value)
                WHERE LOWER(property.[key]) = 'stageid' AND property.type <> 0
                    AND (property.type <> 1 OR LEN(property.value) <> 36 OR m.CloneStageId IS NULL))
                THROW 50028, 'Regra de formulário com coluna inválida/ambígua no quadro; abortando backfill.', 1;

            DECLARE @formRuleCount bigint = (SELECT COUNT_BIG(*) FROM ExternalForms f CROSS APPLY OPENJSON(f.AssignmentRulesJson) r);
            DECLARE @formStageMap TABLE(FormId uniqueidentifier, RuleIndex int, PropertyName nvarchar(128),
                CloneStageId uniqueidentifier, PRIMARY KEY(FormId, RuleIndex));
            INSERT INTO @formStageMap
            SELECT f.Id, CONVERT(int,r.[key]), property.[key], m.CloneStageId
            FROM ExternalForms f JOIN ExternalPortals p ON p.Id=f.ExternalPortalId
            CROSS APPLY OPENJSON(f.AssignmentRulesJson) r CROSS APPLY OPENJSON(r.value) property
            JOIN @stageMap m ON m.BoardId=p.BoardId AND m.LegacyStageId=TRY_CONVERT(uniqueidentifier,property.value)
            WHERE LOWER(property.[key])='stageid' AND property.type=1;

            INSERT INTO Stages
                (Id, BoardId, LegacyStageId, ProjectId, WorkflowStatusId, Name, Position, Category, CreatedAt)
            SELECT m.CloneStageId, m.BoardId, m.LegacyStageId, s.ProjectId, s.WorkflowStatusId,
                   s.Name, s.Position, s.Category, s.CreatedAt
            FROM @stageMap m
            INNER JOIN Stages s ON s.Id = m.LegacyStageId;

            UPDATE wi
            SET wi.StageId = m.CloneStageId
            FROM WorkItems wi
            INNER JOIN @stageMap m
                ON m.BoardId = wi.BoardId
               AND m.LegacyStageId = wi.StageId;

            UPDATE r SET r.TriggerStageId = m.CloneStageId
            FROM AutomationRules r JOIN @stageMap m ON m.BoardId = r.BoardId AND m.LegacyStageId = r.TriggerStageId;
            IF @@ROWCOUNT <> @automationCount
                THROW 50023, 'A contagem de gatilhos remapeados divergiu; abortando.', 1;

            UPDATE r SET r.ActionValue = CONVERT(nvarchar(36), m.CloneStageId)
            FROM AutomationRules r JOIN @stageMap m
                ON m.BoardId = r.BoardId AND m.LegacyStageId = TRY_CONVERT(uniqueidentifier, r.ActionValue)
            WHERE r.ActionType = 2;
            IF @@ROWCOUNT <> @moveActionCount OR (SELECT COUNT_BIG(*) FROM AutomationRules) <> @automationCount
                THROW 50024, 'A contagem de automações/destinos divergiu; abortando.', 1;

            UPDATE f SET f.InitialStageId=m.CloneStageId
            FROM ExternalForms f JOIN ExternalPortals p ON p.Id=f.ExternalPortalId
            JOIN @stageMap m ON m.BoardId=p.BoardId AND m.LegacyStageId=f.InitialStageId;
            IF @@ROWCOUNT <> @initialStageCount
                THROW 50029, 'A contagem de colunas iniciais remapeadas divergiu; abortando.', 1;

            DECLARE @formId uniqueidentifier, @ruleIndex int, @propertyName nvarchar(128), @formClone uniqueidentifier;
            DECLARE form_stage_cursor CURSOR LOCAL FAST_FORWARD FOR
                SELECT FormId, RuleIndex, PropertyName, CloneStageId FROM @formStageMap;
            OPEN form_stage_cursor;
            FETCH NEXT FROM form_stage_cursor INTO @formId, @ruleIndex, @propertyName, @formClone;
            WHILE @@FETCH_STATUS = 0
            BEGIN
                UPDATE ExternalForms SET AssignmentRulesJson=JSON_MODIFY(AssignmentRulesJson,
                    '$['+CONVERT(varchar(12),@ruleIndex)+']."'+@propertyName+'"', CONVERT(nvarchar(36),@formClone))
                WHERE Id=@formId;
                FETCH NEXT FROM form_stage_cursor INTO @formId, @ruleIndex, @propertyName, @formClone;
            END;
            CLOSE form_stage_cursor;
            DEALLOCATE form_stage_cursor;
            IF (SELECT COUNT_BIG(*) FROM ExternalForms) <> @formCount
                OR (SELECT COUNT_BIG(*) FROM ExternalForms f CROSS APPLY OPENJSON(f.AssignmentRulesJson) r) <> @formRuleCount
                THROW 50030, 'A contagem de formulários/regras mudou durante o backfill; abortando.', 1;
            IF EXISTS (
                SELECT 1 FROM @formStageMap m JOIN ExternalForms f ON f.Id=m.FormId
                CROSS APPLY OPENJSON(f.AssignmentRulesJson) r CROSS APPLY OPENJSON(r.value) property
                WHERE CONVERT(int,r.[key])=m.RuleIndex AND property.[key]=m.PropertyName COLLATE Latin1_General_BIN2
                    AND TRY_CONVERT(uniqueidentifier,property.value) <> m.CloneStageId)
                THROW 50031, 'Uma regra do formulário não foi remapeada corretamente; abortando.', 1;

            IF (SELECT COUNT_BIG(*) FROM WorkItems) <> @workItemCount
                THROW 50012, 'A contagem de WorkItems mudou durante o backfill; abortando.', 1;

            IF (SELECT COUNT_BIG(*) FROM StageHistories) <> @stageHistoryCount
                THROW 50013, 'A contagem de StageHistories mudou durante o backfill; abortando.', 1;

            IF (SELECT COUNT_BIG(*) FROM TaskEvents) <> @taskEventCount
                THROW 50014, 'A contagem de TaskEvents mudou durante o backfill; abortando.', 1;

            IF (SELECT COUNT_BIG(*) FROM Stages) <> @legacyStageCount + (SELECT COUNT_BIG(*) FROM @stageMap)
                THROW 50015, 'A contagem de Stages clonadas não corresponde ao mapeamento; abortando.', 1;

            IF EXISTS (
                SELECT 1
                FROM WorkItems wi
                INNER JOIN Stages s ON s.Id = wi.StageId
                WHERE s.BoardId IS NULL OR s.BoardId <> wi.BoardId)
                THROW 50016, 'WorkItem permaneceu ligado a coluna legada ou de outro quadro; abortando.', 1;
            """);

        migrationBuilder.AddForeignKey(
            name: "FK_Stages_Boards_BoardId",
            table: "Stages",
            column: "BoardId",
            principalTable: "Boards",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Stages_Stages_LegacyStageId",
            table: "Stages",
            column: "LegacyStageId",
            principalTable: "Stages",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM Stages WHERE BoardId IS NULL AND LegacyStageId IS NOT NULL)
                THROW 50019, 'Rollback bloqueado: uma coluna clonada foi retirada. Restaure backup coordenado.', 1;

            IF EXISTS (
                SELECT 1 FROM Stages clone JOIN Stages original ON original.Id = clone.LegacyStageId
                WHERE clone.Name <> original.Name OR clone.Position <> original.Position OR clone.Category <> original.Category
                    OR ISNULL(CONVERT(varchar(36), clone.WorkflowStatusId), '') <> ISNULL(CONVERT(varchar(36), original.WorkflowStatusId), ''))
                THROW 50020, 'Rollback bloqueado: colunas foram alteradas após a migração.', 1;

            IF EXISTS (SELECT 1 FROM Stages WHERE BoardId IS NOT NULL AND LegacyStageId IS NULL)
                THROW 50017, 'Rollback bloqueado: existem colunas criadas após a migração sem origem legada.', 1;

            IF EXISTS (
                SELECT 1
                FROM StageHistories h
                INNER JOIN Stages s ON s.Id = h.StageId
                WHERE s.BoardId IS NOT NULL)
                THROW 50018, 'Rollback bloqueado: há histórico associado a colunas independentes. Restaure backup coordenado.', 1;

            IF EXISTS (SELECT 1 FROM ExternalForms WHERE ISJSON(AssignmentRulesJson) <> 1
                OR AssignmentRulesJson IS NULL OR LEFT(LTRIM(AssignmentRulesJson),1) <> '[')
                THROW 50032, 'Rollback bloqueado: regras JSON de formulário inválidas.', 1;
            IF EXISTS (SELECT 1 FROM ExternalForms f CROSS APPLY OPENJSON(f.AssignmentRulesJson) r WHERE r.type <> 5)
                THROW 50032, 'Rollback bloqueado: regras de formulário inválidas.', 1;

            UPDATE wi
            SET wi.StageId = s.LegacyStageId
            FROM WorkItems wi
            INNER JOIN Stages s ON s.Id = wi.StageId
            WHERE s.BoardId IS NOT NULL;

            UPDATE r SET r.TriggerStageId = s.LegacyStageId
            FROM AutomationRules r JOIN Stages s ON s.Id = r.TriggerStageId AND s.BoardId = r.BoardId
            WHERE s.BoardId IS NOT NULL;
            UPDATE r SET r.ActionValue = CONVERT(nvarchar(36), s.LegacyStageId)
            FROM AutomationRules r JOIN Stages s
                ON s.Id = TRY_CONVERT(uniqueidentifier, r.ActionValue) AND s.BoardId = r.BoardId
            WHERE r.ActionType = 2 AND s.BoardId IS NOT NULL;

            UPDATE f SET f.InitialStageId=s.LegacyStageId
            FROM ExternalForms f JOIN ExternalPortals p ON p.Id=f.ExternalPortalId
            JOIN Stages s ON s.Id=f.InitialStageId AND s.BoardId=p.BoardId
            WHERE s.BoardId IS NOT NULL;

            DECLARE @formId uniqueidentifier, @ruleIndex int, @propertyName nvarchar(128), @legacyStage uniqueidentifier;
            DECLARE form_stage_down_cursor CURSOR LOCAL FAST_FORWARD FOR
                SELECT f.Id, CONVERT(int,r.[key]), property.[key], s.LegacyStageId
                FROM ExternalForms f JOIN ExternalPortals p ON p.Id=f.ExternalPortalId
                CROSS APPLY OPENJSON(f.AssignmentRulesJson) r CROSS APPLY OPENJSON(r.value) property
                JOIN Stages s ON s.Id=TRY_CONVERT(uniqueidentifier,property.value) AND s.BoardId=p.BoardId
                WHERE LOWER(property.[key])='stageid' AND property.type=1 AND s.BoardId IS NOT NULL;
            OPEN form_stage_down_cursor;
            FETCH NEXT FROM form_stage_down_cursor INTO @formId, @ruleIndex, @propertyName, @legacyStage;
            WHILE @@FETCH_STATUS = 0
            BEGIN
                UPDATE ExternalForms SET AssignmentRulesJson=JSON_MODIFY(AssignmentRulesJson,
                    '$['+CONVERT(varchar(12),@ruleIndex)+']."'+@propertyName+'"', CONVERT(nvarchar(36),@legacyStage))
                WHERE Id=@formId;
                FETCH NEXT FROM form_stage_down_cursor INTO @formId, @ruleIndex, @propertyName, @legacyStage;
            END;
            CLOSE form_stage_down_cursor;
            DEALLOCATE form_stage_down_cursor;

            DELETE FROM Stages WHERE BoardId IS NOT NULL;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_Stages_Boards_BoardId",
            table: "Stages");

        migrationBuilder.DropForeignKey(
            name: "FK_Stages_Stages_LegacyStageId",
            table: "Stages");

        migrationBuilder.DropIndex(
            name: "IX_Stages_BoardId",
            table: "Stages");

        migrationBuilder.DropIndex(
            name: "IX_Stages_LegacyStageId",
            table: "Stages");

        migrationBuilder.DropIndex(
            name: "IX_Stages_BoardId_Position",
            table: "Stages");

        migrationBuilder.DropColumn(
            name: "BoardId",
            table: "Stages");

        migrationBuilder.DropColumn(
            name: "LegacyStageId",
            table: "Stages");
    }
}
