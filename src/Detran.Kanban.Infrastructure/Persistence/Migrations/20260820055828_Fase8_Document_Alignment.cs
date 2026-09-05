using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detran.Kanban.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Fase8_Document_Alignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskEvents_WorkItemId",
                table: "TaskEvents");

            migrationBuilder.DropIndex(
                name: "IX_StageHistories_WorkItemId",
                table: "StageHistories");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "WorkflowStatuses",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationWorkflowStatusId",
                table: "WorkflowStatuses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Payload",
                table: "TaskEvents",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorId",
                table: "StageHistories",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorName",
                table: "StageHistories",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "StageHistories",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkflowInheritanceMode",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowTemplateId",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "WorkflowTemplateVersion",
                table: "Projects",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "OrganizationMembers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrganizationWorkflowTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationWorkflowTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationWorkflowTemplates_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationWorkflowStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    Position = table.Column<double>(type: "float", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    IsInitial = table.Column<bool>(type: "bit", nullable: false),
                    IsFinal = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationWorkflowStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationWorkflowStatuses_OrganizationWorkflowTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "OrganizationWorkflowTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationWorkflowTransitions",
                columns: table => new
                {
                    SourceStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationWorkflowTransitions", x => new { x.SourceStatusId, x.TargetStatusId });
                    table.ForeignKey(
                        name: "FK_OrganizationWorkflowTransitions_OrganizationWorkflowStatuses_SourceStatusId",
                        column: x => x.SourceStatusId,
                        principalTable: "OrganizationWorkflowStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationWorkflowTransitions_OrganizationWorkflowStatuses_TargetStatusId",
                        column: x => x.TargetStatusId,
                        principalTable: "OrganizationWorkflowStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationWorkflowTransitions_OrganizationWorkflowTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "OrganizationWorkflowTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStatuses_OrganizationWorkflowStatusId",
                table: "WorkflowStatuses",
                column: "OrganizationWorkflowStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStatuses_ProjectId_OrganizationWorkflowStatusId",
                table: "WorkflowStatuses",
                columns: new[] { "ProjectId", "OrganizationWorkflowStatusId" },
                unique: true,
                filter: "[OrganizationWorkflowStatusId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TaskEvents_WorkItemId_CreatedAt",
                table: "TaskEvents",
                columns: new[] { "WorkItemId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StageHistories_WorkItemId_EnteredAt",
                table: "StageHistories",
                columns: new[] { "WorkItemId", "EnteredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_WorkflowTemplateId",
                table: "Projects",
                column: "WorkflowTemplateId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Projects_WorkflowInheritance",
                table: "Projects",
                sql: "([WorkflowInheritanceMode] = 1 AND [WorkflowTemplateId] IS NULL) OR ([WorkflowInheritanceMode] = 2 AND [WorkflowTemplateId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationWorkflowStatuses_TemplateId_Key",
                table: "OrganizationWorkflowStatuses",
                columns: new[] { "TemplateId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationWorkflowTemplates_OrganizationId",
                table: "OrganizationWorkflowTemplates",
                column: "OrganizationId",
                unique: true,
                filter: "[IsDefault] = 1 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationWorkflowTemplates_OrganizationId_Name",
                table: "OrganizationWorkflowTemplates",
                columns: new[] { "OrganizationId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationWorkflowTransitions_TargetStatusId",
                table: "OrganizationWorkflowTransitions",
                column: "TargetStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationWorkflowTransitions_TemplateId_SourceStatusId",
                table: "OrganizationWorkflowTransitions",
                columns: new[] { "TemplateId", "SourceStatusId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_OrganizationWorkflowTemplates_WorkflowTemplateId",
                table: "Projects",
                column: "WorkflowTemplateId",
                principalTable: "OrganizationWorkflowTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowStatuses_OrganizationWorkflowStatuses_OrganizationWorkflowStatusId",
                table: "WorkflowStatuses",
                column: "OrganizationWorkflowStatusId",
                principalTable: "OrganizationWorkflowStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_OrganizationWorkflowTemplates_WorkflowTemplateId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowStatuses_OrganizationWorkflowStatuses_OrganizationWorkflowStatusId",
                table: "WorkflowStatuses");

            migrationBuilder.DropTable(
                name: "OrganizationWorkflowTransitions");

            migrationBuilder.DropTable(
                name: "OrganizationWorkflowStatuses");

            migrationBuilder.DropTable(
                name: "OrganizationWorkflowTemplates");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowStatuses_OrganizationWorkflowStatusId",
                table: "WorkflowStatuses");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowStatuses_ProjectId_OrganizationWorkflowStatusId",
                table: "WorkflowStatuses");

            migrationBuilder.DropIndex(
                name: "IX_TaskEvents_WorkItemId_CreatedAt",
                table: "TaskEvents");

            migrationBuilder.DropIndex(
                name: "IX_StageHistories_WorkItemId_EnteredAt",
                table: "StageHistories");

            migrationBuilder.DropIndex(
                name: "IX_Projects_WorkflowTemplateId",
                table: "Projects");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Projects_WorkflowInheritance",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "WorkflowStatuses");

            migrationBuilder.DropColumn(
                name: "OrganizationWorkflowStatusId",
                table: "WorkflowStatuses");

            migrationBuilder.DropColumn(
                name: "ActorId",
                table: "StageHistories");

            migrationBuilder.DropColumn(
                name: "ActorName",
                table: "StageHistories");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "StageHistories");

            migrationBuilder.DropColumn(
                name: "WorkflowInheritanceMode",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "WorkflowTemplateId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "WorkflowTemplateVersion",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "OrganizationMembers");

            migrationBuilder.AlterColumn<string>(
                name: "Payload",
                table: "TaskEvents",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskEvents_WorkItemId",
                table: "TaskEvents",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StageHistories_WorkItemId",
                table: "StageHistories",
                column: "WorkItemId");
        }
    }
}
