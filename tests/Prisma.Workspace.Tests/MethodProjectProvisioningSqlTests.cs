using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Services.MethodGit;
using Xunit;

namespace Prisma.Workspace.Tests;

[Collection("MethodGit SQL")]
public sealed class MethodProjectProvisioningSqlTests(GitDeliverySqlFixture fixture) : IClassFixture<GitDeliverySqlFixture>
{
    private static readonly string[] Names = ["ID do metodo", "HU", "Arquivo", "Cobre", "Risco", "PR"];

    [MethodGitSqlFact]
    public async Task Explicit_provision_is_idempotent_and_preserves_default_board_cards_and_history()
    {
        await using var c = await fixture.CaseAsync(); var target = await TargetAsync(c);
        var service = Service(c, target.Owner);
        var first = await service.ProvisionAsync(target.ProjectId, new(), target.Owner);
        Assert.False(first.Enabled); Assert.Equal(MethodGitPhase.Webhooks, first.GitPhase);
        Assert.NotEqual(target.BoardId, first.BoardId); Assert.Equal("Método", first.BoardName);
        Assert.Equal(7, first.StageMappings.Count); Assert.Equal(7, first.StageMappings.Values.Distinct().Count());
        Assert.All(Names, name => Assert.True(first.Fields!.ContainsKey(name)));
        var stages = await c.Context.Stages.Where(x => x.BoardId == first.BoardId).ToListAsync();
        Assert.Single(stages, x => x.Category == StageCategory.Done);
        Assert.Equal(first.StageMappings[MethodStatusCodes.Completed], stages.Single(x => x.Category == StageCategory.Done).Id);
        var second = await service.ProvisionAsync(target.ProjectId, new(), target.Owner);
        Assert.Equal(first.BoardId, second.BoardId); Assert.Equal(first.PolicyVersion, second.PolicyVersion);
        Assert.Equal(first.StageMappings.OrderBy(x => x.Key), second.StageMappings.OrderBy(x => x.Key));
        Assert.Equal(2, await c.Context.Boards.CountAsync(x => x.ProjectId == target.ProjectId));
        Assert.Equal(6, await c.Context.Set<ProjectCustomFieldDefinition>().CountAsync(x => x.ProjectId == target.ProjectId && Names.Contains(x.Name)));
        Assert.Equal(target.BoardId, (await c.Context.Projects.AsNoTracking().SingleAsync(x => x.Id == target.ProjectId)).DefaultBoardId);
        await AssertLegacyUnchanged(c, target);
    }

    [MethodGitSqlFact]
    public async Task Failure_after_SQL_save_rolls_back_structure_and_retry_is_safe()
    {
        await using var c = await fixture.CaseAsync(); var target = await TargetAsync(c); var service = Service(c, target.Owner);
        c.Failure.Enabled = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProvisionAsync(target.ProjectId, new(), target.Owner));
        Assert.False(await c.Context.Set<MethodProjectSettings>().AnyAsync(x => x.ProjectId == target.ProjectId));
        Assert.Single(await c.Context.Boards.Where(x => x.ProjectId == target.ProjectId).ToListAsync());
        Assert.Single(await c.Context.Stages.Where(x => x.ProjectId == target.ProjectId).ToListAsync());
        Assert.Single(await c.Context.Set<ProjectCustomFieldDefinition>().Where(x => x.ProjectId == target.ProjectId).ToListAsync());
        await AssertLegacyUnchanged(c, target);
        var retry = await service.ProvisionAsync(target.ProjectId, new(), target.Owner);
        Assert.Equal(7, retry.StageMappings.Count);
        Assert.Equal(2, await c.Context.Boards.CountAsync(x => x.ProjectId == target.ProjectId));
    }

    [MethodGitSqlFact]
    public async Task External_transaction_owns_the_commit_and_can_roll_back_the_complete_configuration()
    {
        await using var c = await fixture.CaseAsync(); var target = await TargetAsync(c); var service = Service(c, target.Owner);
        await using (var transaction = await c.Context.Database.BeginTransactionAsync())
        {
            await service.ProvisionAsync(target.ProjectId, new(), target.Owner);
            Assert.Same(transaction, c.Context.Database.CurrentTransaction);
            await transaction.RollbackAsync();
        }
        c.Context.ChangeTracker.Clear();
        Assert.False(await c.Context.Set<MethodProjectSettings>().AnyAsync(x => x.ProjectId == target.ProjectId));
        Assert.Single(await c.Context.Boards.Where(x => x.ProjectId == target.ProjectId).ToListAsync());
        Assert.Single(await c.Context.Stages.Where(x => x.ProjectId == target.ProjectId).ToListAsync());
        await AssertLegacyUnchanged(c, target);
    }

    [MethodGitSqlFact]
    public async Task Requested_board_reuses_open_columns_without_reclassifying_or_moving_human_cards()
    {
        await using var c = await fixture.CaseAsync(); var target = await TargetAsync(c);
        var existing = new Stage { Id = Guid.NewGuid(), ProjectId = target.ProjectId, BoardId = target.BoardId,
            Name = "Backlog", Category = StageCategory.Review, CreatedAt = c.Now, Position = 50 };
        c.Context.Add(existing); await c.Context.SaveChangesAsync();
        var service = Service(c, target.Owner);
        var result = await service.ProvisionAsync(target.ProjectId, new(target.BoardId), target.Owner);
        Assert.Equal(target.BoardId, result.BoardId); Assert.Equal(existing.Id, result.StageMappings[MethodStatusCodes.Backlog]);
        Assert.Equal(StageCategory.Review, (await c.Context.Stages.AsNoTracking().SingleAsync(x => x.Id == existing.Id)).Category);
        Assert.Single(await c.Context.Boards.Where(x => x.ProjectId == target.ProjectId).ToListAsync());
        Assert.Equal(8, await c.Context.Stages.CountAsync(x => x.BoardId == target.BoardId)); // Triagem humana mais as sete do método.
        await AssertLegacyUnchanged(c, target);
    }

    [MethodGitSqlFact]
    public async Task Conflicting_field_types_or_names_roll_back_without_orphan_structure()
    {
        await using var c = await fixture.CaseAsync();
        foreach (var duplicate in new[] { false, true })
        {
            var target = await TargetAsync(c);
            c.Context.Add(ProjectCustomFieldDefinition.Create(target.ProjectId, "HU", duplicate ? CustomFieldType.Text : CustomFieldType.Number, false, null, 200));
            if (duplicate) c.Context.Add(ProjectCustomFieldDefinition.Create(target.ProjectId, "HU", CustomFieldType.Text, false, null, 300));
            await c.Context.SaveChangesAsync();
            var count = await c.Context.Set<ProjectCustomFieldDefinition>().CountAsync(x => x.ProjectId == target.ProjectId);
            var error = await Assert.ThrowsAsync<MethodOperationException>(() => Service(c, target.Owner).ProvisionAsync(target.ProjectId, new(), target.Owner));
            Assert.Equal(409, error.StatusCode);
            Assert.Single(await c.Context.Boards.Where(x => x.ProjectId == target.ProjectId).ToListAsync());
            Assert.Single(await c.Context.Stages.Where(x => x.ProjectId == target.ProjectId).ToListAsync());
            Assert.Equal(count, await c.Context.Set<ProjectCustomFieldDefinition>().CountAsync(x => x.ProjectId == target.ProjectId));
            Assert.False(await c.Context.Set<MethodProjectSettings>().AnyAsync(x => x.ProjectId == target.ProjectId));
            await AssertLegacyUnchanged(c, target);
        }
    }

    [MethodGitSqlFact]
    public async Task Enabling_validates_structure_but_disabling_can_pause_an_invalid_configuration()
    {
        await using var c = await fixture.CaseAsync(); var target = await TargetAsync(c);
        var service = Service(c, target.Owner); var configured = await service.ProvisionAsync(target.ProjectId, new(), target.Owner);
        var enabled = await service.UpdateSettingsAsync(target.ProjectId, new(true, 2), target.Owner);
        Assert.True(enabled.Enabled); Assert.Equal(2, enabled.AutonomyLevel); Assert.Equal(MethodGitPhase.Webhooks, enabled.GitPhase);
        var stage = await c.Context.Stages.SingleAsync(x => x.Id == configured.StageMappings[MethodStatusCodes.InDevelopment]);
        stage.Category = StageCategory.Done; await c.Context.SaveChangesAsync();
        Assert.Equal(409, (await Assert.ThrowsAsync<MethodOperationException>(() => service.UpdateSettingsAsync(target.ProjectId, new(true, 0), target.Owner))).StatusCode);
        var disabled = await service.UpdateSettingsAsync(target.ProjectId, new(false, 0), target.Owner);
        Assert.False(disabled.Enabled);
        Assert.Equal(StageCategory.Done, (await c.Context.Stages.AsNoTracking().SingleAsync(x => x.Id == stage.Id)).Category);
        var settings = await c.Context.Set<MethodProjectSettings>().SingleAsync(x => x.ProjectId == target.ProjectId);
        settings.BoardId = null; await c.Context.SaveChangesAsync();
        Assert.Equal(Guid.Empty, (await service.UpdateSettingsAsync(target.ProjectId, new(false, 0), target.Owner)).BoardId);
        Assert.Equal(409, (await Assert.ThrowsAsync<MethodOperationException>(() => service.UpdateSettingsAsync(target.ProjectId, new(true, 0), target.Owner))).StatusCode);
        await AssertLegacyUnchanged(c, target);
    }

    [MethodGitSqlFact]
    public async Task Only_the_authenticated_UI_admin_in_the_current_tenant_can_configure()
    {
        await using var c = await fixture.CaseAsync(); var target = await TargetAsync(c);
        foreach (var (kind, role) in new[] { (MethodAuthenticationKind.MethodToken, MethodRole.Human), (MethodAuthenticationKind.MethodToken, MethodRole.Agent),
                     (MethodAuthenticationKind.IntegrationWorker, MethodRole.System), (MethodAuthenticationKind.Unknown, MethodRole.Human),
                     (MethodAuthenticationKind.UiSession, MethodRole.System) })
        {
            var actor = Identity(c, target.Owner) with { AuthenticationKind = kind, Role = role, PrincipalId = Guid.NewGuid(), ProjectId = target.ProjectId };
            var service = Service(c, target.Owner, actor: actor);
            Assert.Equal(403, (await Assert.ThrowsAsync<MethodOperationException>(() => service.ProvisionAsync(target.ProjectId, new(), target.Owner))).StatusCode);
            Assert.Equal(403, (await Assert.ThrowsAsync<MethodOperationException>(() => service.UpdateSettingsAsync(target.ProjectId, new(true, 0), target.Owner))).StatusCode);
        }
        Assert.Equal(403, (await Assert.ThrowsAsync<MethodOperationException>(() => Service(c, target.Owner).ProvisionAsync(target.ProjectId, new(), "outro-ator"))).StatusCode);
        Assert.Equal(403, (await Assert.ThrowsAsync<MethodOperationException>(() => Service(c, target.Owner, allowed: false).ProvisionAsync(target.ProjectId, new(), target.Owner))).StatusCode);
        Assert.Equal(403, (await Assert.ThrowsAsync<MethodOperationException>(() => Service(c, target.Owner,
            actor: Identity(c, target.Owner) with { OrganizationId = Guid.NewGuid() }).ProvisionAsync(target.ProjectId, new(), target.Owner))).StatusCode);
        var alphaBoard = (await c.Store(c.Context).GetProjectAsync(c.ProjectId))!.BoardId;
        Assert.Equal(404, (await Assert.ThrowsAsync<MethodOperationException>(() => Service(c, target.Owner).ProvisionAsync(target.ProjectId, new(alphaBoard), target.Owner))).StatusCode);
        Assert.Equal(400, (await Assert.ThrowsAsync<MethodOperationException>(() => Service(c, target.Owner).ProvisionAsync(target.ProjectId, new(AutonomyLevel: 3), target.Owner))).StatusCode);
        Assert.False(await c.Context.Set<MethodProjectSettings>().AnyAsync(x => x.ProjectId == target.ProjectId));
        Assert.Single(await c.Context.Boards.Where(x => x.ProjectId == target.ProjectId).ToListAsync());
    }

    [MethodGitSqlFact]
    public async Task Reprovision_keeps_explicit_stage_IDs_after_a_human_renames_a_column()
    {
        await using var c = await fixture.CaseAsync(); var target = await TargetAsync(c); var service = Service(c, target.Owner);
        var first = await service.ProvisionAsync(target.ProjectId, new(), target.Owner);
        var stage = await c.Context.Stages.SingleAsync(x => x.Id == first.StageMappings[MethodStatusCodes.InDevelopment]);
        stage.Name = "Execução da equipe"; await c.Context.SaveChangesAsync();
        var again = await service.ProvisionAsync(target.ProjectId, new(), target.Owner);
        Assert.Equal(stage.Id, again.StageMappings[MethodStatusCodes.InDevelopment]);
        Assert.Equal("Execução da equipe", (await c.Context.Stages.AsNoTracking().SingleAsync(x => x.Id == stage.Id)).Name);
        Assert.Equal(7, await c.Context.Stages.CountAsync(x => x.BoardId == first.BoardId));
        await AssertLegacyUnchanged(c, target);
    }

    private sealed record Target(Guid ProjectId, Guid BoardId, Guid StageId, Guid ParentId, Guid ChildId, Guid FieldId, string Owner);
    private static async Task<Target> TargetAsync(GitDeliverySqlFixture.SqlCase c)
    {
        var owner = (await c.Context.Boards.SingleAsync(x => x.ProjectId == c.ProjectId)).OwnerId!;
        var project = Project.Criar("P" + Guid.NewGuid().ToString("N")[..8], "Provisionamento SQL próprio", owner, WorkNature.Project, WorkType.Development);
        project.OrganizationId = c.Repository.OrganizationId;
        var board = new Board { Id = Guid.NewGuid(), OrganizationId = project.OrganizationId, ProjectId = project.Id, OwnerId = owner, Name = "Quadro humano existente", CreatedAt = c.Now };
        var stage = new Stage { Id = Guid.NewGuid(), ProjectId = project.Id, BoardId = board.Id, Name = "Triagem humana", Category = StageCategory.Ready, CreatedAt = c.Now };
        var parent = new WorkItem { Id = Guid.NewGuid(), BoardId = board.Id, StageId = stage.Id, Title = "Card humano preservado", CreatedAt = c.Now, UpdatedAt = c.Now };
        var child = new WorkItem { Id = Guid.NewGuid(), BoardId = board.Id, StageId = stage.Id, ParentId = parent.Id, Title = "Subitem humano preservado", CreatedAt = c.Now, UpdatedAt = c.Now };
        var field = ProjectCustomFieldDefinition.Create(project.Id, "Campo legado", CustomFieldType.Text, false, null, 100);
        c.Context.AddRange(project, board, stage, parent, child, field,
            new WorkItemCustomFieldValue { WorkItemId = parent.Id, FieldDefinitionId = field.Id, Value = "Original preservado", UpdatedBy = owner, UpdatedAt = c.Now });
        await c.Context.SaveChangesAsync(); project.DefaultBoardId = board.Id; await c.Context.SaveChangesAsync();
        return new(project.Id, board.Id, stage.Id, parent.Id, child.Id, field.Id, owner);
    }
    private static async Task AssertLegacyUnchanged(GitDeliverySqlFixture.SqlCase c, Target target)
    {
        var parent = await c.Context.WorkItems.AsNoTracking().SingleAsync(x => x.Id == target.ParentId);
        var child = await c.Context.WorkItems.AsNoTracking().SingleAsync(x => x.Id == target.ChildId);
        Assert.Equal(target.BoardId, parent.BoardId); Assert.Equal(target.StageId, parent.StageId);
        Assert.Equal(target.ParentId, child.ParentId); Assert.Equal(target.StageId, child.StageId);
        Assert.Equal("Original preservado", (await c.Context.Set<WorkItemCustomFieldValue>().SingleAsync(x => x.WorkItemId == parent.Id && x.FieldDefinitionId == target.FieldId)).Value);
        Assert.False(await c.Context.Set<MethodTaskState>().AnyAsync(x => x.ProjectId == target.ProjectId));
        Assert.False(await c.Context.Set<MethodAuthorizationEvent>().AnyAsync(x => x.ProjectId == target.ProjectId));
        Assert.False(await c.Context.TaskEvents.AnyAsync(x => x.WorkItemId == parent.Id || x.WorkItemId == child.Id));
    }
    private static MethodActorIdentity Identity(GitDeliverySqlFixture.SqlCase c, string owner) => new(MethodAuthenticationKind.UiSession, MethodRole.Human,
        null, owner, "Admin SQL próprio", c.Repository.OrganizationId, null, null, null, Enum.GetValues<PlatformPermission>().ToHashSet(), "ui", "provision-sql");
    private static MethodProjectProvisioner Service(GitDeliverySqlFixture.SqlCase c, string owner, bool allowed = true, MethodActorIdentity? actor = null)
        => new(c.Context, new Tenant(c.Repository.OrganizationId), new Actor(actor ?? Identity(c, owner)), new Permissions(allowed), c.Clock);
    private sealed class Tenant(Guid id) : IOrganizationContext { public Guid? OrganizationId => id; }
    private sealed class Actor(MethodActorIdentity actor) : IMethodAccessContext { public MethodActorIdentity? Current => actor; }
    private sealed class Permissions(bool allowed) : IPermissionService
    {
        public Task<bool> HasAsync(string actor, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization, Guid? id = null, CancellationToken ct = default) => Task.FromResult(allowed);
        public Task EnsureAsync(string actor, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization, Guid? id = null, CancellationToken ct = default)
        {
            Assert.Equal(PlatformPermission.ManagePermissions, permission); Assert.Equal(PermissionScope.Project, scope);
            if (!allowed) throw new MethodOperationException(403, "permission_denied", "Sem permissão administrativa no projeto.");
            return Task.CompletedTask;
        }
    }
}
