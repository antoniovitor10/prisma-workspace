using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodGit.Repositories;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;

namespace Prisma.Workspace.Infrastructure.Services.MethodGit;

/// <summary>Somente estrutura/configuração: adoção de tasks e provas pertence ao coordenador F1.</summary>
public sealed class MethodProjectProvisioner(AppDbContext db, IOrganizationContext organization,
    IMethodAccessContext access, IPermissionService permissions, TimeProvider? clock = null) : IMethodProjectProvisioner
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private static readonly string[] FieldNames = ["ID do metodo", "HU", "Arquivo", "Cobre", "Risco", "PR"];
    private static readonly (string Code, string Name, StageCategory Category)[] Columns =
    [
        (MethodStatusCodes.Backlog, "Backlog", StageCategory.Backlog),
        (MethodStatusCodes.Ready, "Pronta", StageCategory.Ready),
        (MethodStatusCodes.InDevelopment, "Em desenvolvimento", StageCategory.InProgress),
        (MethodStatusCodes.InReview, "Em revisão", StageCategory.Review),
        (MethodStatusCodes.InTest, "Em teste", StageCategory.Review),
        (MethodStatusCodes.Completed, "Concluída", StageCategory.Done),
        (MethodStatusCodes.Blocked, "Bloqueada", StageCategory.InProgress)
    ];

    public async Task<MethodProjectSnapshot> ProvisionAsync(Guid projectId, ProvisionMethodProjectRequest request,
        string actorId, CancellationToken cancellationToken = default)
    {
        var tenant = await RequireAdminAsync(projectId, actorId, cancellationToken);
        ValidateAutonomy(request.AutonomyLevel);
        if (request.BoardId == Guid.Empty) throw Invalid("Selecione um quadro válido ou solicite a criação do quadro Método.");
        return await InTransactionAsync(async () =>
        {
            var project = await ProjectAsync(projectId, tenant, cancellationToken);
            var settings = await SettingsAsync(projectId, tenant, cancellationToken);
            var existing = settings is not null;
            var chosenId = request.BoardId ?? settings?.BoardId;
            Board board;
            if (chosenId is { } id)
                board = await db.Boards.SingleOrDefaultAsync(x => x.Id == id && x.ProjectId == projectId && x.OrganizationId == tenant, cancellationToken)
                    ?? throw new MethodOperationException(404, "method_board_not_found", "O quadro não está disponível neste projeto. Selecione explicitamente outro quadro.");
            else
            {
                // BoardId omitido no POST explícito corresponde à opção "Criar quadro Método" da UI.
                board = new Board { Id = Guid.NewGuid(), OrganizationId = tenant, ProjectId = projectId,
                    OwnerId = actorId, Name = "Método", CreatedAt = _clock.GetUtcNow() };
                db.Add(board); // Project.DefaultBoardId e os quadros anteriores permanecem como estavam.
            }
            var sameBoard = settings?.BoardId == board.Id;
            var definitions = await db.Set<ProjectCustomFieldDefinition>().Where(x => x.ProjectId == projectId).ToListAsync(cancellationToken);
            var selected = new Dictionary<string, ProjectCustomFieldDefinition>(StringComparer.Ordinal);
            foreach (var name in FieldNames)
            {
                var matches = definitions.Where(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (matches.Length > 1) throw Conflict($"O campo {name} está duplicado. Resolva a ambiguidade antes de configurar o método.");
                var field = matches.SingleOrDefault();
                if (field is not null && (field.Name != name || field.Type != CustomFieldType.Text || !field.IsActive))
                    throw Conflict($"O campo {name} precisa ter esse nome canônico, estar ativo e ser do tipo texto. A configuração não altera o campo existente.");
                if (field is null)
                {
                    field = ProjectCustomFieldDefinition.Create(projectId, name, CustomFieldType.Text, false, null,
                        definitions.Select(x => x.Position).DefaultIfEmpty(0).Max() + 100);
                    field.CreatedAt = _clock.GetUtcNow(); definitions.Add(field); db.Add(field);
                }
                selected.Add(name, field);
            }
            // Não abandona silenciosamente um campo configurado/renomeado e seus valores legados.
            if (settings is not null)
                foreach (var (fieldId, name) in new[] { (settings.MethodIdFieldId, "ID do metodo"), (settings.HuFieldId, "HU"), (settings.PrFieldId, "PR") })
                    if (fieldId != selected[name].Id) throw Conflict($"O campo configurado {name} diverge da definição canônica. Revise a configuração sem substituir dados legados.");

            var stages = await db.Stages.Where(x => x.ProjectId == projectId && x.BoardId == board.Id).ToListAsync(cancellationToken);
            var mapping = new Dictionary<string, Stage>(StringComparer.Ordinal);
            foreach (var column in Columns)
            {
                var named = stages.Where(x => string.Equals(x.Name, column.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
                var old = sameBoard ? settings!.StageMappings.SingleOrDefault(x => x.StatusCode == column.Code) : null;
                var stage = old is null ? null : stages.SingleOrDefault(x => x.Id == old.StageId);
                if (stage is null)
                {
                    if (named.Length > 1) throw Conflict($"Há mais de uma coluna {column.Name}. Selecione um mapeamento sem ambiguidade.");
                    stage = named.SingleOrDefault();
                    if (stage is null)
                    {
                        stage = new Stage { Id = Guid.NewGuid(), ProjectId = projectId, BoardId = board.Id,
                            Name = column.Name, Category = column.Category,
                            Position = stages.Select(x => x.Position).DefaultIfEmpty(0).Max() + 100, CreatedAt = _clock.GetUtcNow() };
                        stages.Add(stage); db.Add(stage);
                    }
                }
                if (!ValidCategory(column.Code, stage.Category))
                    throw Conflict($"A categoria da coluna {column.Name} é incompatível. Ajuste-a explicitamente; a configuração não altera cards nem categorias existentes.");
                mapping.Add(column.Code, stage);
            }
            if (mapping.Values.Select(x => x.Id).Distinct().Count() != 7) throw Conflict("Cada status precisa de uma coluna distinta.");
            if (settings is null)
            {
                settings = new MethodProjectSettings { ProjectId = projectId, OrganizationId = tenant,
                    CreatedAtUtc = _clock.GetUtcNow(), Enabled = false };
                db.Add(settings);
            }
            var changed = !existing || settings.BoardId != board.Id || settings.AutonomyLevel != request.AutonomyLevel
                || settings.GitPhase != MethodGitPhase.Webhooks || definitions.Any(x => db.Entry(x).State == EntityState.Added);
            if (existing && !sameBoard) settings.Enabled = false;
            settings.BoardId = board.Id; settings.AutonomyLevel = request.AutonomyLevel; settings.GitPhase = MethodGitPhase.Webhooks;
            settings.MethodIdFieldId = selected["ID do metodo"].Id; settings.HuFieldId = selected["HU"].Id; settings.PrFieldId = selected["PR"].Id;
            foreach (var old in settings.StageMappings.Where(x => !MethodStatusCodes.IsValid(x.StatusCode)).ToArray())
            { settings.StageMappings.Remove(old); db.Remove(old); changed = true; }
            foreach (var pair in mapping)
            {
                var row = settings.StageMappings.SingleOrDefault(x => x.StatusCode == pair.Key);
                if (row is null)
                {
                    settings.StageMappings.Add(new MethodStageMapping { ProjectId = projectId, StatusCode = pair.Key,
                        StageId = pair.Value.Id, CreatedAtUtc = _clock.GetUtcNow() });
                    changed = true;
                }
                else if (row.StageId != pair.Value.Id) { row.StageId = pair.Value.Id; changed = true; }
            }
            if (changed)
            {
                settings.UpdatedAtUtc = _clock.GetUtcNow();
                if (existing) settings.PolicyVersion = checked(settings.PolicyVersion + 1);
            }
            if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(cancellationToken);
            return Snapshot(project, board, settings, definitions, settings.Enabled);
        }, cancellationToken);
    }

    public async Task<MethodProjectSnapshot> UpdateSettingsAsync(Guid projectId, UpdateMethodProjectRequest request,
        string actorId, CancellationToken cancellationToken = default)
    {
        var tenant = await RequireAdminAsync(projectId, actorId, cancellationToken);
        ValidateAutonomy(request.AutonomyLevel);
        return await InTransactionAsync(async () =>
        {
            var project = await ProjectAsync(projectId, tenant, cancellationToken);
            var settings = await SettingsAsync(projectId, tenant, cancellationToken)
                ?? throw new MethodOperationException(404, "method_not_provisioned", "Configure o método explicitamente antes de ativá-lo.");
            var board = await db.Boards.SingleOrDefaultAsync(x => x.Id == settings.BoardId && x.ProjectId == projectId && x.OrganizationId == tenant, cancellationToken);
            var definitions = await db.Set<ProjectCustomFieldDefinition>().Where(x => x.ProjectId == projectId).ToListAsync(cancellationToken);
            if (request.Enabled)
            {
                var columns = await db.Stages.Where(x => x.BoardId == settings.BoardId && x.ProjectId == projectId).ToListAsync(cancellationToken);
                if (board is null || settings.StageMappings.Count != 7 || settings.StageMappings.Any(x => !MethodStatusCodes.IsValid(x.StatusCode))
                    || settings.StageMappings.Select(x => x.StageId).Distinct().Count() != 7
                    || settings.StageMappings.Any(map => !columns.Any(stage => stage.Id == map.StageId
                        && ValidCategory(map.StatusCode, stage.Category))))
                    throw Conflict("O mapeamento das sete colunas está indisponível ou incompatível. Revise a configuração antes de ativar.");
                foreach (var name in FieldNames)
                    if (definitions.Count(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)) != 1
                        || !definitions.Any(x => x.Name == name && x.IsActive && x.Type == CustomFieldType.Text))
                        throw Conflict($"O campo {name} está ausente, inativo, duplicado ou incompatível.");
                if (!definitions.Any(x => x.Id == settings.MethodIdFieldId && x.Name == "ID do metodo")
                    || !definitions.Any(x => x.Id == settings.HuFieldId && x.Name == "HU")
                    || !definitions.Any(x => x.Id == settings.PrFieldId && x.Name == "PR"))
                    throw Conflict("As referências dos campos do método precisam pertencer às definições canônicas deste projeto.");
            }
            if (settings.Enabled != request.Enabled || settings.AutonomyLevel != request.AutonomyLevel
                || (request.Enabled && settings.GitPhase != MethodGitPhase.Webhooks))
            {
                settings.Enabled = request.Enabled; settings.AutonomyLevel = request.AutonomyLevel;
                if (request.Enabled) settings.GitPhase = MethodGitPhase.Webhooks;
                settings.PolicyVersion = checked(settings.PolicyVersion + 1); settings.UpdatedAtUtc = _clock.GetUtcNow();
                await db.SaveChangesAsync(cancellationToken);
            }
            return Snapshot(project, board, settings, definitions, request.Enabled);
        }, cancellationToken);
    }

    private async Task<Guid> RequireAdminAsync(Guid projectId, string actorId, CancellationToken ct)
    {
        var actor = access.Current;
        if (actor?.IsInteractiveHuman != true || string.IsNullOrWhiteSpace(actorId) || actorId.Length > 450
            || actor.ActorId != actorId || actor.OrganizationId is null
            || organization.OrganizationId != actor.OrganizationId)
            throw new MethodOperationException(403, "method_provision_permission", "Configuração do método exige sessão humana da UI e permissão administrativa no projeto.");
        await permissions.EnsureAsync(actorId, PlatformPermission.ManagePermissions, PermissionScope.Project, projectId, ct);
        return actor.OrganizationId.Value;
    }
    private async Task<Project> ProjectAsync(Guid id, Guid tenant, CancellationToken ct) => await db.Projects
        .SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == tenant && !x.IsArchived, ct)
        ?? throw new MethodOperationException(404, "method_project_not_found", "Projeto indisponível nesta organização.");
    private Task<MethodProjectSettings?> SettingsAsync(Guid projectId, Guid tenant, CancellationToken ct) => db.Set<MethodProjectSettings>()
        .Include(x => x.StageMappings).SingleOrDefaultAsync(x => x.ProjectId == projectId && x.OrganizationId == tenant, ct);
    private static void ValidateAutonomy(int level)
    { if (level is < 0 or > 2) throw Invalid("O nível de autonomia deve estar entre 0 e 2; ele não habilita gate, deploy ou reconciliação."); }
    private static bool ValidCategory(string code, StageCategory category) => Enum.IsDefined(category)
        && ((code == MethodStatusCodes.Completed) == (category == StageCategory.Done));
    private static MethodProjectSnapshot Snapshot(Project project, Board? board, MethodProjectSettings settings,
        IReadOnlyList<ProjectCustomFieldDefinition> definitions, bool enabled) => new(settings.OrganizationId, project.Id, project.Name,
            board?.Id ?? Guid.Empty, board?.Name ?? "Quadro indisponível", settings.MethodIdFieldId, settings.HuFieldId, settings.PrFieldId,
            enabled, settings.GitPhase, settings.AutonomyLevel, settings.PolicyVersion,
            settings.StageMappings.ToDictionary(x => x.StatusCode, x => x.StageId, StringComparer.Ordinal),
            definitions.Where(x => x.IsActive && x.Type == CustomFieldType.Text).GroupBy(x => x.Name, StringComparer.Ordinal)
                .Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.Single().Id, StringComparer.Ordinal));
    private async Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        try
        {
            if (db.Database.CurrentTransaction is not null) return await action();
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                try { var result = await action(); await transaction.CommitAsync(ct); return result; }
                catch { await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
            });
        }
        catch (DbUpdateConcurrencyException)
        { throw Conflict("A configuração foi alterada por outra operação. Atualize os dados e tente novamente."); }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
        { throw Conflict("A estrutura foi configurada por outra operação. Atualize os dados antes de tentar novamente."); }
    }
    private static MethodOperationException Invalid(string message) => new(400, "invalid_method_configuration", message);
    private static MethodOperationException Conflict(string message) => new(409, "method_configuration_conflict", message);
}
