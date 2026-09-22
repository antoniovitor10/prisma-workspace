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

            UPDATE wi
            SET wi.StageId = s.LegacyStageId
            FROM WorkItems wi
            INNER JOIN Stages s ON s.Id = wi.StageId
            WHERE s.BoardId IS NOT NULL;

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
