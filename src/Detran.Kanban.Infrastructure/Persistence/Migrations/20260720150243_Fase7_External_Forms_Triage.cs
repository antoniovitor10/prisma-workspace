using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detran.Kanban.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Fase7_External_Forms_Triage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "ExternalRequests",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExternalFormId",
                table: "ExternalRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelatedService",
                table: "ExternalRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequesterPhone",
                table: "ExternalRequests",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmittedValuesJson",
                table: "ExternalRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TriageStatus",
                table: "ExternalRequests",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "ExternalForms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalPortalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicSlug = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ConfirmationMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    DefaultPriority = table.Column<int>(type: "int", nullable: false),
                    InitialStageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefaultTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefaultResponsibleId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    MaxFiles = table.Column<int>(type: "int", nullable: false),
                    MaxFileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    AllowedExtensions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AllowedMimeTypes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    MinimumCompletionSeconds = table.Column<int>(type: "int", nullable: false),
                    FieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssignmentRulesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalForms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalForms_ExternalPortals_ExternalPortalId",
                        column: x => x.ExternalPortalId,
                        principalTable: "ExternalPortals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExternalForms_Stages_InitialStageId",
                        column: x => x.InitialStageId,
                        principalTable: "Stages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExternalForms_Teams_DefaultTeamId",
                        column: x => x.DefaultTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ExternalRequestTriageEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalRequestTriageEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalRequestTriageEvents_ExternalRequests_ExternalRequestId",
                        column: x => x.ExternalRequestId,
                        principalTable: "ExternalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO [ExternalForms]
                    ([Id], [ExternalPortalId], [PublicSlug], [Title], [Description], [Category],
                     [ConfirmationMessage], [IsEnabled], [IsDefault], [DefaultPriority],
                     [InitialStageId], [DefaultTeamId], [DefaultResponsibleId], [MaxFiles],
                     [MaxFileSizeBytes], [AllowedExtensions], [AllowedMimeTypes],
                     [MinimumCompletionSeconds], [FieldsJson], [AssignmentRulesJson],
                     [CreatedAt], [UpdatedAt])
                SELECT NEWID(), ep.[Id], N'solicitacao',
                       LEFT(CONCAT(N'Solicitação — ', p.[Name]), 200),
                       N'Descreva sua necessidade para que nossa equipe possa iniciar o atendimento.',
                       NULL,
                       N'Recebemos sua solicitação. Guarde o protocolo e a chave de acompanhamento.',
                       1, 1, 1, NULL, NULL, NULL, 5, 10000000,
                       N'.pdf,.png,.jpg,.jpeg,.doc,.docx,.xls,.xlsx',
                       N'application/pdf,image/png,image/jpeg,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/vnd.ms-excel,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
                       2,
                       N'[{"key":"requesterName","label":"Nome","type":1,"kind":5,"isRequired":true,"position":0,"maxLength":200},{"key":"requesterEmail","label":"E-mail","type":3,"kind":6,"isRequired":true,"position":1,"maxLength":320},{"key":"requesterPhone","label":"Telefone","type":4,"kind":7,"isRequired":false,"position":2,"maxLength":30},{"key":"subject","label":"Assunto","type":1,"kind":1,"isRequired":true,"position":3,"maxLength":500},{"key":"description","label":"Descrição detalhada","type":2,"kind":2,"isRequired":true,"position":4,"maxLength":10000},{"key":"category","label":"Categoria","type":1,"kind":3,"isRequired":false,"position":5,"maxLength":120},{"key":"priority","label":"Prioridade informada","type":5,"kind":4,"isRequired":false,"position":6,"options":["Baixa","Média","Alta","Crítica"]},{"key":"relatedService","label":"Serviço relacionado","type":1,"kind":8,"isRequired":false,"position":7,"maxLength":200},{"key":"attachments","label":"Anexos","type":9,"kind":9,"isRequired":false,"position":8}]',
                       N'[]', SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM [ExternalPortals] ep
                INNER JOIN [Projects] p ON p.[Id] = ep.[ProjectId]
                WHERE NOT EXISTS (
                    SELECT 1 FROM [ExternalForms] existing
                    WHERE existing.[ExternalPortalId] = ep.[Id]);

                UPDATE er
                SET er.[ExternalFormId] = form.[Id],
                    er.[SubmittedValuesJson] = (
                        SELECT wi.[RequesterName] AS [requesterName],
                               er.[RequesterEmail] AS [requesterEmail],
                               wi.[Title] AS [subject],
                               wi.[Description] AS [description]
                        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
                FROM [ExternalRequests] er
                INNER JOIN [ExternalForms] form
                    ON form.[ExternalPortalId] = er.[ExternalPortalId] AND form.[IsDefault] = 1
                INNER JOIN [WorkItems] wi ON wi.[Id] = er.[WorkItemId]
                WHERE er.[ExternalFormId] IS NULL;

                INSERT INTO [ExternalRequestTriageEvents]
                    ([Id], [ExternalRequestId], [Action], [ActorId], [ActorName],
                     [Description], [DataJson], [CreatedAt])
                SELECT NEWID(), er.[Id], 1,
                       LEFT(CONCAT(N'external:', er.[RequesterEmail]), 450),
                       COALESCE(NULLIF(wi.[RequesterName], N''), N'Solicitante'),
                       N'Solicitação recebida e convertida em tarefa interna.', NULL, er.[CreatedAt]
                FROM [ExternalRequests] er
                INNER JOIN [WorkItems] wi ON wi.[Id] = er.[WorkItemId]
                WHERE NOT EXISTS (
                    SELECT 1 FROM [ExternalRequestTriageEvents] evt
                    WHERE evt.[ExternalRequestId] = er.[Id] AND evt.[Action] = 1);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRequests_ExternalFormId",
                table: "ExternalRequests",
                column: "ExternalFormId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalForms_DefaultTeamId",
                table: "ExternalForms",
                column: "DefaultTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalForms_ExternalPortalId_IsDefault",
                table: "ExternalForms",
                columns: new[] { "ExternalPortalId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalForms_ExternalPortalId_PublicSlug",
                table: "ExternalForms",
                columns: new[] { "ExternalPortalId", "PublicSlug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalForms_InitialStageId",
                table: "ExternalForms",
                column: "InitialStageId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRequestTriageEvents_ExternalRequestId_CreatedAt",
                table: "ExternalRequestTriageEvents",
                columns: new[] { "ExternalRequestId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalRequests_ExternalForms_ExternalFormId",
                table: "ExternalRequests",
                column: "ExternalFormId",
                principalTable: "ExternalForms",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExternalRequests_ExternalForms_ExternalFormId",
                table: "ExternalRequests");

            migrationBuilder.DropTable(
                name: "ExternalForms");

            migrationBuilder.DropTable(
                name: "ExternalRequestTriageEvents");

            migrationBuilder.DropIndex(
                name: "IX_ExternalRequests_ExternalFormId",
                table: "ExternalRequests");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "ExternalRequests");

            migrationBuilder.DropColumn(
                name: "ExternalFormId",
                table: "ExternalRequests");

            migrationBuilder.DropColumn(
                name: "RelatedService",
                table: "ExternalRequests");

            migrationBuilder.DropColumn(
                name: "RequesterPhone",
                table: "ExternalRequests");

            migrationBuilder.DropColumn(
                name: "SubmittedValuesJson",
                table: "ExternalRequests");

            migrationBuilder.DropColumn(
                name: "TriageStatus",
                table: "ExternalRequests");
        }
    }
}
