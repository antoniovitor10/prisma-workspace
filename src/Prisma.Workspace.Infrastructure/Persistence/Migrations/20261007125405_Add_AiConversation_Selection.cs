using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prisma.Workspace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_AiConversation_Selection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SelectedConnectionId",
                table: "AiConversations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedModel",
                table: "AiConversations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SelectedConnectionId",
                table: "AiConversations");

            migrationBuilder.DropColumn(
                name: "SelectedModel",
                table: "AiConversations");
        }
    }
}
