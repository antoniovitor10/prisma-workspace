using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prisma.Workspace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_MethodGit_Security_And_Webhook_Tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MethodPrincipals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<byte>(type: "tinyint", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HumanUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DelegatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PermissionCeilingJson = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MethodPrincipals", x => x.Id);
                    table.UniqueConstraint("AK_MethodPrincipals_ProjectId_Id", x => new { x.ProjectId, x.Id });
                    table.CheckConstraint("CK_MethodPrincipals_Human", "([Role] = 1 AND [HumanUserId] IS NOT NULL AND [HumanUserId] = [DelegatedByUserId]) OR ([Role] IN (2,3) AND [HumanUserId] IS NULL)");
                    table.CheckConstraint("CK_MethodPrincipals_Role", "[Role] IN (1,2,3)");
                    table.ForeignKey(
                        name: "FK_MethodPrincipals_AspNetUsers_DelegatedByUserId",
                        column: x => x.DelegatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodPrincipals_AspNetUsers_HumanUserId",
                        column: x => x.HumanUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodPrincipals_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodPrincipals_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MethodProjectSettings",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BoardId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MethodIdFieldId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HuFieldId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrFieldId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    GitPhase = table.Column<byte>(type: "tinyint", nullable: false),
                    AutonomyLevel = table.Column<int>(type: "int", nullable: false),
                    PolicyVersion = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MethodProjectSettings", x => x.ProjectId);
                    table.CheckConstraint("CK_MethodProjectSettings_Autonomy", "[AutonomyLevel] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_MethodProjectSettings_Phase", "[GitPhase] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_MethodProjectSettings_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodProjectSettings_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodProjectSettings_ProjectCustomFields_HuFieldId",
                        column: x => x.HuFieldId,
                        principalTable: "ProjectCustomFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodProjectSettings_ProjectCustomFields_MethodIdFieldId",
                        column: x => x.MethodIdFieldId,
                        principalTable: "ProjectCustomFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodProjectSettings_ProjectCustomFields_PrFieldId",
                        column: x => x.PrFieldId,
                        principalTable: "ProjectCustomFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodProjectSettings_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MethodApiTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TokenHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    TokenPrefix = table.Column<string>(type: "varchar(24)", nullable: false),
                    IssuedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    LastUsedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MethodApiTokens", x => x.Id);
                    table.CheckConstraint("CK_MethodApiTokens_Expiry", "[ExpiresAtUtc] > [IssuedAtUtc]");
                    table.ForeignKey(
                        name: "FK_MethodApiTokens_AspNetUsers_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodApiTokens_AspNetUsers_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodApiTokens_MethodPrincipals_ProjectId_PrincipalId",
                        columns: x => new { x.ProjectId, x.PrincipalId },
                        principalTable: "MethodPrincipals",
                        principalColumns: new[] { "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodApiTokens_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MethodAuthorizationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventKind = table.Column<string>(type: "varchar(24)", nullable: false),
                    RelatedEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorPrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TaskEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MethodAuthorizationEvents", x => x.Id);
                    table.UniqueConstraint("AK_MethodAuthorizationEvents_ProjectId_WorkItemId_CycleId_Id", x => new { x.ProjectId, x.WorkItemId, x.CycleId, x.Id });
                    table.CheckConstraint("CK_MethodAuthorizationEvents_Actor", "[ActorPrincipalId] IS NOT NULL OR [ActorUserId] IS NOT NULL");
                    table.CheckConstraint("CK_MethodAuthorizationEvents_Kind", "[EventKind] IN ('Granted','Revoked','Blocked','ResumeAllowed','Resumed')");
                    table.ForeignKey(
                        name: "FK_MethodAuthorizationEvents_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodAuthorizationEvents_MethodAuthorizationEvents_ProjectId_WorkItemId_CycleId_RelatedEventId",
                        columns: x => new { x.ProjectId, x.WorkItemId, x.CycleId, x.RelatedEventId },
                        principalTable: "MethodAuthorizationEvents",
                        principalColumns: new[] { "ProjectId", "WorkItemId", "CycleId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodAuthorizationEvents_MethodPrincipals_ProjectId_ActorPrincipalId",
                        columns: x => new { x.ProjectId, x.ActorPrincipalId },
                        principalTable: "MethodPrincipals",
                        principalColumns: new[] { "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodAuthorizationEvents_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodAuthorizationEvents_TaskEvents_TaskEventId",
                        column: x => x.TaskEventId,
                        principalTable: "TaskEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodAuthorizationEvents_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GitRepositoryIntegrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "varchar(16)", nullable: false),
                    Host = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    RepositoryId = table.Column<long>(type: "bigint", nullable: false),
                    RepositoryFullName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DefaultBranch = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    AppId = table.Column<long>(type: "bigint", nullable: false),
                    InstallationId = table.Column<long>(type: "bigint", nullable: false),
                    AppPrivateKeySecretRef = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    WebhookSecretRef = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    PreviousWebhookSecretRef = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PreviousSecretValidUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SystemPrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitRepositoryIntegrations", x => x.Id);
                    table.UniqueConstraint("AK_GitRepositoryIntegrations_ProjectId_Id", x => new { x.ProjectId, x.Id });
                    table.CheckConstraint("CK_GitRepositoryIntegrations_Ids", "[RepositoryId] > 0 AND [AppId] > 0 AND [InstallationId] > 0");
                    table.CheckConstraint("CK_GitRepositoryIntegrations_Provider", "[Provider] = 'github'");
                    table.CheckConstraint("CK_GitRepositoryIntegrations_Rotation", "([PreviousWebhookSecretRef] IS NULL AND [PreviousSecretValidUntilUtc] IS NULL) OR ([PreviousWebhookSecretRef] IS NOT NULL AND [PreviousSecretValidUntilUtc] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_GitRepositoryIntegrations_MethodPrincipals_ProjectId_SystemPrincipalId",
                        columns: x => new { x.ProjectId, x.SystemPrincipalId },
                        principalTable: "MethodPrincipals",
                        principalColumns: new[] { "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GitRepositoryIntegrations_MethodProjectSettings_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "MethodProjectSettings",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GitRepositoryIntegrations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MethodStageMappings",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatusCode = table.Column<string>(type: "varchar(32)", nullable: false),
                    StageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MethodStageMappings", x => new { x.ProjectId, x.StatusCode });
                    table.CheckConstraint("CK_MethodStageMappings_Status", "[StatusCode] IN ('backlog','pronta','em-desenvolvimento','em-revisao','em-teste','concluida','bloqueada')");
                    table.ForeignKey(
                        name: "FK_MethodStageMappings_MethodProjectSettings_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "MethodProjectSettings",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodStageMappings_Stages_StageId",
                        column: x => x.StageId,
                        principalTable: "Stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MethodTaskStates",
                columns: table => new
                {
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MethodTaskId = table.Column<string>(type: "varchar(32)", nullable: true),
                    CycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActiveAuthorizationEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BlockingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BlockingPrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResumeApprovalEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastObservedFactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MethodTaskStates", x => new { x.ProjectId, x.WorkItemId });
                    table.ForeignKey(
                        name: "FK_MethodTaskStates_MethodAuthorizationEvents_ProjectId_WorkItemId_CycleId_ActiveAuthorizationEventId",
                        columns: x => new { x.ProjectId, x.WorkItemId, x.CycleId, x.ActiveAuthorizationEventId },
                        principalTable: "MethodAuthorizationEvents",
                        principalColumns: new[] { "ProjectId", "WorkItemId", "CycleId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodTaskStates_MethodAuthorizationEvents_ProjectId_WorkItemId_CycleId_BlockingEventId",
                        columns: x => new { x.ProjectId, x.WorkItemId, x.CycleId, x.BlockingEventId },
                        principalTable: "MethodAuthorizationEvents",
                        principalColumns: new[] { "ProjectId", "WorkItemId", "CycleId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodTaskStates_MethodAuthorizationEvents_ProjectId_WorkItemId_CycleId_ResumeApprovalEventId",
                        columns: x => new { x.ProjectId, x.WorkItemId, x.CycleId, x.ResumeApprovalEventId },
                        principalTable: "MethodAuthorizationEvents",
                        principalColumns: new[] { "ProjectId", "WorkItemId", "CycleId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodTaskStates_MethodPrincipals_ProjectId_BlockingPrincipalId",
                        columns: x => new { x.ProjectId, x.BlockingPrincipalId },
                        principalTable: "MethodPrincipals",
                        principalColumns: new[] { "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodTaskStates_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MethodTaskStates_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GitPullRequestObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PullRequestNumber = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    HeadBranch = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    BaseBranch = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    HeadSha = table.Column<string>(type: "varchar(64)", nullable: false),
                    MergeSha = table.Column<string>(type: "varchar(64)", nullable: true),
                    HeadRepositoryId = table.Column<long>(type: "bigint", nullable: true),
                    PullRequestAuthorId = table.Column<string>(type: "varchar(32)", nullable: true),
                    PullRequestAuthorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    State = table.Column<string>(type: "varchar(16)", nullable: false),
                    IsDraft = table.Column<bool>(type: "bit", nullable: false),
                    ProviderUpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UnlinkedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitPullRequestObservations", x => x.Id);
                    table.UniqueConstraint("AK_GitPullRequestObservations_ProjectId_IntegrationId_Id", x => new { x.ProjectId, x.IntegrationId, x.Id });
                    table.CheckConstraint("CK_GitPullRequestObservations_Number", "[PullRequestNumber] > 0");
                    table.ForeignKey(
                        name: "FK_GitPullRequestObservations_GitRepositoryIntegrations_ProjectId_IntegrationId",
                        columns: x => new { x.ProjectId, x.IntegrationId },
                        principalTable: "GitRepositoryIntegrations",
                        principalColumns: new[] { "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GitWebhookDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "varchar(16)", nullable: false),
                    DeliveryId = table.Column<string>(type: "varchar(100)", nullable: false),
                    BodyHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    EventName = table.Column<string>(type: "varchar(64)", nullable: false),
                    EventAction = table.Column<string>(type: "varchar(64)", nullable: true),
                    NormalizedEventJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "varchar(24)", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LeaseUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastErrorCode = table.Column<string>(type: "varchar(64)", nullable: true),
                    CorrelationId = table.Column<string>(type: "varchar(64)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitWebhookDeliveries", x => x.Id);
                    table.CheckConstraint("CK_GitWebhookDeliveries_Attempts", "[Attempts] >= 0");
                    table.ForeignKey(
                        name: "FK_GitWebhookDeliveries_GitRepositoryIntegrations_ProjectId_IntegrationId",
                        columns: x => new { x.ProjectId, x.IntegrationId },
                        principalTable: "GitRepositoryIntegrations",
                        principalColumns: new[] { "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GitAppliedFacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SemanticKey = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    FactType = table.Column<string>(type: "varchar(40)", nullable: false),
                    ProviderEntityId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderRevision = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FromStageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToStageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppliedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitAppliedFacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GitAppliedFacts_GitRepositoryIntegrations_ProjectId_IntegrationId",
                        columns: x => new { x.ProjectId, x.IntegrationId },
                        principalTable: "GitRepositoryIntegrations",
                        principalColumns: new[] { "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GitAppliedFacts_MethodTaskStates_ProjectId_WorkItemId",
                        columns: x => new { x.ProjectId, x.WorkItemId },
                        principalTable: "MethodTaskStates",
                        principalColumns: new[] { "ProjectId", "WorkItemId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GitAppliedFacts_Stages_FromStageId",
                        column: x => x.FromStageId,
                        principalTable: "Stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GitAppliedFacts_Stages_ToStageId",
                        column: x => x.ToStageId,
                        principalTable: "Stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GitAppliedFacts_TaskEvents_TaskEventId",
                        column: x => x.TaskEventId,
                        principalTable: "TaskEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskGitLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PullRequestObservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BranchName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Source = table.Column<string>(type: "varchar(16)", nullable: false),
                    CreatedByPrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskGitLinks", x => x.Id);
                    table.CheckConstraint("CK_TaskGitLinks_Source", "[Source] IN ('explicit','branch','title')");
                    table.CheckConstraint("CK_TaskGitLinks_Target", "[PullRequestObservationId] IS NOT NULL OR [BranchName] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_TaskGitLinks_GitPullRequestObservations_ProjectId_IntegrationId_PullRequestObservationId",
                        columns: x => new { x.ProjectId, x.IntegrationId, x.PullRequestObservationId },
                        principalTable: "GitPullRequestObservations",
                        principalColumns: new[] { "ProjectId", "IntegrationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskGitLinks_GitRepositoryIntegrations_ProjectId_IntegrationId",
                        columns: x => new { x.ProjectId, x.IntegrationId },
                        principalTable: "GitRepositoryIntegrations",
                        principalColumns: new[] { "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskGitLinks_MethodPrincipals_ProjectId_CreatedByPrincipalId",
                        columns: x => new { x.ProjectId, x.CreatedByPrincipalId },
                        principalTable: "MethodPrincipals",
                        principalColumns: new[] { "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskGitLinks_MethodTaskStates_ProjectId_WorkItemId",
                        columns: x => new { x.ProjectId, x.WorkItemId },
                        principalTable: "MethodTaskStates",
                        principalColumns: new[] { "ProjectId", "WorkItemId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GitAppliedFacts_FromStageId",
                table: "GitAppliedFacts",
                column: "FromStageId");

            migrationBuilder.CreateIndex(
                name: "IX_GitAppliedFacts_IntegrationId_ProviderEntityId",
                table: "GitAppliedFacts",
                columns: new[] { "IntegrationId", "ProviderEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_GitAppliedFacts_ProjectId_IntegrationId",
                table: "GitAppliedFacts",
                columns: new[] { "ProjectId", "IntegrationId" });

            migrationBuilder.CreateIndex(
                name: "IX_GitAppliedFacts_ProjectId_WorkItemId_SemanticKey",
                table: "GitAppliedFacts",
                columns: new[] { "ProjectId", "WorkItemId", "SemanticKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitAppliedFacts_TaskEventId",
                table: "GitAppliedFacts",
                column: "TaskEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitAppliedFacts_ToStageId",
                table: "GitAppliedFacts",
                column: "ToStageId");

            migrationBuilder.CreateIndex(
                name: "IX_GitPullRequestObservations_IntegrationId_PullRequestNumber",
                table: "GitPullRequestObservations",
                columns: new[] { "IntegrationId", "PullRequestNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitPullRequestObservations_ProjectId_State_ProviderUpdatedAtUtc",
                table: "GitPullRequestObservations",
                columns: new[] { "ProjectId", "State", "ProviderUpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GitRepositoryIntegrations_OrganizationId",
                table: "GitRepositoryIntegrations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_GitRepositoryIntegrations_ProjectId_Enabled",
                table: "GitRepositoryIntegrations",
                columns: new[] { "ProjectId", "Enabled" });

            migrationBuilder.CreateIndex(
                name: "IX_GitRepositoryIntegrations_ProjectId_SystemPrincipalId",
                table: "GitRepositoryIntegrations",
                columns: new[] { "ProjectId", "SystemPrincipalId" });

            migrationBuilder.CreateIndex(
                name: "IX_GitRepositoryIntegrations_Provider_Host_RepositoryId",
                table: "GitRepositoryIntegrations",
                columns: new[] { "Provider", "Host", "RepositoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitWebhookDeliveries_IntegrationId_Provider_DeliveryId",
                table: "GitWebhookDeliveries",
                columns: new[] { "IntegrationId", "Provider", "DeliveryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitWebhookDeliveries_ProcessedAtUtc",
                table: "GitWebhookDeliveries",
                column: "ProcessedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_GitWebhookDeliveries_ProjectId_IntegrationId",
                table: "GitWebhookDeliveries",
                columns: new[] { "ProjectId", "IntegrationId" });

            migrationBuilder.CreateIndex(
                name: "IX_GitWebhookDeliveries_Status_NextAttemptAtUtc_LeaseUntilUtc",
                table: "GitWebhookDeliveries",
                columns: new[] { "Status", "NextAttemptAtUtc", "LeaseUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MethodApiTokens_IssuedByUserId",
                table: "MethodApiTokens",
                column: "IssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MethodApiTokens_ProjectId_PrincipalId_RevokedAtUtc_ExpiresAtUtc",
                table: "MethodApiTokens",
                columns: new[] { "ProjectId", "PrincipalId", "RevokedAtUtc", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MethodApiTokens_RevokedByUserId",
                table: "MethodApiTokens",
                column: "RevokedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MethodApiTokens_TokenHash",
                table: "MethodApiTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MethodAuthorizationEvents_ActorUserId",
                table: "MethodAuthorizationEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MethodAuthorizationEvents_ProjectId_ActorPrincipalId",
                table: "MethodAuthorizationEvents",
                columns: new[] { "ProjectId", "ActorPrincipalId" });

            migrationBuilder.CreateIndex(
                name: "IX_MethodAuthorizationEvents_ProjectId_WorkItemId_CreatedAtUtc_Id",
                table: "MethodAuthorizationEvents",
                columns: new[] { "ProjectId", "WorkItemId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MethodAuthorizationEvents_ProjectId_WorkItemId_CycleId_RelatedEventId",
                table: "MethodAuthorizationEvents",
                columns: new[] { "ProjectId", "WorkItemId", "CycleId", "RelatedEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_MethodAuthorizationEvents_TaskEventId",
                table: "MethodAuthorizationEvents",
                column: "TaskEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MethodAuthorizationEvents_WorkItemId",
                table: "MethodAuthorizationEvents",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MethodPrincipals_DelegatedByUserId",
                table: "MethodPrincipals",
                column: "DelegatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MethodPrincipals_HumanUserId",
                table: "MethodPrincipals",
                column: "HumanUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MethodPrincipals_OrganizationId",
                table: "MethodPrincipals",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_MethodPrincipals_ProjectId_Role_IsActive",
                table: "MethodPrincipals",
                columns: new[] { "ProjectId", "Role", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MethodProjectSettings_BoardId",
                table: "MethodProjectSettings",
                column: "BoardId",
                unique: true,
                filter: "[BoardId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MethodProjectSettings_HuFieldId",
                table: "MethodProjectSettings",
                column: "HuFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_MethodProjectSettings_MethodIdFieldId",
                table: "MethodProjectSettings",
                column: "MethodIdFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_MethodProjectSettings_OrganizationId_Enabled",
                table: "MethodProjectSettings",
                columns: new[] { "OrganizationId", "Enabled" });

            migrationBuilder.CreateIndex(
                name: "IX_MethodProjectSettings_PrFieldId",
                table: "MethodProjectSettings",
                column: "PrFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_MethodStageMappings_ProjectId_StageId",
                table: "MethodStageMappings",
                columns: new[] { "ProjectId", "StageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MethodStageMappings_StageId",
                table: "MethodStageMappings",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_MethodTaskStates_ProjectId_BlockingPrincipalId",
                table: "MethodTaskStates",
                columns: new[] { "ProjectId", "BlockingPrincipalId" });

            migrationBuilder.CreateIndex(
                name: "IX_MethodTaskStates_ProjectId_MethodTaskId",
                table: "MethodTaskStates",
                columns: new[] { "ProjectId", "MethodTaskId" },
                unique: true,
                filter: "[MethodTaskId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MethodTaskStates_ProjectId_WorkItemId_CycleId_ActiveAuthorizationEventId",
                table: "MethodTaskStates",
                columns: new[] { "ProjectId", "WorkItemId", "CycleId", "ActiveAuthorizationEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_MethodTaskStates_ProjectId_WorkItemId_CycleId_BlockingEventId",
                table: "MethodTaskStates",
                columns: new[] { "ProjectId", "WorkItemId", "CycleId", "BlockingEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_MethodTaskStates_ProjectId_WorkItemId_CycleId_ResumeApprovalEventId",
                table: "MethodTaskStates",
                columns: new[] { "ProjectId", "WorkItemId", "CycleId", "ResumeApprovalEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_MethodTaskStates_WorkItemId",
                table: "MethodTaskStates",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskGitLinks_IntegrationId_BranchName",
                table: "TaskGitLinks",
                columns: new[] { "IntegrationId", "BranchName" },
                unique: true,
                filter: "[PullRequestObservationId] IS NULL AND [BranchName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TaskGitLinks_ProjectId_CreatedByPrincipalId",
                table: "TaskGitLinks",
                columns: new[] { "ProjectId", "CreatedByPrincipalId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskGitLinks_ProjectId_IntegrationId_PullRequestObservationId",
                table: "TaskGitLinks",
                columns: new[] { "ProjectId", "IntegrationId", "PullRequestObservationId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskGitLinks_ProjectId_WorkItemId",
                table: "TaskGitLinks",
                columns: new[] { "ProjectId", "WorkItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskGitLinks_PullRequestObservationId",
                table: "TaskGitLinks",
                column: "PullRequestObservationId",
                unique: true,
                filter: "[PullRequestObservationId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GitAppliedFacts");

            migrationBuilder.DropTable(
                name: "GitWebhookDeliveries");

            migrationBuilder.DropTable(
                name: "MethodApiTokens");

            migrationBuilder.DropTable(
                name: "MethodStageMappings");

            migrationBuilder.DropTable(
                name: "TaskGitLinks");

            migrationBuilder.DropTable(
                name: "GitPullRequestObservations");

            migrationBuilder.DropTable(
                name: "MethodTaskStates");

            migrationBuilder.DropTable(
                name: "GitRepositoryIntegrations");

            migrationBuilder.DropTable(
                name: "MethodAuthorizationEvents");

            migrationBuilder.DropTable(
                name: "MethodProjectSettings");

            migrationBuilder.DropTable(
                name: "MethodPrincipals");
        }
    }
}
