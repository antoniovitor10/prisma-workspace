using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prisma.Workspace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_AiAssistant_Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPlatformAdministrator",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AiConversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiConversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiConversations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiInstallationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    DailyTokenLimit = table.Column<long>(type: "bigint", nullable: false),
                    OrganizationTokenLimit = table.Column<long>(type: "bigint", nullable: false),
                    UserTokenLimit = table.Column<long>(type: "bigint", nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiInstallationSettings", x => x.Id);
                    table.CheckConstraint("CK_AiInstallationSettings_Singleton", "[Id] = 1");
                });

            migrationBuilder.CreateTable(
                name: "AiProviderConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EncryptedSecret = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecretSuffix = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    InputPrice = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: true),
                    OutputPrice = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TestSucceeded = table.Column<bool>(type: "bit", nullable: false),
                    SupportsTools = table.Column<bool>(type: "bit", nullable: false),
                    TestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LatencyMs = table.Column<int>(type: "int", nullable: true),
                    TestMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProviderConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiUsageRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Feature = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    ReservedTokens = table.Column<long>(type: "bigint", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsageRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationAiSettings",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    DailyTokenLimit = table.Column<long>(type: "bigint", nullable: true),
                    UserTokenLimit = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationAiSettings", x => x.OrganizationId);
                    table.ForeignKey(
                        name: "FK_OrganizationAiSettings_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourcesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ToolsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiMessages_AiConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "AiConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiConversations_OrganizationId_UserId_CreatedAt",
                table: "AiConversations",
                columns: new[] { "OrganizationId", "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiMessages_ConversationId_CreatedAt",
                table: "AiMessages",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiProviderConnections_Purpose",
                table: "AiProviderConnections",
                column: "Purpose",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageRecords_CreatedAt_OrganizationId_UserId_ConnectionId",
                table: "AiUsageRecords",
                columns: new[] { "CreatedAt", "OrganizationId", "UserId", "ConnectionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiInstallationSettings");

            migrationBuilder.DropTable(
                name: "AiMessages");

            migrationBuilder.DropTable(
                name: "AiProviderConnections");

            migrationBuilder.DropTable(
                name: "AiUsageRecords");

            migrationBuilder.DropTable(
                name: "OrganizationAiSettings");

            migrationBuilder.DropTable(
                name: "AiConversations");

            migrationBuilder.DropColumn(
                name: "IsPlatformAdministrator",
                table: "AspNetUsers");
        }
    }
}
