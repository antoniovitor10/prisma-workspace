using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prisma.Workspace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Remove_Sla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectSlaPolicies");

            migrationBuilder.DropIndex(
                name: "IX_ExternalRequests_FirstResponseDueAt",
                table: "ExternalRequests");

            migrationBuilder.DropIndex(
                name: "IX_ExternalRequests_ResolutionDueAt",
                table: "ExternalRequests");

            migrationBuilder.DropColumn(
                name: "FirstRespondedAt",
                table: "ExternalRequests");

            migrationBuilder.DropColumn(
                name: "FirstResponseDueAt",
                table: "ExternalRequests");

            migrationBuilder.DropColumn(
                name: "ResolutionDueAt",
                table: "ExternalRequests");

            migrationBuilder.DropColumn(
                name: "SlaPausedAt",
                table: "ExternalRequests");

            migrationBuilder.DropColumn(
                name: "SlaPausedBusinessMinutes",
                table: "ExternalRequests");

            migrationBuilder.DropColumn(
                name: "SlaPolicySnapshotJson",
                table: "ExternalRequests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FirstRespondedAt",
                table: "ExternalRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FirstResponseDueAt",
                table: "ExternalRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResolutionDueAt",
                table: "ExternalRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SlaPausedAt",
                table: "ExternalRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SlaPausedBusinessMinutes",
                table: "ExternalRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SlaPolicySnapshotJson",
                table: "ExternalRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectSlaPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlertsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    BusinessDaysMask = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FirstResponseMinutes = table.Column<int>(type: "int", nullable: false),
                    HolidaysJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    NearDueMinutes = table.Column<int>(type: "int", nullable: false),
                    PauseWhileWaitingRequester = table.Column<bool>(type: "bit", nullable: false),
                    ResolutionMinutes = table.Column<int>(type: "int", nullable: false),
                    RulesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ServiceEnd = table.Column<TimeOnly>(type: "time", nullable: false),
                    ServiceStart = table.Column<TimeOnly>(type: "time", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSlaPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectSlaPolicies_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRequests_FirstResponseDueAt",
                table: "ExternalRequests",
                column: "FirstResponseDueAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRequests_ResolutionDueAt",
                table: "ExternalRequests",
                column: "ResolutionDueAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSlaPolicies_ProjectId",
                table: "ProjectSlaPolicies",
                column: "ProjectId",
                unique: true);
        }
    }
}
