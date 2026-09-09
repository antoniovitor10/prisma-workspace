using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Domain.Interfaces;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace Prisma.Workspace.Infrastructure.Persistence;

/// <summary>
/// Contexto principal do banco de dados.
/// Herda de IdentityDbContext para incluir as tabelas do ASP.NET Core Identity.
/// </summary>
public class AppDbContext : IdentityDbContext
{
    private readonly IOrganizationContext? _organizationContext;
    private readonly IAuditContext? _auditContext;
    private Guid? CurrentOrganizationId => _organizationContext?.OrganizationId;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        IOrganizationContext? organizationContext = null,
        IAuditContext? auditContext = null)
        : base(options)
    {
        _organizationContext = organizationContext;
        _auditContext = auditContext;
    }

    // ─── DbSets de negócio ───────────────────────────────────────────────
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<Stage> Stages => Set<Stage>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<WorkItemAssignee> WorkItemAssignees => Set<WorkItemAssignee>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<StageHistory> StageHistories => Set<StageHistory>();
    public DbSet<SavedFilter> SavedFilters => Set<SavedFilter>();
    public DbSet<AutomationRule> AutomationRules => Set<AutomationRule>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<TaskEvent> TaskEvents => Set<TaskEvent>();
    public DbSet<TaskType> TaskTypes => Set<TaskType>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<WorkItemTag> WorkItemTags => Set<WorkItemTag>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<DayJustification> DayJustifications => Set<DayJustification>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<ProjectTeam> ProjectTeams => Set<ProjectTeam>();
    public DbSet<ProjectTag> ProjectTags => Set<ProjectTag>();
    public DbSet<ProjectCustomFieldDefinition> ProjectCustomFields => Set<ProjectCustomFieldDefinition>();
    public DbSet<ProjectEvent> ProjectEvents => Set<ProjectEvent>();
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<SprintCapacity> SprintCapacities => Set<SprintCapacity>();
    public DbSet<SprintItemSnapshot> SprintItemSnapshots => Set<SprintItemSnapshot>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationWorkflowTemplate> OrganizationWorkflowTemplates => Set<OrganizationWorkflowTemplate>();
    public DbSet<OrganizationWorkflowStatus> OrganizationWorkflowStatuses => Set<OrganizationWorkflowStatus>();
    public DbSet<OrganizationWorkflowTransition> OrganizationWorkflowTransitions => Set<OrganizationWorkflowTransition>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<OrganizationInvitation> OrganizationInvitations => Set<OrganizationInvitation>();
    public DbSet<PermissionGrant> PermissionGrants => Set<PermissionGrant>();
    public DbSet<WorkItemLink> WorkItemLinks => Set<WorkItemLink>();
    public DbSet<WorkItemFollower> WorkItemFollowers => Set<WorkItemFollower>();
    public DbSet<WorkItemCustomFieldValue> WorkItemCustomFieldValues => Set<WorkItemCustomFieldValue>();
    public DbSet<WikiPage> WikiPages => Set<WikiPage>();
    public DbSet<WikiPageRevision> WikiPageRevisions => Set<WikiPageRevision>();
    public DbSet<WikiAttachment> WikiAttachments => Set<WikiAttachment>();
    public DbSet<WikiPageWorkItemLink> WikiPageWorkItemLinks => Set<WikiPageWorkItemLink>();
    public DbSet<WorkflowStatus> WorkflowStatuses => Set<WorkflowStatus>();
    public DbSet<WorkflowTransition> WorkflowTransitions => Set<WorkflowTransition>();
    public DbSet<ExternalPortal> ExternalPortals => Set<ExternalPortal>();
    public DbSet<ExternalRequest> ExternalRequests => Set<ExternalRequest>();
    public DbSet<ExternalRequestMessage> ExternalRequestMessages => Set<ExternalRequestMessage>();
    public DbSet<ExternalPortalInvitation> ExternalPortalInvitations => Set<ExternalPortalInvitation>();
    public DbSet<ExternalPortalVerification> ExternalPortalVerifications => Set<ExternalPortalVerification>();
    public DbSet<ExternalForm> ExternalForms => Set<ExternalForm>();
    public DbSet<ExternalRequestTriageEvent> ExternalRequestTriageEvents => Set<ExternalRequestTriageEvent>();
    public DbSet<SavedReport> SavedReports => Set<SavedReport>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<InstallationState> InstallationStates => Set<InstallationState>();


    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configurações de mapeamento de entidades de negócio serão adicionadas na Fase 1.
        // Aplicar todas as configurações do assembly atual (IEntityTypeConfiguration<T>).
        builder.HasSequence<long>("WorkItemNumbers")
            .StartsAt(1000)
            .IncrementsBy(1);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        ApplyOrganizationFilters(builder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        CaptureAuditEntries();
        EnforceOrganizationOwnership();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        CaptureAuditEntries();
        EnforceOrganizationOwnership();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnforceOrganizationOwnership()
    {
        foreach (var entry in ChangeTracker.Entries<InstallationState>()
                     .Where(x => x.State == EntityState.Modified))
        {
            var wasInitialized = (bool)entry.OriginalValues[nameof(InstallationState.IsInitialized)]!;
            var isInitialized = (bool)entry.CurrentValues[nameof(InstallationState.IsInitialized)]!;
            DomainException.Garantir(!wasInitialized || isInitialized,
                "O estado de instalação não pode ser reaberto.");
        }

        var organizationId = CurrentOrganizationId;
        foreach (var entry in ChangeTracker.Entries()
                     .Where(x => x.Entity is IOrganizationOwned
                         && x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var owned = (IOrganizationOwned)entry.Entity;
            if (entry.State == EntityState.Added && owned.OrganizationId == Guid.Empty && organizationId is not null)
                owned.OrganizationId = organizationId.Value;

            DomainException.Garantir(owned.OrganizationId != Guid.Empty,
                "Todo dado precisa pertencer a uma organização.");

            if (organizationId is not null)
                DomainException.Garantir(owned.OrganizationId == organizationId,
                    "Não é permitido alterar dados de outra organização.");
        }
    }

    private void ApplyOrganizationFilters(ModelBuilder builder)
    {
        builder.Entity<Organization>().HasQueryFilter(x => x.Id == CurrentOrganizationId);
        builder.Entity<OrganizationWorkflowTemplate>().HasQueryFilter(x => x.OrganizationId == CurrentOrganizationId);
        builder.Entity<OrganizationWorkflowStatus>().HasQueryFilter(x => x.Template.OrganizationId == CurrentOrganizationId);
        builder.Entity<OrganizationWorkflowTransition>().HasQueryFilter(x => x.Template.OrganizationId == CurrentOrganizationId);
        builder.Entity<OrganizationMember>().HasQueryFilter(x => x.OrganizationId == CurrentOrganizationId);
        builder.Entity<OrganizationInvitation>().HasQueryFilter(x => x.OrganizationId == CurrentOrganizationId);
        builder.Entity<PermissionGrant>().HasQueryFilter(x => x.OrganizationId == CurrentOrganizationId);

        builder.Entity<Project>().HasQueryFilter(x => x.OrganizationId == CurrentOrganizationId);
        builder.Entity<ProjectMember>().HasQueryFilter(x => x.Project.OrganizationId == CurrentOrganizationId);
        builder.Entity<ProjectTeam>().HasQueryFilter(x => x.Project.OrganizationId == CurrentOrganizationId);
        builder.Entity<ProjectTag>().HasQueryFilter(x => x.Project.OrganizationId == CurrentOrganizationId);
        builder.Entity<ProjectCustomFieldDefinition>().HasQueryFilter(x => x.Project.OrganizationId == CurrentOrganizationId);
        builder.Entity<ProjectEvent>().HasQueryFilter(x => x.Project.OrganizationId == CurrentOrganizationId);
        builder.Entity<WorkflowStatus>().HasQueryFilter(x => x.Project.OrganizationId == CurrentOrganizationId);
        builder.Entity<WorkflowTransition>().HasQueryFilter(x => x.SourceStatus.Project.OrganizationId == CurrentOrganizationId);
        builder.Entity<Sprint>().HasQueryFilter(x => x.Project.OrganizationId == CurrentOrganizationId);
        builder.Entity<SprintCapacity>().HasQueryFilter(x => x.Sprint.Project.OrganizationId == CurrentOrganizationId);
        builder.Entity<SprintItemSnapshot>().HasQueryFilter(x => x.Sprint.Project.OrganizationId == CurrentOrganizationId);
        builder.Entity<ExternalPortal>().HasQueryFilter(x => x.OrganizationId == CurrentOrganizationId);
        builder.Entity<ExternalRequest>().HasQueryFilter(x => x.ExternalPortal.OrganizationId == CurrentOrganizationId);
        builder.Entity<ExternalRequestMessage>().HasQueryFilter(x => x.ExternalRequest.ExternalPortal.OrganizationId == CurrentOrganizationId);
        builder.Entity<ExternalPortalInvitation>().HasQueryFilter(x => x.ExternalPortal.OrganizationId == CurrentOrganizationId);
        builder.Entity<ExternalPortalVerification>().HasQueryFilter(x => x.ExternalPortal.OrganizationId == CurrentOrganizationId);
        builder.Entity<ExternalForm>().HasQueryFilter(x => x.ExternalPortal.OrganizationId == CurrentOrganizationId);
        builder.Entity<ExternalRequestTriageEvent>().HasQueryFilter(x => x.ExternalRequest.ExternalPortal.OrganizationId == CurrentOrganizationId);
        builder.Entity<SavedReport>().HasQueryFilter(x => x.OrganizationId == CurrentOrganizationId);
        builder.Entity<Notification>().HasQueryFilter(x => x.OrganizationId == CurrentOrganizationId);
        builder.Entity<NotificationPreference>().HasQueryFilter(x => x.OrganizationId == CurrentOrganizationId);
        builder.Entity<AuditLog>().HasQueryFilter(x => x.OrganizationId == CurrentOrganizationId);

        builder.Entity<Team>().HasQueryFilter(x => x.OrganizationId == CurrentOrganizationId);
        builder.Entity<TeamMember>().HasQueryFilter(x => x.Team.OrganizationId == CurrentOrganizationId);
        builder.Entity<Board>().HasQueryFilter(x => x.OrganizationId == CurrentOrganizationId);
        builder.Entity<Stage>().HasQueryFilter(x => x.Board.OrganizationId == CurrentOrganizationId);
        builder.Entity<WorkItem>().HasQueryFilter(x => x.Board.OrganizationId == CurrentOrganizationId);
        builder.Entity<WikiPage>().HasQueryFilter(x => x.Project.OrganizationId == CurrentOrganizationId);
        builder.Entity<WikiPageRevision>().HasQueryFilter(x => x.Page.Project.OrganizationId == CurrentOrganizationId);
        builder.Entity<WikiAttachment>().HasQueryFilter(x => x.Page.Project.OrganizationId == CurrentOrganizationId);
        builder.Entity<WikiPageWorkItemLink>().HasQueryFilter(x => x.Page.Project.OrganizationId == CurrentOrganizationId);
        builder.Entity<WorkItemAssignee>().HasQueryFilter(x => x.WorkItem.Board.OrganizationId == CurrentOrganizationId);
        builder.Entity<Attachment>().HasQueryFilter(x => x.WorkItem.Board.OrganizationId == CurrentOrganizationId);
        builder.Entity<TimeEntry>().HasQueryFilter(x => x.WorkItem.Board.OrganizationId == CurrentOrganizationId);
        builder.Entity<StageHistory>().HasQueryFilter(x => x.WorkItem.Board.OrganizationId == CurrentOrganizationId);
        builder.Entity<SavedFilter>().HasQueryFilter(x => x.Board.OrganizationId == CurrentOrganizationId);
        builder.Entity<AutomationRule>().HasQueryFilter(x => x.Board.OrganizationId == CurrentOrganizationId);
        builder.Entity<Comment>().HasQueryFilter(x => x.WorkItem.Board.OrganizationId == CurrentOrganizationId);
        builder.Entity<TaskEvent>().HasQueryFilter(x => x.WorkItem.Board.OrganizationId == CurrentOrganizationId);
        builder.Entity<ChecklistItem>().HasQueryFilter(x => x.WorkItem.Board.OrganizationId == CurrentOrganizationId);
        builder.Entity<Approval>().HasQueryFilter(x => x.WorkItem.Board.OrganizationId == CurrentOrganizationId);
        builder.Entity<WorkItemTag>().HasQueryFilter(x => x.WorkItem.Board.OrganizationId == CurrentOrganizationId);
        builder.Entity<WorkItemLink>().HasQueryFilter(x => x.SourceWorkItem.Board.OrganizationId == CurrentOrganizationId);
        builder.Entity<WorkItemFollower>().HasQueryFilter(x => x.WorkItem.Board.OrganizationId == CurrentOrganizationId);
        builder.Entity<WorkItemCustomFieldValue>().HasQueryFilter(x => x.WorkItem.Board.OrganizationId == CurrentOrganizationId);

        builder.Entity<Client>().HasQueryFilter(x => x.OrganizationId == CurrentOrganizationId);
        builder.Entity<Tag>().HasQueryFilter(x => x.OrganizationId == CurrentOrganizationId);
        builder.Entity<TaskType>().HasQueryFilter(x => x.OrganizationId == CurrentOrganizationId);
        builder.Entity<DayJustification>().HasQueryFilter(x => x.OrganizationId == CurrentOrganizationId);
    }

    private static readonly HashSet<string> AuditedEntityTypes = new(StringComparer.Ordinal)
    {
        nameof(WorkItem), nameof(ExternalRequest), nameof(Project), nameof(ProjectMember),
        nameof(ProjectTeam), nameof(ProjectCustomFieldDefinition),
        nameof(OrganizationMember), nameof(PermissionGrant), nameof(Sprint), nameof(SprintCapacity),
        nameof(WorkflowStatus), nameof(WorkflowTransition), nameof(Stage), nameof(TimeEntry),
        nameof(OrganizationWorkflowTemplate), nameof(OrganizationWorkflowStatus), nameof(OrganizationWorkflowTransition),
        nameof(ExternalForm), nameof(ExternalRequestTriageEvent), nameof(SavedReport),
        nameof(AutomationRule), nameof(WorkItemAssignee), nameof(WorkItemTag)
    };

    private static readonly string[] SensitivePropertyFragments =
    {
        "password", "token", "hash", "secret", "securitystamp", "concurrencystamp",
        "accesskey", "code", "binary", "contenttype", "requesteremail", "requesterphone"
    };

    private void CaptureAuditEntries()
    {
        var organizationId = CurrentOrganizationId ?? ResolveTrackedOrganizationId();
        if (organizationId is null || _auditContext?.CorrelationId is null
            || ChangeTracker.Entries<AuditLog>().Any()) return;

        ChangeTracker.DetectChanges();
        var entries = ChangeTracker.Entries()
            .Where(x => AuditedEntityTypes.Contains(x.Metadata.ClrType.Name)
                && x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        foreach (var entry in entries)
        {
            var previous = entry.State == EntityState.Added ? null : Values(entry, original: true);
            var current = entry.State == EntityState.Deleted ? null : Values(entry, original: false);
            if (entry.State == EntityState.Modified && (current is null || current.Count == 0)) continue;

            AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId.Value,
                UserId = _auditContext?.UserId,
                OccurredAt = DateTimeOffset.UtcNow,
                Action = entry.State switch
                {
                    EntityState.Added => "created",
                    EntityState.Deleted => "deleted",
                    _ => "updated"
                },
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = EntityIdentifier(entry),
                PreviousValuesJson = previous is null ? null : JsonSerializer.Serialize(previous),
                NewValuesJson = current is null ? null : JsonSerializer.Serialize(current),
                Origin = _auditContext?.Origin ?? "system",
                IpAddress = _auditContext?.IpAddress,
                CorrelationId = _auditContext?.CorrelationId
            });
        }
    }

    private Guid? ResolveTrackedOrganizationId()
    {
        var ids = ChangeTracker.Entries()
            .Select(x => x.Entity)
            .OfType<IOrganizationOwned>()
            .Select(x => x.OrganizationId)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .Take(2)
            .ToList();
        return ids.Count == 1 ? ids[0] : null;
    }

    private static Dictionary<string, object?> Values(EntityEntry entry, bool original)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in entry.Properties)
        {
            if (property.Metadata.IsShadowProperty()) continue;
            if (entry.State == EntityState.Modified && !property.IsModified) continue;
            if (property.Metadata.IsConcurrencyToken) continue;
            var name = property.Metadata.Name;
            if (SensitivePropertyFragments.Any(fragment =>
                    name.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            {
                values[name] = "[REDACTED]";
                continue;
            }
            var value = original ? property.OriginalValue : property.CurrentValue;
            values[name] = value switch
            {
                byte[] => "[BINARY]",
                string text when text.Length > 4000 => text[..4000] + "…",
                _ => value
            };
        }
        return values;
    }

    private static string EntityIdentifier(EntityEntry entry)
    {
        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey is null) return "unknown";
        if (primaryKey.Properties.Count == 1)
        {
            var property = primaryKey.Properties[0];
            var value = entry.Property(property.Name).CurrentValue
                ?? entry.Property(property.Name).OriginalValue;
            return value?.ToString() ?? "unknown";
        }

        return string.Join("|", primaryKey.Properties.Select(property =>
        {
            var value = entry.Property(property.Name).CurrentValue
                ?? entry.Property(property.Name).OriginalValue;
            return $"{property.Name}={value}";
        }));
    }
}
