using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Infrastructure.Persistence;
using Detran.Kanban.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Detran.Kanban.Tests;

public class MultitenancyPersistenceTests
{
    private sealed class OrganizationContext(Guid? organizationId) : IOrganizationContext
    {
        public Guid? OrganizationId { get; } = organizationId;
    }

    [Fact]
    public async Task GlobalFilters_ReturnOnlyTheActiveOrganization()
    {
        var database = $"tenant-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(database)
            .Options;
        var organizationA = Guid.NewGuid();
        var organizationB = Guid.NewGuid();

        await using (var seed = new AppDbContext(options))
        {
            seed.Organizations.AddRange(
                NewOrganization(organizationA, "a"),
                NewOrganization(organizationB, "b"));
            seed.Boards.AddRange(
                NewBoard(organizationA, "Quadro A"),
                NewBoard(organizationB, "Quadro B"));
            await seed.SaveChangesAsync();
        }

        await using var contextA = new AppDbContext(options, new OrganizationContext(organizationA));
        var boards = await contextA.Boards.AsNoTracking().ToListAsync();

        var board = Assert.Single(boards);
        Assert.Equal("Quadro A", board.Name);
    }

    [Fact]
    public async Task SaveChanges_AutomaticallyStampsNewRootAndRejectsAnotherTenant()
    {
        var organizationId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tenant-{Guid.NewGuid()}")
            .Options;
        await using var context = new AppDbContext(options, new OrganizationContext(organizationId));
        context.Organizations.Add(NewOrganization(organizationId, "current"));
        var team = Team.Criar("Plataforma");
        context.Teams.Add(team);

        await context.SaveChangesAsync();
        Assert.Equal(organizationId, team.OrganizationId);

        context.Teams.Add(new Team
        {
            Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), Name = "Outra",
            DefaultWeeklyCapacityHours = 40, CreatedAt = DateTimeOffset.UtcNow
        });
        await Assert.ThrowsAsync<Detran.Kanban.Domain.Exceptions.DomainException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task ProjectRepository_ExplicitlyInsertsNewCustomFieldAndHistoryEvent()
    {
        var organizationId = Guid.NewGuid();
        var database = $"project-customization-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(database)
            .Options;
        var project = Project.Criar("DEMO", "Projeto demo", "owner", WorkNature.Project, WorkType.Development);
        project.OrganizationId = organizationId;

        await using (var seed = new AppDbContext(options))
        {
            seed.Organizations.Add(NewOrganization(organizationId, "demo"));
            seed.Projects.Add(project);
            await seed.SaveChangesAsync();
        }

        await using (var context = new AppDbContext(options, new OrganizationContext(organizationId)))
        {
            var repository = new ProjectRepository(context);
            var field = ProjectCustomFieldDefinition.Create(
                project.Id, "Protocolo", CustomFieldType.Text, false, null, 100);
            var projectEvent = ProjectEvent.Register(project.Id, "owner", "custom_field_saved");

            repository.AddCustomField(field);
            repository.AddEvent(projectEvent);
            await repository.SaveAsync();
        }

        await using var assertion = new AppDbContext(options, new OrganizationContext(organizationId));
        Assert.Single(await assertion.ProjectCustomFields.ToListAsync());
        Assert.Single(await assertion.ProjectEvents.ToListAsync());
    }

    private static Organization NewOrganization(Guid id, string slug) => new()
    {
        Id = id, Name = slug.ToUpperInvariant(), Slug = slug, IsActive = true,
        Locale = "pt-BR", TimeZone = "America/Sao_Paulo", WeekStartDay = DayOfWeek.Monday,
        CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
    };

    private static Board NewBoard(Guid organizationId, string name) => new()
    {
        Id = Guid.NewGuid(), OrganizationId = organizationId, Name = name,
        OwnerId = "user", CreatedAt = DateTimeOffset.UtcNow
    };
}
