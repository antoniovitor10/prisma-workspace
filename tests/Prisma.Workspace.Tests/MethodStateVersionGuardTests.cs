using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Xunit;
using GuardFixture = Prisma.Workspace.Tests.MethodGenericWriteGuardTests.GuardFixture;

namespace Prisma.Workspace.Tests;

public sealed class MethodStateVersionGuardTests
{
    [Theory]
    [InlineData("agente")]
    [InlineData("humano")]
    [InlineData("sistema")]
    public async Task NoOpVersionPulseKeepsIdentityAndAuthorityForScopedEditor(string role)
    {
        await using var f = await GuardFixture.CreateAsync();
        await using var scope = f.Scope(await f.TokenAsync(role));
        var state = await scope.Db.Set<MethodTaskState>().SingleAsync();
        var cycle = state.CycleId;
        scope.Db.Entry(state).Property(x => x.MethodTaskId).IsModified = true;
        await scope.Db.SaveChangesAsync();
        var stored = await f.Seed.Db.Set<MethodTaskState>().AsNoTracking().SingleAsync();
        Assert.Equal("T-0001", stored.MethodTaskId); Assert.Equal(cycle, stored.CycleId);
        Assert.Null(stored.ActiveAuthorizationEventId);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("forged-original-id")]
    [InlineData("grant")]
    [InlineData("forged-original-grant")]
    [InlineData("forged-original-cycle")]
    [InlineData("grant-marked")]
    [InlineData("cycle")]
    [InlineData("resume")]
    [InlineData("new-state")]
    [InlineData("authorization-event")]
    [InlineData("audit")]
    public async Task VersionPulseDoesNotAllowActualAuthorityOrIdentityWrites(string attack)
    {
        await using var f = await GuardFixture.CreateAsync();
        await using var scope = f.Scope(await f.TokenAsync("agente"));
        var state = await scope.Db.Set<MethodTaskState>().SingleAsync();
        scope.Db.Entry(state).Property(x => x.MethodTaskId).IsModified = true;
        switch (attack)
        {
            case "id": state.MethodTaskId = "T-9999"; break;
            case "forged-original-id":
                state.MethodTaskId = "T-9999";
                scope.Db.Entry(state).Property(x => x.MethodTaskId).OriginalValue = "T-9999";
                break;
            case "grant": state.ActiveAuthorizationEventId = Guid.NewGuid(); break;
            case "forged-original-grant":
                state.ActiveAuthorizationEventId = Guid.NewGuid();
                scope.Db.Entry(state).Property(x => x.ActiveAuthorizationEventId).OriginalValue = state.ActiveAuthorizationEventId;
                scope.Db.Entry(state).Property(x => x.ActiveAuthorizationEventId).IsModified = false;
                break;
            case "forged-original-cycle":
                state.CycleId = Guid.NewGuid();
                scope.Db.Entry(state).Property(x => x.CycleId).OriginalValue = state.CycleId;
                scope.Db.Entry(state).Property(x => x.CycleId).IsModified = false;
                break;
            case "grant-marked": scope.Db.Entry(state).Property(x => x.ActiveAuthorizationEventId).IsModified = true; break;
            case "cycle": state.CycleId = Guid.NewGuid(); break;
            case "resume": state.ResumeApprovalEventId = Guid.NewGuid(); break;
            case "new-state": scope.Db.Add(new MethodTaskState { ProjectId = f.Seed.ProjectId,
                OrganizationId = f.Seed.OrgId, WorkItemId = Guid.NewGuid(), CycleId = Guid.NewGuid(), MethodTaskId = "T-9998" }); break;
            case "authorization-event": scope.Db.Add(new MethodAuthorizationEvent { Id = Guid.NewGuid(), ProjectId = f.Seed.ProjectId,
                OrganizationId = f.Seed.OrgId, WorkItemId = f.ItemId, CycleId = state.CycleId, EventKind = "Granted" }); break;
            case "audit": scope.Db.Add(new AuditLog { Id = Guid.NewGuid(), OrganizationId = f.Seed.OrgId }); break;
        }
        Assert.Equal(403, (await Assert.ThrowsAsync<MethodOperationException>(() => scope.Db.SaveChangesAsync())).StatusCode);
        Assert.Equal("T-0001", (await f.Seed.Db.Set<MethodTaskState>().AsNoTracking().SingleAsync()).MethodTaskId);
    }

    [Fact]
    public async Task VersionPulseCannotTouchAssociationFromAnotherProject()
    {
        await using var f = await GuardFixture.CreateAsync();
        var other = await f.Seed.AddProjectAsync();
        f.Seed.Db.Add(new MethodTaskState { ProjectId = other, OrganizationId = f.Seed.OrgId,
            WorkItemId = f.ItemId, CycleId = Guid.NewGuid() });
        await f.Seed.Db.SaveChangesAsync();
        await using var scope = f.Scope(await f.TokenAsync("agente"));
        var foreign = await scope.Db.Set<MethodTaskState>().IgnoreQueryFilters().SingleAsync(x => x.ProjectId == other);
        scope.Db.Entry(foreign).Property(x => x.MethodTaskId).IsModified = true;
        Assert.Equal(403, (await Assert.ThrowsAsync<MethodOperationException>(() => scope.Db.SaveChangesAsync())).StatusCode);
    }

    [Fact]
    public async Task ReadOnlyTokenCannotPulseTaskVersion()
    {
        await using var f = await GuardFixture.CreateAsync();
        var token = await f.Seed.Service().IssueAsync(f.Seed.ProjectId,
            new("Somente leitura", "agente", Permissions: [PlatformPermission.View]));
        var actor = (await f.Seed.Service().AuthenticateAsync(token.Token, "read-only-pulse"))!;
        await using var scope = f.Scope(actor);
        var state = await scope.Db.Set<MethodTaskState>().SingleAsync();
        scope.Db.Entry(state).Property(x => x.MethodTaskId).IsModified = true;
        Assert.Equal(403, (await Assert.ThrowsAsync<MethodOperationException>(() => scope.Db.SaveChangesAsync())).StatusCode);
    }
}
