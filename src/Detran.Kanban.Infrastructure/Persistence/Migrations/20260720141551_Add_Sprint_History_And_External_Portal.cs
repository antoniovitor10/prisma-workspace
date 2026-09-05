using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detran.Kanban.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_Sprint_History_And_External_Portal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                table: "Sprints",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "Sprints",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsExternalVisible",
                table: "Attachments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ExternalPortals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BoardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicSlug = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RequiresAuthentication = table.Column<bool>(type: "bit", nullable: false),
                    AccessModes = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalPortals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalPortals_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExternalPortals_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SprintItemSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SprintId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemNumber = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: true),
                    EstimatedHours = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    WasCompleted = table.Column<bool>(type: "bit", nullable: false),
                    WorkItemCompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    DestinationSprintId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CapturedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprintItemSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SprintItemSnapshots_Sprints_SprintId",
                        column: x => x.SprintId,
                        principalTable: "Sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalPortalInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalPortalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalPortalInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalPortalInvitations_ExternalPortals_ExternalPortalId",
                        column: x => x.ExternalPortalId,
                        principalTable: "ExternalPortals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalPortalVerifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalPortalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalPortalVerifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalPortalVerifications_ExternalPortals_ExternalPortalId",
                        column: x => x.ExternalPortalId,
                        principalTable: "ExternalPortals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalPortalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Protocol = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AccessKeyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequesterEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: true),
                    RatingComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletionConfirmedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalRequests_ExternalPortals_ExternalPortalId",
                        column: x => x.ExternalPortalId,
                        principalTable: "ExternalPortals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExternalRequests_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalRequestMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorType = table.Column<int>(type: "int", nullable: false),
                    AuthorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AuthorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalRequestMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalRequestMessages_ExternalRequests_ExternalRequestId",
                        column: x => x.ExternalRequestId,
                        principalTable: "ExternalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_ProjectId_Status",
                table: "Sprints",
                columns: new[] { "ProjectId", "Status" },
                unique: true,
                filter: "[Status] = 2");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalPortalInvitations_ExternalPortalId",
                table: "ExternalPortalInvitations",
                column: "ExternalPortalId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalPortalInvitations_TokenHash",
                table: "ExternalPortalInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalPortals_BoardId",
                table: "ExternalPortals",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalPortals_ProjectId",
                table: "ExternalPortals",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalPortals_PublicSlug",
                table: "ExternalPortals",
                column: "PublicSlug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalPortalVerifications_ExternalPortalId_Email_ExpiresAt",
                table: "ExternalPortalVerifications",
                columns: new[] { "ExternalPortalId", "Email", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRequestMessages_ExternalRequestId_CreatedAt",
                table: "ExternalRequestMessages",
                columns: new[] { "ExternalRequestId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRequests_ExternalPortalId_CreatedAt",
                table: "ExternalRequests",
                columns: new[] { "ExternalPortalId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRequests_Protocol",
                table: "ExternalRequests",
                column: "Protocol",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRequests_WorkItemId",
                table: "ExternalRequests",
                column: "WorkItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SprintItemSnapshots_SprintId_WorkItemId",
                table: "SprintItemSnapshots",
                columns: new[] { "SprintId", "WorkItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalPortalInvitations");

            migrationBuilder.DropTable(
                name: "ExternalPortalVerifications");

            migrationBuilder.DropTable(
                name: "ExternalRequestMessages");

            migrationBuilder.DropTable(
                name: "SprintItemSnapshots");

            migrationBuilder.DropTable(
                name: "ExternalRequests");

            migrationBuilder.DropTable(
                name: "ExternalPortals");

            migrationBuilder.DropIndex(
                name: "IX_Sprints_ProjectId_Status",
                table: "Sprints");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Sprints");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Sprints");

            migrationBuilder.DropColumn(
                name: "IsExternalVisible",
                table: "Attachments");
        }
    }
}
