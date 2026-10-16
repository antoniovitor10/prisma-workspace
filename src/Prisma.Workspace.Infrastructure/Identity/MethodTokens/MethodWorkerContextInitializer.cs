using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodSecurity;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Tenancy;

namespace Prisma.Workspace.Infrastructure.Identity.MethodTokens;

public sealed class MethodWorkerContextInitializer(AppDbContext db, CurrentOrganizationContext organization,
    CurrentAuditContext audit, IMethodContextInitializer initializer, IMethodAccessContext context,
    IMethodPermissionEvaluator permissions) : IMethodWorkerContextInitializer
{
    public async Task InitializeAsync(Guid integrationId, Guid? deliveryId, string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (context.Current is not null)
            throw new InvalidOperationException("O worker precisa de um escopo novo de autenticação.");
        // Descoberta mínima; não lê task/evento/payload antes de estabelecer o tenant.
        var repository = await db.Set<GitRepositoryIntegration>().IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == integrationId)
            .Select(x => new { x.OrganizationId, x.ProjectId, x.SystemPrincipalId })
            .SingleOrDefaultAsync(cancellationToken);
        if (repository is null) throw Denied();
        if (deliveryId is { } id && !await db.Set<GitWebhookDelivery>().IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(x => x.Id == id && x.IntegrationId == integrationId && x.ProjectId == repository.ProjectId, cancellationToken))
            throw Denied();
        var principal = await db.Set<MethodPrincipal>().IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == repository.SystemPrincipalId && x.ProjectId == repository.ProjectId
                && x.OrganizationId == repository.OrganizationId && x.IsActive && x.Role == MethodRole.System, cancellationToken);
        if (principal is null) throw Denied();
        organization.Set(repository.OrganizationId);
        correlationId = string.IsNullOrWhiteSpace(correlationId) ? integrationId.ToString("D") : correlationId;
        if (correlationId.Length > 100) correlationId = correlationId[..100];
        var actor = new MethodActorIdentity(MethodAuthenticationKind.IntegrationWorker, MethodRole.System,
            principal.Id, $"method:{principal.Id:D}", principal.DisplayName, repository.OrganizationId,
            repository.ProjectId, null, null, MethodPermissionEvaluator.Ceiling(principal), MethodChannels.Webhook, correlationId);
        if (!await permissions.HasAsync(actor, PlatformPermission.View, PermissionScope.Project, repository.ProjectId, cancellationToken))
            throw Denied();
        // Configuração de colunas/Enabled é validada pelo processador: o ingresso assinado continua durável.
        initializer.Initialize(actor);
        audit.Set(actor.ActorId, MethodChannels.Webhook, null, correlationId);
    }

    private static MethodOperationException Denied() => new(403, "integration_authority_unavailable",
        "A integração não possui principal e delegação válidos neste projeto.");
}
