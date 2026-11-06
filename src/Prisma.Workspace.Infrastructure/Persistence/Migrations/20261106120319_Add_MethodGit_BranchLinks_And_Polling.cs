using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prisma.Workspace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_MethodGit_BranchLinks_And_Polling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_GitRepositoryIntegrations_Ids",
                table: "GitRepositoryIntegrations");

            migrationBuilder.AddColumn<string>(
                name: "BranchState",
                table: "TaskGitLinks",
                type: "varchar(16)",
                nullable: false,
                defaultValue: "remote");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSeenAtUtc",
                table: "TaskGitLinks",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "WebhookSecretRef",
                table: "GitRepositoryIntegrations",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512);

            migrationBuilder.AlterColumn<long>(
                name: "InstallationId",
                table: "GitRepositoryIntegrations",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "AppPrivateKeySecretRef",
                table: "GitRepositoryIntegrations",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512);

            migrationBuilder.AlterColumn<long>(
                name: "AppId",
                table: "GitRepositoryIntegrations",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<string>(
                name: "LastPollStatus",
                table: "GitRepositoryIntegrations",
                type: "varchar(64)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastPolledAtUtc",
                table: "GitRepositoryIntegrations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mode",
                table: "GitRepositoryIntegrations",
                type: "varchar(16)",
                nullable: false,
                defaultValue: "app");

            migrationBuilder.AddColumn<Guid>(
                name: "ReadCredentialId",
                table: "GitRepositoryIntegrations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalsCount",
                table: "GitPullRequestObservations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ChangesRequested",
                table: "GitPullRequestObservations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "GitReadCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GitHubOwner = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProtectedValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    TokenPrefix = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitReadCredentials", x => x.Id);
                    table.UniqueConstraint("AK_GitReadCredentials_ProjectId_Id", x => new { x.ProjectId, x.Id });
                    table.CheckConstraint("CK_GitReadCredentials_Revocation", "([RevokedAtUtc] IS NULL AND [RevokedByUserId] IS NULL) OR ([RevokedAtUtc] IS NOT NULL AND [RevokedByUserId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_GitReadCredentials_MethodProjectSettings_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "MethodProjectSettings",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GitReadCredentials_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaskGitLinks_BranchState",
                table: "TaskGitLinks",
                sql: "[BranchState] IN ('local','remote')");

            migrationBuilder.CreateIndex(
                name: "IX_GitRepositoryIntegrations_ProjectId_ReadCredentialId",
                table: "GitRepositoryIntegrations",
                columns: new[] { "ProjectId", "ReadCredentialId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_GitRepositoryIntegrations_Ids",
                table: "GitRepositoryIntegrations",
                sql: "[RepositoryId] > 0 AND ([Mode] = 'poll' OR ([AppId] IS NOT NULL AND [AppId] > 0 AND [InstallationId] IS NOT NULL AND [InstallationId] > 0 AND [AppPrivateKeySecretRef] IS NOT NULL AND [WebhookSecretRef] IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GitRepositoryIntegrations_Mode",
                table: "GitRepositoryIntegrations",
                sql: "[Mode] IN ('app','poll')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GitRepositoryIntegrations_PollCredential",
                table: "GitRepositoryIntegrations",
                sql: "[Mode] = 'app' OR [ReadCredentialId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GitRepositoryIntegrations_PollIds",
                table: "GitRepositoryIntegrations",
                sql: "([AppId] IS NULL OR [AppId] > 0) AND ([InstallationId] IS NULL OR [InstallationId] > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_GitReadCredentials_OrganizationId",
                table: "GitReadCredentials",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_GitReadCredentials_ProjectId_GitHubOwner",
                table: "GitReadCredentials",
                columns: new[] { "ProjectId", "GitHubOwner" },
                unique: true,
                filter: "[RevokedAtUtc] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_GitRepositoryIntegrations_GitReadCredentials_ProjectId_ReadCredentialId",
                table: "GitRepositoryIntegrations",
                columns: new[] { "ProjectId", "ReadCredentialId" },
                principalTable: "GitReadCredentials",
                principalColumns: new[] { "ProjectId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Integração em modo consulta não tem App: reverter apagaria a configuração. Remova-as antes, de forma explícita.
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [GitRepositoryIntegrations] WHERE [Mode] = 'poll') THROW 50001, 'Existem integrações Git em modo consulta; remova-as antes de reverter esta migration.', 1;");

            migrationBuilder.DropForeignKey(
                name: "FK_GitRepositoryIntegrations_GitReadCredentials_ProjectId_ReadCredentialId",
                table: "GitRepositoryIntegrations");

            migrationBuilder.DropTable(
                name: "GitReadCredentials");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaskGitLinks_BranchState",
                table: "TaskGitLinks");

            migrationBuilder.DropIndex(
                name: "IX_GitRepositoryIntegrations_ProjectId_ReadCredentialId",
                table: "GitRepositoryIntegrations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GitRepositoryIntegrations_Ids",
                table: "GitRepositoryIntegrations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GitRepositoryIntegrations_Mode",
                table: "GitRepositoryIntegrations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GitRepositoryIntegrations_PollCredential",
                table: "GitRepositoryIntegrations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GitRepositoryIntegrations_PollIds",
                table: "GitRepositoryIntegrations");

            migrationBuilder.DropColumn(
                name: "BranchState",
                table: "TaskGitLinks");

            migrationBuilder.DropColumn(
                name: "LastSeenAtUtc",
                table: "TaskGitLinks");

            migrationBuilder.DropColumn(
                name: "LastPollStatus",
                table: "GitRepositoryIntegrations");

            migrationBuilder.DropColumn(
                name: "LastPolledAtUtc",
                table: "GitRepositoryIntegrations");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "GitRepositoryIntegrations");

            migrationBuilder.DropColumn(
                name: "ReadCredentialId",
                table: "GitRepositoryIntegrations");

            migrationBuilder.DropColumn(
                name: "ApprovalsCount",
                table: "GitPullRequestObservations");

            migrationBuilder.DropColumn(
                name: "ChangesRequested",
                table: "GitPullRequestObservations");

            migrationBuilder.AlterColumn<string>(
                name: "WebhookSecretRef",
                table: "GitRepositoryIntegrations",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "InstallationId",
                table: "GitRepositoryIntegrations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AppPrivateKeySecretRef",
                table: "GitRepositoryIntegrations",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "AppId",
                table: "GitRepositoryIntegrations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_GitRepositoryIntegrations_Ids",
                table: "GitRepositoryIntegrations",
                sql: "[RepositoryId] > 0 AND [AppId] > 0 AND [InstallationId] > 0");
        }
    }
}
