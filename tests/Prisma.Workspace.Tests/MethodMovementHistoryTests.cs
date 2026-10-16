using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Xunit;

namespace Prisma.Workspace.Tests;

public sealed class MethodMovementHistoryTests
{
    [Fact]
    public async Task HumanMovementRecordsActualColumnNamesAndKeepsThemAfterRename()
    {
        await using var f = await MethodGenericWriteGuardTests.GuardFixture.CreateAsync();
        await RenameAsync(f, MethodStatusCodes.Backlog, "Fila do produto");
        await RenameAsync(f, MethodStatusCodes.InDevelopment, "Construção");
        await f.MoveAsync(f.Ui, MethodStatusCodes.InDevelopment);

        await AssertMovementAsync(f, "Fila do produto", "Construção", "ui");
        await RenameAsync(f, MethodStatusCodes.InDevelopment, "Novo nome da coluna");
        await AssertMovementAsync(f, "Fila do produto", "Construção", "ui");
    }

    [Fact]
    public async Task AgentBlockRecordsActualSourceAndDestinationNames()
    {
        await using var f = await MethodGenericWriteGuardTests.GuardFixture.CreateAsync();
        await RenameAsync(f, MethodStatusCodes.InDevelopment, "Construção");
        await RenameAsync(f, MethodStatusCodes.Blocked, "Aguardando dependência");
        await f.MoveAsync(f.Ui, MethodStatusCodes.InDevelopment);
        await f.MoveAsync(await f.TokenAsync("agente"), MethodStatusCodes.Blocked, "Serviço indisponível.");

        await AssertMovementAsync(f, "Construção", "Aguardando dependência", "mcp");
    }

    [Fact]
    public async Task ExistingEventCannotForgeColumnNames()
    {
        await using var f = await MethodGenericWriteGuardTests.GuardFixture.CreateAsync();
        await using var scope = f.Scope(f.Ui);
        var item = await scope.Db.WorkItems.SingleAsync(x => x.Id == f.ItemId);
        item.StageId = f.Stages[MethodStatusCodes.InDevelopment];
        scope.Db.TaskEvents.Add(TaskEvent.Registrar(item.Id, f.Ui.ActorId, "stage_changed",
            "{\"fromStageName\":\"Inventada\",\"toStageName\":\"Inventada\"}"));
        await scope.Db.SaveChangesAsync();

        await AssertMovementAsync(f, MethodStatusCodes.Backlog, MethodStatusCodes.InDevelopment, "ui");
    }

    [Fact]
    public async Task HumanTransferRecordsNameOfUnmappedDestinationColumn()
    {
        await using var f = await MethodGenericWriteGuardTests.GuardFixture.CreateAsync();
        var board = new Board { Id = Guid.NewGuid(), ProjectId = f.Seed.ProjectId, OrganizationId = f.Seed.OrgId,
            OwnerId = MethodGenericWriteGuardTests.GuardFixture.Human, Name = "Outro quadro" };
        var stage = new Stage { Id = Guid.NewGuid(), BoardId = board.Id, ProjectId = board.ProjectId,
            Name = "Triagem externa", Category = StageCategory.Ready };
        f.Seed.Db.AddRange(board, stage);
        await f.Seed.Db.SaveChangesAsync();
        await using var scope = f.Scope(f.Ui);
        var item = await scope.Db.WorkItems.SingleAsync(x => x.Id == f.ItemId);
        item.BoardId = board.Id; item.StageId = stage.Id;
        await scope.Db.SaveChangesAsync();

        await AssertMovementAsync(f, MethodStatusCodes.Backlog, "Triagem externa", "ui");
    }

    [Fact]
    public async Task MovementUsesColumnNameChangedInTheSameSave()
    {
        await using var f = await MethodGenericWriteGuardTests.GuardFixture.CreateAsync();
        await using var scope = f.Scope(f.Ui);
        var stage = await scope.Db.Stages.SingleAsync(x => x.Id == f.Stages[MethodStatusCodes.InDevelopment]);
        stage.Name = "Construção atualizada";
        var item = await scope.Db.WorkItems.SingleAsync(x => x.Id == f.ItemId);
        item.StageId = stage.Id;
        await scope.Db.SaveChangesAsync();

        await AssertMovementAsync(f, MethodStatusCodes.Backlog, "Construção atualizada", "ui");
    }

    private static async Task RenameAsync(MethodGenericWriteGuardTests.GuardFixture f, string status, string name)
    {
        (await f.Seed.Db.Stages.SingleAsync(x => x.Id == f.Stages[status])).Name = name;
        await f.Seed.Db.SaveChangesAsync();
    }

    private static async Task AssertMovementAsync(MethodGenericWriteGuardTests.GuardFixture f,
        string from, string to, string channel)
    {
        var entry = await f.Seed.Db.TaskEvents.AsNoTracking().Where(x => x.WorkItemId == f.ItemId
            && x.Kind == "stage_changed").OrderByDescending(x => x.CreatedAt).FirstAsync();
        using var payload = JsonDocument.Parse(entry.Payload!);
        Assert.Equal(from, payload.RootElement.GetProperty("fromStageName").GetString());
        Assert.Equal(to, payload.RootElement.GetProperty("toStageName").GetString());
        Assert.Equal(channel, payload.RootElement.GetProperty("channel").GetString());
    }
}
