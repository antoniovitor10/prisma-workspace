using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Repositories;

namespace Prisma.Workspace.Tests;

public class BoardStructureSqlTests
{
    [Fact]
    public async Task DeleteBoard_FailureRollsBackTaskStageAndHistory()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.ExternalPortals.Add(ExternalPortal.Create(fixture.OrganizationId, fixture.Project.Id,
            fixture.Source.Id, "d89-" + Guid.NewGuid().ToString("N"), false, false, ExternalPortalAccessMode.PublicLink));
        await fixture.Context.SaveChangesAsync();
        var initialHistory = fixture.Item.StageHistories.Single().Id;

        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Repository.DeleteBoardAsync(
            fixture.Source.Id, fixture.Target.Id, fixture.TargetStage.Id, "test", default));
        fixture.Context.ChangeTracker.Clear();
        var item = await fixture.Context.WorkItems.Include(x => x.StageHistories).SingleAsync(x => x.Id == fixture.Item.Id);
        Assert.Equal(fixture.Source.Id, item.BoardId);
        Assert.Equal(fixture.SourceStage.Id, item.StageId);
        Assert.Equal(initialHistory, Assert.Single(item.StageHistories).Id);
        Assert.Null(item.StageHistories.Single().LeftAt);
        Assert.Equal(fixture.Source.Id, (await fixture.Context.Stages.SingleAsync(x => x.Id == fixture.SourceStage.Id)).BoardId);
    }

    [Fact]
    public async Task DeleteBoard_TransfersArchivedTaskAndPreservesHistory()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Item.IsArchived = true;
        await fixture.Context.SaveChangesAsync();
        var initialHistory = fixture.Item.StageHistories.Single().Id;
        await fixture.Repository.DeleteBoardAsync(fixture.Source.Id, fixture.Target.Id, fixture.TargetStage.Id, "test", default);
        fixture.Context.ChangeTracker.Clear();
        var item = await fixture.Context.WorkItems.Include(x => x.StageHistories).SingleAsync(x => x.Id == fixture.Item.Id);
        Assert.Equal(fixture.Target.Id, item.BoardId);
        Assert.Equal(fixture.TargetStage.Id, item.StageId);
        Assert.True(item.IsArchived);
        Assert.Contains(item.StageHistories, x => x.Id == initialHistory && x.LeftAt != null);
        Assert.Null((await fixture.Context.Stages.SingleAsync(x => x.Id == fixture.SourceStage.Id)).BoardId);
        Assert.False(await fixture.Context.Boards.AnyAsync(x => x.Id == fixture.Source.Id));
    }

    [Fact]
    public async Task Reorder_RejectsForeignColumnWithoutPartialWrite()
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<DomainException>(() => fixture.Repository.ReorderAsync(
            fixture.Source.Id, [fixture.SourceStage.Id, fixture.TargetStage.Id], default));
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(175, (await fixture.Context.Stages.SingleAsync(x => x.Id == fixture.SourceStage.Id)).Position);
    }

    [Fact]
    public async Task Reclassify_RequiresConfirmationAndUpdatesOnlySelectedBoard()
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<DomainException>(() => fixture.Repository.UpdateStageAsync(fixture.SourceStage.Id,
            "Conclusão", StageCategory.Done, null, false, "test", default));
        fixture.Context.ChangeTracker.Clear();
        var impact = await fixture.Repository.GetStageImpactAsync(fixture.SourceStage.Id, StageCategory.Done, default);
        await fixture.Repository.UpdateStageAsync(fixture.SourceStage.Id,
            "Conclusão", StageCategory.Done, null, true, "test", default, impactToken: impact.SnapshotToken);
        fixture.Context.ChangeTracker.Clear();
        Assert.NotNull((await fixture.Context.WorkItems.SingleAsync(x => x.Id == fixture.Item.Id)).CompletedAt);
        Assert.Equal(StageCategory.Backlog, (await fixture.Context.Stages.SingleAsync(x => x.Id == fixture.TargetStage.Id)).Category);
    }

    [Fact]
    public async Task Transfer_MovesEntireTreeIncludingArchivedDescendantsWithIndividualHistory()
    {
        await using var fixture = await Fixture.CreateAsync();
        var child = await fixture.AddChildAsync(fixture.Item.Id);
        var archived = await fixture.AddChildAsync(fixture.Item.Id, archived: true);
        var grandchild = await fixture.AddChildAsync(archived.Id);
        var expectedParents = new Dictionary<Guid, Guid?>
        {
            [fixture.Item.Id] = null, [child.Id] = fixture.Item.Id,
            [archived.Id] = fixture.Item.Id, [grandchild.Id] = archived.Id
        };
        var ids = expectedParents.Keys.ToList();
        var originalHistoryIds = await fixture.Context.StageHistories
            .Where(x => ids.Contains(x.WorkItemId)).Select(x => x.Id).ToListAsync();

        await fixture.Repository.TransferAsync(fixture.Item.Id, fixture.Target.Id, fixture.TargetStage.Id, "test", default);

        fixture.Context.ChangeTracker.Clear();
        var items = await fixture.Context.WorkItems.IgnoreQueryFilters().Include(x => x.StageHistories)
            .Where(x => ids.Contains(x.Id)).ToListAsync();
        Assert.Equal(4, items.Count);
        foreach (var item in items)
        {
            Assert.Equal(fixture.Target.Id, item.BoardId);
            Assert.Equal(fixture.TargetStage.Id, item.StageId);
            Assert.Equal(expectedParents[item.Id], item.ParentId);
            Assert.Null(item.CompletedAt);
            Assert.Equal(2, item.StageHistories.Count);
            Assert.Contains(item.StageHistories, x => originalHistoryIds.Contains(x.Id) && x.LeftAt != null);
            Assert.Equal(fixture.TargetStage.Id, Assert.Single(item.StageHistories.Where(x => x.LeftAt == null)).StageId);
            var entry = Assert.Single(await fixture.Context.TaskEvents.Where(x => x.WorkItemId == item.Id).ToListAsync());
            Assert.Equal("stage_changed", entry.Kind);
            Assert.Contains(fixture.Target.Id.ToString(), entry.Payload);
        }
        Assert.True(items.Single(x => x.Id == archived.Id).IsArchived);
        Assert.NotNull(items.Single(x => x.Id == archived.Id).ArchivedAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Transfer_ToDoneRejectsOpenDescendantWithoutChangingAnyItem(bool archived)
    {
        await using var fixture = await Fixture.CreateAsync();
        var child = await fixture.AddChildAsync(fixture.Item.Id, archived);
        child.CompletedAt = DateTimeOffset.UtcNow;
        var grandchild = await fixture.AddChildAsync(child.Id, archived);
        fixture.TargetStage.Category = StageCategory.Done;
        await fixture.Context.SaveChangesAsync();
        var ids = new[] { fixture.Item.Id, child.Id, grandchild.Id };

        await Assert.ThrowsAsync<DomainException>(() => fixture.Repository.TransferAsync(
            fixture.Item.Id, fixture.Target.Id, fixture.TargetStage.Id, "test", default));

        fixture.Context.ChangeTracker.Clear();
        var items = await fixture.Context.WorkItems.IgnoreQueryFilters().Include(x => x.StageHistories)
            .Where(x => ids.Contains(x.Id)).ToListAsync();
        Assert.Equal(3, items.Count);
        foreach (var item in items)
        {
            Assert.Equal(fixture.Source.Id, item.BoardId);
            Assert.Equal(fixture.SourceStage.Id, item.StageId);
            Assert.Null(Assert.Single(item.StageHistories).LeftAt);
        }
        Assert.Null(items.Single(x => x.Id == fixture.Item.Id).CompletedAt);
        Assert.Null(items.Single(x => x.Id == grandchild.Id).CompletedAt);
        Assert.False(await fixture.Context.TaskEvents.AnyAsync(x => ids.Contains(x.WorkItemId)));
    }

    [Fact]
    public async Task Reclassify_RequiresRecursiveConsentAndReopensOnlyColumnWithoutDuplicateHistory()
    {
        await using var fixture = await Fixture.CreateAsync();
        var child = await fixture.AddChildAsync(fixture.Item.Id, archived: true);
        child.StageId = fixture.TargetStage.Id;
        child.BoardId = fixture.Target.Id;
        var grandchild = await fixture.AddChildAsync(child.Id);
        grandchild.StageId = fixture.TargetStage.Id;
        grandchild.BoardId = fixture.Target.Id;
        var doneChild = await fixture.AddChildAsync(fixture.Item.Id);
        doneChild.StageId = fixture.TargetStage.Id;doneChild.BoardId = fixture.Target.Id;
        doneChild.CompletedAt = DateTimeOffset.UtcNow;
        await fixture.Context.SaveChangesAsync();
        var ids = new[] { fixture.Item.Id, child.Id, grandchild.Id, doneChild.Id };
        var impact = await fixture.Repository.GetStageImpactAsync(fixture.SourceStage.Id, StageCategory.Done, default);
        Assert.Equal(1, impact.TotalItems);Assert.Equal(1, impact.ChangedItems);Assert.Equal(2, impact.OpenDescendants);
        await Assert.ThrowsAsync<DomainException>(() => fixture.Repository.UpdateStageAsync(fixture.SourceStage.Id,
            "Concluída", StageCategory.Done, null, true, "test", default, impactToken: impact.SnapshotToken));
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(StageCategory.Backlog, (await fixture.Context.Stages.SingleAsync(x => x.Id == fixture.SourceStage.Id)).Category);
        Assert.False(await fixture.Context.TaskEvents.AnyAsync(x => ids.Contains(x.WorkItemId)));

        await fixture.Repository.UpdateStageAsync(fixture.SourceStage.Id, "Concluída", StageCategory.Done, null,
            true, "test", default, true, impact.SnapshotToken);
        fixture.Context.ChangeTracker.Clear();
        var items = await fixture.Context.WorkItems.IgnoreQueryFilters().Where(x => ids.Contains(x.Id)).ToListAsync();
        Assert.All(items, item => Assert.NotNull(item.CompletedAt));
        Assert.Equal(fixture.TargetStage.Id, items.Single(x => x.Id == child.Id).StageId);
        Assert.Equal(fixture.Target.Id, items.Single(x => x.Id == grandchild.Id).BoardId);
        Assert.True(items.Single(x => x.Id == child.Id).IsArchived);
        Assert.Equal(3, await fixture.Context.TaskEvents.CountAsync(x => ids.Contains(x.WorkItemId)));
        Assert.False(await fixture.Context.TaskEvents.AnyAsync(x => x.WorkItemId == doneChild.Id));

        var reopen = await fixture.Repository.GetStageImpactAsync(fixture.SourceStage.Id, StageCategory.Backlog, default);
        Assert.Equal(0, reopen.OpenDescendants);
        await fixture.Repository.UpdateStageAsync(fixture.SourceStage.Id, "Aberta", StageCategory.Backlog, null,
            true, "test", default, impactToken: reopen.SnapshotToken);
        fixture.Context.ChangeTracker.Clear();
        Assert.Null((await fixture.Context.WorkItems.SingleAsync(x => x.Id == fixture.Item.Id)).CompletedAt);
        Assert.NotNull((await fixture.Context.WorkItems.SingleAsync(x => x.Id == child.Id)).CompletedAt);
        Assert.Equal(4, await fixture.Context.TaskEvents.CountAsync(x => ids.Contains(x.WorkItemId)));
    }

    [Fact]
    public async Task Reclassify_RejectsStaleImpactWithoutPartialChanges()
    {
        await using var fixture = await Fixture.CreateAsync();
        var impact = await fixture.Repository.GetStageImpactAsync(fixture.SourceStage.Id, StageCategory.Done, default);
        await fixture.AddChildAsync(fixture.Item.Id);
        await Assert.ThrowsAsync<DomainException>(() => fixture.Repository.UpdateStageAsync(fixture.SourceStage.Id,
            "Conclusão", StageCategory.Done, null, true, "test", default, true, impact.SnapshotToken));
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(StageCategory.Backlog, (await fixture.Context.Stages.SingleAsync(x => x.Id == fixture.SourceStage.Id)).Category);
        Assert.Null((await fixture.Context.WorkItems.SingleAsync(x => x.Id == fixture.Item.Id)).CompletedAt);
        Assert.False(await fixture.Context.TaskEvents.AnyAsync(x => x.WorkItemId == fixture.Item.Id));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeleteStage_BlocksAutomationTriggerOrTargetWithoutPartialChanges(bool trigger)
    {
        await using var fixture = await Fixture.CreateAsync();
        var other = new Stage { Id=Guid.NewGuid(), ProjectId=fixture.Project.Id, BoardId=fixture.Source.Id,
            Name="Automação", Position=275, Category=StageCategory.Backlog, CreatedAt=DateTimeOffset.UtcNow };
        fixture.Context.Stages.Add(other);
        fixture.Context.AutomationRules.Add(new AutomationRule { Id=Guid.NewGuid(), BoardId=fixture.Source.Id,
            TriggerStageId=trigger ? fixture.SourceStage.Id : other.Id,
            ActionType=AutomationActionType.MoveToStage,
            ActionValue=(trigger ? other.Id : fixture.SourceStage.Id).ToString(), IsActive=false, CreatedAt=DateTimeOffset.UtcNow });
        await fixture.Context.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<DomainException>(() => fixture.Repository.DeleteStageAsync(
            fixture.SourceStage.Id, other.Id, "test", default));

        Assert.Contains("automação", error.Message);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(fixture.Source.Id, (await fixture.Context.Stages.SingleAsync(x => x.Id == fixture.SourceStage.Id)).BoardId);
        Assert.Equal(fixture.SourceStage.Id, (await fixture.Context.WorkItems.SingleAsync(x => x.Id == fixture.Item.Id)).StageId);
        Assert.Null((await fixture.Context.StageHistories.SingleAsync(x => x.WorkItemId == fixture.Item.Id)).LeftAt);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid OrganizationId { get; } = Guid.NewGuid();
        public AppDbContext Context { get; private set; } = null!;
        public BoardStructureRepository Repository => new(Context, new DirectoryFake());
        public Project Project { get; private set; } = null!;
        public Board Source { get; private set; } = null!;
        public Board Target { get; private set; } = null!;
        public Stage SourceStage { get; private set; } = null!;
        public Stage TargetStage { get; private set; } = null!;
        public WorkItem Item { get; private set; } = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var value = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            Assert.False(string.IsNullOrWhiteSpace(value), "Configure o banco E2E exclusivo.");
            Assert.StartsWith("Prisma_BoardColumns_", new SqlConnectionStringBuilder(value).InitialCatalog);
            var fixture = new Fixture();
            fixture.Context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(value).Options,
                new OrganizationContextFake(fixture.OrganizationId));
            var now = DateTimeOffset.UtcNow;
            fixture.Project = Project.Criar("T" + Guid.NewGuid().ToString("N")[..8], "D89 SQL", "test", WorkNature.Project, WorkType.Development);
            fixture.Project.OrganizationId = fixture.OrganizationId;
            fixture.Source = new Board { Id=Guid.NewGuid(), ProjectId=fixture.Project.Id, OrganizationId=fixture.OrganizationId, Name="Origem", OwnerId="test", CreatedAt=now };
            fixture.Target = new Board { Id=Guid.NewGuid(), ProjectId=fixture.Project.Id, OrganizationId=fixture.OrganizationId, Name="Destino", OwnerId="test", CreatedAt=now };
            fixture.SourceStage = new Stage { Id=Guid.NewGuid(), ProjectId=fixture.Project.Id, BoardId=fixture.Source.Id, Name="Livre", Position=175, Category=StageCategory.Backlog, CreatedAt=now };
            fixture.TargetStage = new Stage { Id=Guid.NewGuid(), ProjectId=fixture.Project.Id, BoardId=fixture.Target.Id, Name="Livre", Position=175, Category=StageCategory.Backlog, CreatedAt=now };
            fixture.Item = new WorkItem { Id=Guid.NewGuid(), BoardId=fixture.Source.Id, StageId=fixture.SourceStage.Id,
                Title="D89 SQL", CreatedAt=now, UpdatedAt=now, Position=175 };
            fixture.Item.StageHistories.Add(new StageHistory { Id=Guid.NewGuid(), WorkItemId=fixture.Item.Id, StageId=fixture.SourceStage.Id, EnteredAt=now });
            fixture.Context.AddRange(new Organization { Id=fixture.OrganizationId, Name="D89 SQL", Slug="d89-"+fixture.OrganizationId.ToString("N"), CreatedAt=now, UpdatedAt=now },
                fixture.Project, fixture.Source, fixture.Target, fixture.SourceStage, fixture.TargetStage, fixture.Item);
            await fixture.Context.SaveChangesAsync();
            return fixture;
        }
        public async Task<WorkItem> AddChildAsync(Guid parentId, bool archived = false)
        {
            var now = DateTimeOffset.UtcNow;
            var child = new WorkItem { Id=Guid.NewGuid(), BoardId=Source.Id, StageId=SourceStage.Id,
                ParentId=parentId, Title="D89 SQL descendente", CreatedAt=now, UpdatedAt=now,
                Position=175, IsArchived=archived, ArchivedAt=archived ? now : null };
            child.StageHistories.Add(new StageHistory { Id=Guid.NewGuid(), WorkItemId=child.Id,
                StageId=SourceStage.Id, EnteredAt=now });
            Context.WorkItems.Add(child);
            await Context.SaveChangesAsync();
            return child;
        }
        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed class OrganizationContextFake(Guid organizationId) : IOrganizationContext
    {
        public Guid? OrganizationId => organizationId;
    }
    private sealed class DirectoryFake : IUserDirectory
    {
        public Task<IReadOnlyDictionary<string,string>> GetDisplayNamesAsync(IEnumerable<string> ids, CancellationToken ct=default)
            => Task.FromResult<IReadOnlyDictionary<string,string>>(ids.ToDictionary(x=>x,_=>"Teste"));
        public Task<IReadOnlyList<UserSummary>> GetAllAsync(CancellationToken ct=default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserSummary>> GetByIdsAsync(IEnumerable<string> ids,bool includeInactive=false,CancellationToken cancellationToken=default) => throw new NotSupportedException();
        public Task<UserSummary?> GetByIdAsync(string id,CancellationToken ct=default) => throw new NotSupportedException();
        public Task<UserSummary?> GetByIdUnscopedAsync(string id,CancellationToken ct=default) => throw new NotSupportedException();
        public Task<UserSummary?> GetByEmailAsync(string email,CancellationToken ct=default) => throw new NotSupportedException();
    }
}
