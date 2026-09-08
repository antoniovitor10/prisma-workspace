using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Prisma.Workspace.Infrastructure;

/// <summary>
/// Extensões de DI para registrar os serviços da camada de Infrastructure.
/// </summary>
public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<Tenancy.CurrentOrganizationContext>();
        services.AddScoped<IOrganizationContext>(sp =>
            sp.GetRequiredService<Tenancy.CurrentOrganizationContext>());
        services.AddScoped<Tenancy.CurrentAuditContext>();
        services.AddScoped<IAuditContext>(sp =>
            sp.GetRequiredService<Tenancy.CurrentAuditContext>());

        // Registra o AppDbContext com SQL Server usando a connection string "DefaultConnection".
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.MigrationsAssembly(
                    typeof(AppDbContext).Assembly.FullName)));

        // ─── Repositórios ─────────────────────────────────────────────────
        services.AddScoped<IBoardRepository, BoardRepository>();
        services.AddScoped<IWorkItemRepository, WorkItemRepository>();
        services.AddScoped<IWorkItemManagementRepository, WorkItemManagementRepository>();
        services.AddScoped<IWorkItemSearchRepository, WorkItemManagementRepository>();
        services.AddScoped<IStageRepository, StageRepository>();
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        services.AddScoped<ITimeEntryRepository, TimeEntryRepository>();
        services.AddScoped<IStageHistoryRepository, StageHistoryRepository>();
        services.AddScoped<ITaskFeedRepository, TaskFeedRepository>();
        services.AddScoped<IApprovalRepository, ApprovalRepository>();
        services.AddScoped<IDayJustificationRepository, DayJustificationRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IWorkItemDetailsRepository, WorkItemDetailsRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<ITaskTypeRepository, TaskTypeRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IWikiRepository, WikiRepository>();
        services.AddSingleton<IHtmlSanitizer, Services.HtmlSanitizerService>();
        services.AddScoped<ISprintRepository, SprintRepository>();
        services.AddScoped<IBacklogRepository, BacklogRepository>();
        services.AddScoped<IProductivityRepository, ProductivityRepository>();
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();
        services.AddScoped<IOrganizationWorkflowRepository, OrganizationWorkflowRepository>();
        services.AddScoped<IMyWorkRepository, MyWorkRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IExternalPortalRepository, ExternalPortalRepository>();
        services.AddScoped<ISlaRepository, SlaRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IPlatformNotificationPublisher>(sp =>
            (NotificationRepository)sp.GetRequiredService<INotificationRepository>());
        services.AddScoped<IGlobalSearchRepository, GlobalSearchRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IRefreshTokenService, Services.RefreshTokenService>();
        services.AddScoped<Services.PortalEmailSender>();
        services.AddScoped<IPortalEmailSender>(sp => sp.GetRequiredService<Services.PortalEmailSender>());
        services.AddScoped<IApplicationEmailSender>(sp => sp.GetRequiredService<Services.PortalEmailSender>());
        services.AddHostedService<Services.NotificationDeliveryWorker>();
        services.AddHostedService<Services.NotificationReminderWorker>();

        // ─── Lado de leitura (projeções) ─────────────────────────────────────
        services.AddScoped<ICompanyQueries, Queries.CompanyQueries>();
        services.AddScoped<IBoardMetricsQueries, Queries.BoardMetricsQueries>();

        // ─── Serviços de apoio ───────────────────────────────────────────────
        services.AddScoped<IUserDirectory, Identity.UserDirectory>();
        services.AddScoped<IProjectAccessService, Identity.ProjectAccessService>();
        services.AddScoped<IPermissionService, Identity.PermissionService>();
        services.AddScoped<IBoardAccessService, Identity.BoardAccessService>();
        services.AddScoped<IWorkItemAccessService, Identity.WorkItemAccessService>();
        services.AddScoped<IAutomationExecutor, Services.AutomationExecutor>();
        services.AddScoped<IInstallationSetupService, Services.InstallationSetupService>();

        return services;
    }
}
