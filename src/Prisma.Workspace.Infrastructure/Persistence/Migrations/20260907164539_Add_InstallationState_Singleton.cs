using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prisma.Workspace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_InstallationState_Singleton : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InstallationStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    IsInitialized = table.Column<bool>(type: "bit", nullable: false),
                    InitializedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstallationStates", x => x.Id);
                    table.CheckConstraint("CK_InstallationStates_Singleton", "[Id] = 1");
                });

            migrationBuilder.Sql(
                """
                DECLARE @LegacyOrganizationId uniqueidentifier = '11111111-1111-4111-8111-111111111111';
                DECLARE @IsLegacyEmpty bit = CASE WHEN
                    NOT EXISTS (SELECT 1 FROM [AspNetUsers]) AND
                    NOT EXISTS (SELECT 1 FROM [OrganizationMembers]) AND
                    NOT EXISTS (SELECT 1 FROM [Organizations] WHERE [Id] <> @LegacyOrganizationId OR [Slug] <> 'detran-se') AND
                    NOT EXISTS (SELECT 1 FROM [Boards]) AND
                    NOT EXISTS (SELECT 1 FROM [Projects]) AND
                    NOT EXISTS (SELECT 1 FROM [WorkItems]) AND
                    NOT EXISTS (SELECT 1 FROM [Teams]) AND
                    NOT EXISTS (SELECT 1 FROM [Clients]) AND
                    NOT EXISTS (SELECT 1 FROM [TaskTypes]) AND
                    NOT EXISTS (SELECT 1 FROM [Tags])
                    THEN 1 ELSE 0 END;

                IF @IsLegacyEmpty = 1
                    DELETE FROM [Organizations] WHERE [Id] = @LegacyOrganizationId AND [Slug] = 'detran-se';

                INSERT INTO [InstallationStates] ([Id], [IsInitialized], [InitializedAt])
                VALUES (
                    1,
                    CASE WHEN @IsLegacyEmpty = 1 THEN 0 ELSE 1 END,
                    CASE WHEN @IsLegacyEmpty = 1 THEN NULL ELSE SYSDATETIMEOFFSET() END
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstallationStates");
        }
    }
}
