using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detran.Kanban.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Fase6D_Projects_WorkItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "WorkItemNumbers",
                startValue: 1000L);

            migrationBuilder.AddColumn<string>(
                name: "AcceptanceCriteria",
                table: "WorkItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                table: "WorkItems",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "WorkItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "Number",
                table: "WorkItems",
                type: "bigint",
                nullable: false,
                defaultValueSql: "NEXT VALUE FOR [WorkItemNumbers]");

            migrationBuilder.AddColumn<int>(
                name: "Origin",
                table: "WorkItems",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "RequesterEmail",
                table: "WorkItems",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequesterId",
                table: "WorkItems",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequesterName",
                table: "WorkItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsibleId",
                table: "WorkItems",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                table: "WorkItems",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "WorkItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                table: "Projects",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                table: "Projects",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Methodology",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "SettingsJson",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                table: "Projects",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Projects",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.Sql("UPDATE [Projects] SET [UpdatedAt] = [CreatedAt]");
            migrationBuilder.Sql("""
                UPDATE wi
                SET wi.[TeamId] = b.[TeamId]
                FROM [WorkItems] wi
                INNER JOIN [Boards] b ON b.[Id] = wi.[BoardId]
                WHERE wi.[TeamId] IS NULL AND b.[TeamId] IS NOT NULL;
                """);
            migrationBuilder.Sql("""
                UPDATE wi
                SET wi.[ResponsibleId] = assigned.[UserId]
                FROM [WorkItems] wi
                CROSS APPLY (
                    SELECT TOP (1) a.[UserId]
                    FROM [WorkItemAssignees] a
                    WHERE a.[WorkItemId] = wi.[Id]
                    ORDER BY a.[AssignedAt], a.[UserId]
                ) assigned
                WHERE wi.[ResponsibleId] IS NULL;
                """);

            migrationBuilder.CreateTable(
                name: "ProjectCustomFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    OptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Position = table.Column<double>(type: "float", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectCustomFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectCustomFields_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectEvents_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTags",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTags", x => new { x.ProjectId, x.TagId });
                    table.ForeignKey(
                        name: "FK_ProjectTags_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemFollowers",
                columns: table => new
                {
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    FollowedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemFollowers", x => new { x.WorkItemId, x.UserId });
                    table.ForeignKey(
                        name: "FK_WorkItemFollowers_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceWorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetWorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemLinks_WorkItems_SourceWorkItemId",
                        column: x => x.SourceWorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItemLinks_WorkItems_TargetWorkItemId",
                        column: x => x.TargetWorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemCustomFieldValues",
                columns: table => new
                {
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemCustomFieldValues", x => new { x.WorkItemId, x.FieldDefinitionId });
                    table.ForeignKey(
                        name: "FK_WorkItemCustomFieldValues_ProjectCustomFields_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "ProjectCustomFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItemCustomFieldValues_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_BoardId_IsArchived",
                table: "WorkItems",
                columns: new[] { "BoardId", "IsArchived" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Number",
                table: "WorkItems",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ResponsibleId",
                table: "WorkItems",
                column: "ResponsibleId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_TeamId",
                table: "WorkItems",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OrganizationId_IsArchived_Status",
                table: "Projects",
                columns: new[] { "OrganizationId", "IsArchived", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCustomFields_ProjectId_IsActive_Position",
                table: "ProjectCustomFields",
                columns: new[] { "ProjectId", "IsActive", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectEvents_ProjectId_CreatedAt",
                table: "ProjectEvents",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTags_TagId",
                table: "ProjectTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemCustomFieldValues_FieldDefinitionId",
                table: "WorkItemCustomFieldValues",
                column: "FieldDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemFollowers_UserId",
                table: "WorkItemFollowers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemLinks_SourceWorkItemId_TargetWorkItemId_Type",
                table: "WorkItemLinks",
                columns: new[] { "SourceWorkItemId", "TargetWorkItemId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemLinks_TargetWorkItemId",
                table: "WorkItemLinks",
                column: "TargetWorkItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_Teams_TeamId",
                table: "WorkItems",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_Teams_TeamId",
                table: "WorkItems");

            migrationBuilder.DropTable(
                name: "ProjectEvents");

            migrationBuilder.DropTable(
                name: "ProjectTags");

            migrationBuilder.DropTable(
                name: "WorkItemCustomFieldValues");

            migrationBuilder.DropTable(
                name: "WorkItemFollowers");

            migrationBuilder.DropTable(
                name: "WorkItemLinks");

            migrationBuilder.DropTable(
                name: "ProjectCustomFields");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_BoardId_IsArchived",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Number",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_ResponsibleId",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_TeamId",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_Projects_OrganizationId_IsArchived_Status",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AcceptanceCriteria",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "RequesterEmail",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "RequesterId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "RequesterName",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "ResponsibleId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Methodology",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SettingsJson",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Projects");

            migrationBuilder.DropSequence(
                name: "WorkItemNumbers");
        }
    }
}
