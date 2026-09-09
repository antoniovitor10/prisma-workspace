using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prisma.Workspace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Move_Stage_BoardId_To_ProjectId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Nova coluna (ainda com BoardId intacto para o backfill)
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Stages",
                type: "uniqueidentifier",
                nullable: true);

            // 2) Backfill trivial: ProjectId da etapa = ProjectId do quadro antigo
            migrationBuilder.Sql("""
                UPDATE s
                SET s.ProjectId = b.ProjectId
                FROM Stages s
                INNER JOIN Boards b ON b.Id = s.BoardId
                WHERE s.ProjectId IS NULL
                """);

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM Stages WHERE ProjectId IS NULL)
                    THROW 50001, 'Backfill Stage.ProjectId deixou nulos — abortando migration.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Stages_Boards_BoardId",
                table: "Stages");

            migrationBuilder.DropIndex(
                name: "IX_Stages_BoardId",
                table: "Stages");

            migrationBuilder.DropColumn(
                name: "BoardId",
                table: "Stages");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProjectId",
                table: "Stages",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stages_ProjectId",
                table: "Stages",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Stages_ProjectId_Position",
                table: "Stages",
                columns: new[] { "ProjectId", "Position" });

            migrationBuilder.AddForeignKey(
                name: "FK_Stages_Projects_ProjectId",
                table: "Stages",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Boards.ProjectId obrigatório (já não há órfãos no banco E2E / produção descartável)
            migrationBuilder.AlterColumn<Guid>(
                name: "ProjectId",
                table: "Boards",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ProjectId",
                table: "Boards",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.DropForeignKey(
                name: "FK_Stages_Projects_ProjectId",
                table: "Stages");

            migrationBuilder.DropIndex(
                name: "IX_Stages_ProjectId_Position",
                table: "Stages");

            migrationBuilder.DropIndex(
                name: "IX_Stages_ProjectId",
                table: "Stages");

            migrationBuilder.AddColumn<Guid>(
                name: "BoardId",
                table: "Stages",
                type: "uniqueidentifier",
                nullable: true);

            // Reverse: escolhe um quadro do mesmo projeto (há exatamente 1 no estado atual)
            migrationBuilder.Sql("""
                UPDATE s
                SET s.BoardId = b.Id
                FROM Stages s
                CROSS APPLY (
                    SELECT TOP 1 b2.Id
                    FROM Boards b2
                    WHERE b2.ProjectId = s.ProjectId
                    ORDER BY b2.CreatedAt
                ) b
                WHERE s.BoardId IS NULL
                """);

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM Stages WHERE BoardId IS NULL)
                    THROW 50002, 'Down Stage.BoardId deixou nulos — abortando.', 1;
                """);

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Stages");

            migrationBuilder.AlterColumn<Guid>(
                name: "BoardId",
                table: "Stages",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stages_BoardId",
                table: "Stages",
                column: "BoardId");

            migrationBuilder.AddForeignKey(
                name: "FK_Stages_Boards_BoardId",
                table: "Stages",
                column: "BoardId",
                principalTable: "Boards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
