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
        await fixture.Repository.UpdateStageAsync(fixture.SourceStage.Id,
            "Conclusão", StageCategory.Done, null, true, "test", default);
        fixture.Context.ChangeTracker.Clear();
        Assert.NotNull((await fixture.Context.WorkItems.SingleAsync(x => x.Id == fixture.Item.Id)).CompletedAt);
        Assert.Equal(StageCategory.Backlog, (await fixture.Context.Stages.SingleAsync(x => x.Id == fixture.TargetStage.Id)).Category);
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
