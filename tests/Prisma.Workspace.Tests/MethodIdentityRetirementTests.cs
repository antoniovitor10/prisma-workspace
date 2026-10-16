using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Xunit;
using GuardFixture = Prisma.Workspace.Tests.MethodGenericWriteGuardTests.GuardFixture;

namespace Prisma.Workspace.Tests;

public sealed class MethodIdentityRetirementTests
{
    [Theory]
    [InlineData("agente")]
    [InlineData("sistema")]
    public async Task MethodPrincipalCannotRemoveStableIdentityThroughGenericFieldWrite(string role)
    {
        await using var f = await GuardFixture.CreateAsync();
        await f.MoveAsync(f.Ui, MethodStatusCodes.InDevelopment);
        await using var scope = f.Scope(await f.TokenAsync(role));
        var field = await scope.Db.WorkItemCustomFieldValues.SingleAsync(x => x.WorkItemId == f.ItemId);
        field.Value = null;
        Assert.Equal(403, (await Assert.ThrowsAsync<MethodOperationException>(() => scope.Db.SaveChangesAsync())).StatusCode);
        Assert.Equal("T-0001", (await f.Seed.Db.WorkItemCustomFieldValues.AsNoTracking().SingleAsync(x => x.WorkItemId == f.ItemId)).Value);
        Assert.NotNull((await f.Seed.Db.Set<MethodTaskState>().AsNoTracking().SingleAsync()).ActiveAuthorizationEventId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("T-0002")]
    public async Task HumanUiCanRemoveOrReplaceIdWithoutMovingCardOrReusingAuthorization(string? value)
    {
        await using var f = await GuardFixture.CreateAsync();
        await f.MoveAsync(f.Ui, MethodStatusCodes.InDevelopment);
        var before = await f.Seed.Db.Set<MethodTaskState>().AsNoTracking().SingleAsync();
        var historyCount = await f.Seed.Db.StageHistories.CountAsync();
        await using var scope = f.Scope(f.Ui);
        (await scope.Db.WorkItemCustomFieldValues.SingleAsync(x => x.WorkItemId == f.ItemId)).Value = value;
        await scope.Db.SaveChangesAsync();
        var after = await scope.Db.Set<MethodTaskState>().SingleAsync();
        Assert.Equal(value, after.MethodTaskId); Assert.Null(after.ActiveAuthorizationEventId); Assert.NotEqual(before.CycleId, after.CycleId);
        Assert.Equal(f.Stages[MethodStatusCodes.InDevelopment], (await f.StoredAsync()).StageId);
        Assert.Equal(historyCount, await scope.Db.StageHistories.CountAsync());
        Assert.True(await scope.Db.Set<MethodAuthorizationEvent>().AnyAsync(x => x.Id == before.ActiveAuthorizationEventId));
        Assert.Single(await scope.Db.Set<MethodAuthorizationEvent>().Where(x => x.EventKind == "Revoked").ToArrayAsync());
        if (value is null) Assert.Null(await scope.Store.GetTaskAsync(f.ItemId));
        else Assert.Null((await scope.Store.GetTaskAsync(f.ItemId))!.ActiveAuthorizationEventId);
    }
}
