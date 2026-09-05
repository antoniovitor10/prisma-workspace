using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prisma.Workspace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Fase6E_Workflow_MyWork_Kanban : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowStatusId",
                table: "WorkItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowStatusId",
                table: "Stages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardSettingsJson",
                table: "Boards",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkflowStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    Position = table.Column<double>(type: "float", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    IsInitial = table.Column<bool>(type: "bit", nullable: false),
                    IsFinal = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowStatuses_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTransitions",
                columns: table => new
                {
                    SourceStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTransitions", x => new { x.SourceStatusId, x.TargetStatusId });
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_WorkflowStatuses_SourceStatusId",
                        column: x => x.SourceStatusId,
                        principalTable: "WorkflowStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_WorkflowStatuses_TargetStatusId",
                        column: x => x.TargetStatusId,
                        principalTable: "WorkflowStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                CREATE TABLE #WorkflowSeed
                (
                    StatusId uniqueidentifier NOT NULL,
                    ProjectId uniqueidentifier NOT NULL,
                    Name nvarchar(120) NOT NULL,
                    Category int NOT NULL,
                    Position float NOT NULL
                );

                INSERT INTO #WorkflowSeed (StatusId, ProjectId, Name, Category, Position)
                SELECT NEWID(), b.ProjectId, LEFT(s.Name, 120), s.Category, MIN(s.Position)
                FROM Stages s
                INNER JOIN Boards b ON b.Id = s.BoardId
                WHERE b.ProjectId IS NOT NULL
                GROUP BY b.ProjectId, LEFT(s.Name, 120), s.Category;

                ;WITH Ranked AS
                (
                    SELECT ws.*,
                        ROW_NUMBER() OVER (
                            PARTITION BY ws.ProjectId
                            ORDER BY ws.Position, ws.StatusId) AS InitialRank
                    FROM #WorkflowSeed ws
                )
                INSERT INTO WorkflowStatuses
                    (Id, ProjectId, Name, Color, Position, Category,
                     IsInitial, IsFinal, CreatedAt, UpdatedAt)
                SELECT StatusId, ProjectId, Name,
                    CASE Category
                        WHEN 1 THEN '#94A3B8'
                        WHEN 2 THEN '#3B82F6'
                        WHEN 3 THEN '#F59E0B'
                        WHEN 4 THEN '#8B5CF6'
                        WHEN 5 THEN '#10B981'
                        ELSE '#64748B'
                    END,
                    Position, Category,
                    CASE WHEN InitialRank = 1 THEN 1 ELSE 0 END,
                    CASE WHEN Category = 5 THEN 1 ELSE 0 END,
                    SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM Ranked;

                UPDATE s
                SET s.WorkflowStatusId = ws.StatusId
                FROM Stages s
                INNER JOIN Boards b ON b.Id = s.BoardId
                INNER JOIN #WorkflowSeed ws
                    ON ws.ProjectId = b.ProjectId
                    AND ws.Name = LEFT(s.Name, 120)
                    AND ws.Category = s.Category;

                UPDATE wi
                SET wi.WorkflowStatusId = s.WorkflowStatusId
                FROM WorkItems wi
                INNER JOIN Stages s ON s.Id = wi.StageId
                WHERE s.WorkflowStatusId IS NOT NULL;

                INSERT INTO WorkflowTransitions (SourceStatusId, TargetStatusId, CreatedAt)
                SELECT source.StatusId, target.StatusId, SYSUTCDATETIME()
                FROM #WorkflowSeed source
                INNER JOIN #WorkflowSeed target
                    ON target.ProjectId = source.ProjectId
                    AND target.StatusId <> source.StatusId;

                DROP TABLE #WorkflowSeed;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_WorkflowStatusId",
                table: "WorkItems",
                column: "WorkflowStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Stages_WorkflowStatusId",
                table: "Stages",
                column: "WorkflowStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStatuses_ProjectId_Name",
                table: "WorkflowStatuses",
                columns: new[] { "ProjectId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStatuses_ProjectId_Position",
                table: "WorkflowStatuses",
                columns: new[] { "ProjectId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_TargetStatusId",
                table: "WorkflowTransitions",
                column: "TargetStatusId");

            migrationBuilder.AddForeignKey(
                name: "FK_Stages_WorkflowStatuses_WorkflowStatusId",
                table: "Stages",
                column: "WorkflowStatusId",
                principalTable: "WorkflowStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_WorkflowStatuses_WorkflowStatusId",
                table: "WorkItems",
                column: "WorkflowStatusId",
                principalTable: "WorkflowStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stages_WorkflowStatuses_WorkflowStatusId",
                table: "Stages");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_WorkflowStatuses_WorkflowStatusId",
                table: "WorkItems");

            migrationBuilder.DropTable(
                name: "WorkflowTransitions");

            migrationBuilder.DropTable(
                name: "WorkflowStatuses");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_WorkflowStatusId",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_Stages_WorkflowStatusId",
                table: "Stages");

            migrationBuilder.DropColumn(
                name: "WorkflowStatusId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "WorkflowStatusId",
                table: "Stages");

            migrationBuilder.DropColumn(
                name: "CardSettingsJson",
                table: "Boards");
        }
    }
}
