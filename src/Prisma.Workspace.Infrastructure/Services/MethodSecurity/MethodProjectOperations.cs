using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodGit.Repositories;
using Prisma.Workspace.Application.Features.MethodSecurity;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;

namespace Prisma.Workspace.Infrastructure.Services.MethodSecurity;

public sealed class MethodProjectOperations(AppDbContext db, IMethodAccessContext access, IOrganizationContext organization,
    IPermissionService permissions, IMethodGitStore store, IMethodProjectProvisioner provisioner) : IMethodProjectOperations
{
    private static readonly PlatformPermission[] Capabilities = [PlatformPermission.View, PlatformPermission.Create,
        PlatformPermission.Edit, PlatformPermission.ChangeStatus, PlatformPermission.Comment, PlatformPermission.ManagePermissions];

    public async Task<MethodIdentityDto> IdentityAsync(Guid? projectId, CancellationToken cancellationToken = default)
    {
        var actor = Actor();
        var id = projectId ?? actor.ProjectId ?? throw new MethodOperationException(400, "project_required", "Escolha o projeto para consultar a identidade.");
        var project = await ProjectAsync(id, cancellationToken);
        var settings = await store.GetProjectAsync(id, cancellationToken);
        if (actor.AuthenticationKind == MethodAuthenticationKind.MethodToken && settings?.Enabled != true)
            throw new MethodOperationException(403, "method_disabled", "A configuração do método está indisponível. Peça a um administrador para verificá-la. Pare e avise.");
        var capabilities = new List<string>();
        foreach (var permission in Capabilities)
            if (await permissions.HasAsync(actor.ActorId, permission, PermissionScope.Project, id, cancellationToken))
                capabilities.Add(permission.ToString());
        return new MethodIdentityDto(JsonNamingPolicy.CamelCase.ConvertName(actor.AuthenticationKind.ToString()),
            actor.Role.ToCode(), actor.PrincipalId, actor.ActorId, actor.DisplayName, project.OrganizationId,
            project.Id, project.Name, settings?.BoardId ?? Guid.Empty, settings?.BoardName ?? string.Empty,
            actor.TokenId, actor.ExpiresAtUtc, capabilities);
    }

    public async Task<MethodProjectSnapshot?> SettingsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await ProjectAsync(projectId, cancellationToken);
        var settings = await store.GetProjectAsync(projectId, cancellationToken);
        return settings is null ? null : await WithIssuesAsync(settings, cancellationToken);
    }

    public async Task<MethodProjectSnapshot> ProvisionAsync(Guid projectId, ProvisionMethodProjectRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await RequireManageUiAsync(projectId, cancellationToken);
        return await WithIssuesAsync(await provisioner.ProvisionAsync(projectId, request, actor.ActorId, cancellationToken), cancellationToken);
    }

    public async Task<MethodProjectSnapshot> UpdateAsync(Guid projectId, UpdateMethodProjectRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await RequireManageUiAsync(projectId, cancellationToken);
        return await WithIssuesAsync(await provisioner.UpdateSettingsAsync(projectId, request, actor.ActorId, cancellationToken), cancellationToken);
    }

    public async Task<MethodRulesDto> RulesAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var identity = await IdentityAsync(projectId, cancellationToken);
        var project = await store.GetProjectAsync(projectId, cancellationToken)
            ?? throw new MethodOperationException(409, "method_not_provisioned", "Configure o método explicitamente neste projeto antes de usar o MCP.");
        if (!project.Enabled && Actor().AuthenticationKind == MethodAuthenticationKind.MethodToken)
            throw new MethodOperationException(403, "method_disabled", "O método está pausado ou possui configuração inválida. Pare e avise.");
        var transitions = new Dictionary<string, IReadOnlyList<string>>();
        var canMove = identity.Capabilities.Contains(nameof(PlatformPermission.ChangeStatus));
        foreach (var from in MethodStatusCodes.All)
        {
            var targets = new List<string>();
            if (canMove && Actor().IsInteractiveHuman)
                targets.AddRange(MethodStatusCodes.All.Where(to => to != from));
            else if (canMove && Actor().Role == MethodRole.Human)
            {
                targets.AddRange(MethodStatusCodes.All.Where(to => to != from && to != MethodStatusCodes.InDevelopment));
                if (from == MethodStatusCodes.InReview) targets.Add(MethodStatusCodes.InDevelopment);
            }
            else if (canMove && Actor().Role == MethodRole.Agent)
            {
                if (from != MethodStatusCodes.Completed && from != MethodStatusCodes.Blocked) targets.Add(MethodStatusCodes.Blocked);
                if (from == MethodStatusCodes.InDevelopment && project.AutonomyLevel == 0) targets.Add(MethodStatusCodes.InReview);
                if (from == MethodStatusCodes.Blocked) targets.Add(MethodStatusCodes.InDevelopment);
            }
            transitions[from] = targets;
        }
        return new(identity, project, transitions,
        [
            "Use branch task/T-XXXX-slug. O título do PR pode ser livre; vínculo explícito e branch têm prioridade sobre [T-XXXX].",
            "Nunca escreva status: no frontmatter das tasks. O status da HU continua em hu.md.",
            "Nenhum token do método concede início ou liberação humana de retomada; essas ações exigem a pessoa autenticada na UI.",
            "Retomada pelo agente exige bloqueio próprio de execução autorizada e liberação para o bloqueio e ciclo atuais.",
            "Se o MCP não responde, ou devolve 401/403, pare e avise. Não use senha, JWT humano ou modo local como alternativa.",
            "Transições são revalidadas por task e por evidência. PR aberto e merge não substituem autorização humana."
        ]);
    }

    private MethodActorIdentity Actor() => access.Current
        ?? throw new MethodOperationException(401, "method_identity_required", "Entre novamente para estabelecer uma identidade autenticada.");

    private async Task<MethodProjectSnapshot> WithIssuesAsync(MethodProjectSnapshot project, CancellationToken ct)
    {
        if (!Actor().IsInteractiveHuman || !await permissions.HasAsync(Actor().ActorId, PlatformPermission.ManagePermissions,
            PermissionScope.Project, project.ProjectId, ct)) return project;
        var values = await db.WorkItemCustomFieldValues.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.FieldDefinitionId == project.MethodIdFieldId && x.Value != null && x.Value != ""
                && x.WorkItem.Board.ProjectId == project.ProjectId && x.WorkItem.Board.OrganizationId == project.OrganizationId)
            .Select(x => new { x.WorkItemId, x.Value }).ToListAsync(ct);
        var issues = new List<MethodTaskIdentityIssueDto>();
        foreach (var group in values.GroupBy(x => x.Value!, StringComparer.OrdinalIgnoreCase))
        {
            var valid = group.Key is { Length: >= 6 and <= 32 } && group.Key.StartsWith("T-", StringComparison.Ordinal)
                && group.Key[2..].All(char.IsAsciiDigit);
            if (group.Count() == 1 && valid) continue;
            issues.Add(new(group.Key.Length <= 32 ? group.Key : group.Key[..32], group.Count() > 1 ? "duplicate_method_id" : "invalid_method_id",
                group.Count() > 1 ? "ID duplicado no projeto, incluindo cards arquivados e de outros quadros. Resolva a duplicidade; nenhuma task foi escolhida ou renumerada."
                    : "ID inválido. Use T- seguido de pelo menos quatro dígitos, com até 32 caracteres. Nenhuma autorização foi criada.",
                group.Select(x => x.WorkItemId).Distinct().ToArray()));
        }
        return project with { IdentityIssues = issues };
    }

    private async Task<Project> ProjectAsync(Guid projectId, CancellationToken ct)
    {
        var actor = Actor();
        if (actor.OrganizationId != organization.OrganizationId || organization.OrganizationId is null
            || actor.AuthenticationKind is MethodAuthenticationKind.MethodToken or MethodAuthenticationKind.IntegrationWorker
                && actor.ProjectId != projectId)
            throw new MethodOperationException(403, "method_project_scope", "Projeto fora do escopo autenticado.");
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(x => x.Id == projectId
            && x.OrganizationId == organization.OrganizationId && !x.IsArchived, ct)
            ?? throw new MethodOperationException(404, "project_not_found", "Projeto não encontrado na organização ativa.");
        await permissions.EnsureAsync(actor.ActorId, PlatformPermission.View, PermissionScope.Project, projectId, ct);
        return project;
    }

    private async Task<MethodActorIdentity> RequireManageUiAsync(Guid projectId, CancellationToken ct)
    {
        var actor = Actor();
        if (!actor.IsInteractiveHuman)
            throw new MethodOperationException(403, "interactive_session_required", "Configurar o método exige sessão humana da UI.");
        await ProjectAsync(projectId, ct);
        await permissions.EnsureAsync(actor.ActorId, PlatformPermission.ManagePermissions, PermissionScope.Project, projectId, ct);
        return actor;
    }
}
