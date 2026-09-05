using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detran.Kanban.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MultiBoard_WorkItemPlacements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_Boards_BoardId",
                table: "WorkItems");

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultBoardId",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkItemBoardPlacements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BoardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Position = table.Column<double>(type: "float", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemBoardPlacements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemBoardPlacements_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItemBoardPlacements_Stages_StageId",
                        column: x => x.StageId,
                        principalTable: "Stages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkItemBoardPlacements_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_DefaultBoardId",
                table: "Projects",
                column: "DefaultBoardId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemBoardPlacements_BoardId",
                table: "WorkItemBoardPlacements",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemBoardPlacements_BoardId_StageId_Position",
                table: "WorkItemBoardPlacements",
                columns: new[] { "BoardId", "StageId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemBoardPlacements_StageId",
                table: "WorkItemBoardPlacements",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemBoardPlacements_WorkItemId_BoardId",
                table: "WorkItemBoardPlacements",
                columns: new[] { "WorkItemId", "BoardId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Boards_DefaultBoardId",
                table: "Projects",
                column: "DefaultBoardId",
                principalTable: "Boards",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_Boards_BoardId",
                table: "WorkItems",
                column: "BoardId",
                principalTable: "Boards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Backfill: cria placement canônico para cada WorkItem existente
            // usando seu BoardId/StageId/Position atuais.
            migrationBuilder.Sql(@"
                INSERT INTO WorkItemBoardPlacements (Id, WorkItemId, BoardId, StageId, Position, CreatedAt, UpdatedAt)
                SELECT NEWID(), wi.Id, wi.BoardId, wi.StageId, wi.Position,
                       wi.CreatedAt, wi.UpdatedAt
                FROM WorkItems wi
                WHERE NOT EXISTS (
                    SELECT 1 FROM WorkItemBoardPlacements p
                    WHERE p.WorkItemId = wi.Id AND p.BoardId = wi.BoardId
                );
            ");

            // Backfill: define DefaultBoardId nos projetos que ainda não têm
            migrationBuilder.Sql(@"
                UPDATE Projects
                SET DefaultBoardId = (
                    SELECT TOP 1 b.Id
                    FROM Boards b
                    WHERE b.ProjectId = Projects.Id
                    ORDER BY b.CreatedAt
                )
                WHERE DefaultBoardId IS NULL
                  AND EXISTS (
                    SELECT 1 FROM Boards b2 WHERE b2.ProjectId = Projects.Id
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Boards_DefaultBoardId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_Boards_BoardId",
                table: "WorkItems");

            migrationBuilder.DropTable(
                name: "WorkItemBoardPlacements");

            migrationBuilder.DropIndex(
                name: "IX_Projects_DefaultBoardId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DefaultBoardId",
                table: "Projects");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_Boards_BoardId",
                table: "WorkItems",
                column: "BoardId",
                principalTable: "Boards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
