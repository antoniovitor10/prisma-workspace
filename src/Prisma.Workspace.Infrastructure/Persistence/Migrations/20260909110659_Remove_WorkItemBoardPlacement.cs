using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prisma.Workspace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Remove_WorkItemBoardPlacement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkItemBoardPlacements");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkItemBoardPlacements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BoardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Position = table.Column<double>(type: "float", nullable: false),
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
        }
    }
}
