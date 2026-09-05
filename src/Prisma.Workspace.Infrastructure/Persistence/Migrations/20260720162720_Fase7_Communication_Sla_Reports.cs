using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prisma.Workspace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Fase7_Communication_Sla_Reports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    FirstResponseMinutes = table.Column<int>(type: "int", nullable: false),
                    ResolutionMinutes = table.Column<int>(type: "int", nullable: false),
                    ServiceStart = table.Column<TimeOnly>(type: "time", nullable: false),
                    ServiceEnd = table.Column<TimeOnly>(type: "time", nullable: false),
                    BusinessDaysMask = table.Column<int>(type: "int", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PauseWhileWaitingRequester = table.Column<bool>(type: "bit", nullable: false),
                    AlertsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    NearDueMinutes = table.Column<int>(type: "int", nullable: false),
                    HolidaysJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RulesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "SavedReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Visualization = table.Column<int>(type: "int", nullable: false),
                    DefinitionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsShared = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedReports_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
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

            migrationBuilder.CreateIndex(
                name: "IX_SavedReports_OrganizationId_IsShared_UpdatedAt",
                table: "SavedReports",
                columns: new[] { "OrganizationId", "IsShared", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedReports_OrganizationId_OwnerId_UpdatedAt",
                table: "SavedReports",
                columns: new[] { "OrganizationId", "OwnerId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedReports_ProjectId",
                table: "SavedReports",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectSlaPolicies");

            migrationBuilder.DropTable(
                name: "SavedReports");

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
    }
}
