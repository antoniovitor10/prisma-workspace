using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prisma.Workspace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Remove_Stage_WipLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WipLimit",
                table: "Stages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WipLimit",
                table: "Stages",
                type: "int",
                nullable: true);
        }
    }
}
