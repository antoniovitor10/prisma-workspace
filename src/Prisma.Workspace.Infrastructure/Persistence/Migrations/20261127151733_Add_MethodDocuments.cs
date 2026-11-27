using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prisma.Workspace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_MethodDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MethodDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    MethodKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentHash = table.Column<string>(type: "char(64)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedVia = table.Column<string>(type: "varchar(8)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MethodDocuments", x => x.Id);
                    table.CheckConstraint("CK_MethodDocuments_Kind", "[Kind] IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_MethodDocuments_Via", "[UpdatedVia] IN ('ui', 'mcp')");
                    table.ForeignKey(
                        name: "FK_MethodDocuments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodDocuments_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MethodDocumentRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentHash = table.Column<string>(type: "char(64)", nullable: false),
                    AuthorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AuthorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Via = table.Column<string>(type: "varchar(8)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MethodDocumentRevisions", x => x.Id);
                    table.CheckConstraint("CK_MethodDocumentRevisions_Via", "[Via] IN ('ui', 'mcp')");
                    table.ForeignKey(
                        name: "FK_MethodDocumentRevisions_MethodDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "MethodDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodDocumentRevisions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MethodDocumentRevisions_DocumentId_Version",
                table: "MethodDocumentRevisions",
                columns: new[] { "DocumentId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MethodDocumentRevisions_ProjectId",
                table: "MethodDocumentRevisions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_MethodDocuments_ProjectId_MethodKey",
                table: "MethodDocuments",
                columns: new[] { "ProjectId", "MethodKey" },
                unique: true,
                filter: "[MethodKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MethodDocuments_ProjectId_UpdatedAtUtc",
                table: "MethodDocuments",
                columns: new[] { "ProjectId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MethodDocuments_WorkItemId_Kind",
                table: "MethodDocuments",
                columns: new[] { "WorkItemId", "Kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MethodDocumentRevisions");

            migrationBuilder.DropTable(
                name: "MethodDocuments");
        }
    }
}
