using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Domain.Exceptions;

namespace Detran.Kanban.Tests;

public class ScrumDomainTests
{
    [Fact]
    public void CreateProject_NormalizesKey_AndMakesOwnerAdministrator()
    {
        var project = Project.Criar("  detran-ti ", "Plataforma Detran", "user-1", WorkNature.Project, WorkType.Development);

        Assert.Equal("DETRAN-TI", project.Key);
        var owner = Assert.Single(project.Members);
        Assert.Equal("user-1", owner.UserId);
        Assert.Equal(ProjectRole.ProjectAdmin, owner.Role);
    }

    [Fact]
    public void CreateSprint_RejectsEndBeforeStart()
    {
        var start = new DateOnly(2026, 7, 15);
        var end = start.AddDays(-1);

        Assert.Throws<DomainException>(() => Sprint.Criar(
            Guid.NewGuid(), Guid.NewGuid(), "Sprint 1", start, end, "Entregar backlog"));
    }

    [Fact]
    public void CreateSprint_StartsPlanned_WithGoal()
    {
        var start = new DateOnly(2026, 7, 15);
        var sprint = Sprint.Criar(Guid.NewGuid(), Guid.NewGuid(), "Sprint 1", start, start.AddDays(13), "Meta");

        Assert.Equal(SprintStatus.Planned, sprint.Status);
        Assert.Equal("Meta", sprint.Goal);
    }

    [Fact]
    public void ChangeSprintStatus_RequiresTheScrumLifecycle()
    {
        var sprint = Sprint.Criar(Guid.NewGuid(), Guid.NewGuid(), "Sprint 1",
            new DateOnly(2026, 7, 15), new DateOnly(2026, 7, 28), "Meta");

        Assert.Throws<DomainException>(() => sprint.ChangeStatus(SprintStatus.Closed));

        sprint.ChangeStatus(SprintStatus.Active);
        sprint.ChangeStatus(SprintStatus.Closed);

        Assert.Equal(SprintStatus.Closed, sprint.Status);
        Assert.Throws<DomainException>(() => sprint.ChangeStatus(SprintStatus.Active));
    }

    [Fact]
    public void ChangeSprintStatus_RejectsASecondActiveSprintForTheProject()
    {
        var sprint = Sprint.Criar(Guid.NewGuid(), Guid.NewGuid(), "Sprint 1",
            new DateOnly(2026, 7, 15), new DateOnly(2026, 7, 28), null);

        Assert.Throws<DomainException>(() => sprint.ChangeStatus(
            SprintStatus.Active, projectHasAnotherActiveSprint: true));
        Assert.Equal(SprintStatus.Planned, sprint.Status);
    }

    [Fact]
    public void CancelSprint_IsTerminal_AndPreservesScopeSnapshot()
    {
        var sprint = Sprint.Criar(Guid.NewGuid(), Guid.NewGuid(), "Sprint cancelada",
            new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 31), "Meta");
        var item = new WorkItem
        {
            Id = Guid.NewGuid(), Number = 1042, Title = "História pendente",
            Kind = WorkItemKind.UserStory, Points = 8, EstimatedHours = 12
        };

        sprint.CaptureHistory([item], SprintIncompleteItemsAction.ReturnToBacklog, null);
        sprint.ChangeStatus(SprintStatus.Cancelled);

        Assert.True(sprint.IsTerminal);
        Assert.NotNull(sprint.CancelledAt);
        var snapshot = Assert.Single(sprint.ItemSnapshots);
        Assert.Equal(SprintItemOutcome.ReturnedToBacklog, snapshot.Outcome);
        Assert.Equal(8, snapshot.Points);
        Assert.Throws<DomainException>(() => sprint.Update(
            "Novo nome", null, sprint.StartDate, sprint.EndDate));
    }

    [Fact]
    public void CompleteSprint_SnapshotKeepsCompletedAndMovedOutcomes()
    {
        var sprint = Sprint.Criar(Guid.NewGuid(), Guid.NewGuid(), "Sprint 1",
            new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 31), null);
        var destinationId = Guid.NewGuid();
        var completed = new WorkItem
        {
            Id = Guid.NewGuid(), Number = 1100, Title = "Entregue",
            Kind = WorkItemKind.Bug, Points = 3, CompletedAt = DateTimeOffset.UtcNow
        };
        var pending = new WorkItem
        {
            Id = Guid.NewGuid(), Number = 1101, Title = "Pendente",
            Kind = WorkItemKind.UserStory, Points = 5
        };

        sprint.CaptureHistory([completed, pending], SprintIncompleteItemsAction.MoveToSprint, destinationId);

        Assert.Equal(SprintItemOutcome.Completed,
            sprint.ItemSnapshots.Single(x => x.WorkItemId == completed.Id).Outcome);
        var moved = sprint.ItemSnapshots.Single(x => x.WorkItemId == pending.Id);
        Assert.Equal(SprintItemOutcome.MovedToSprint, moved.Outcome);
        Assert.Equal(destinationId, moved.DestinationSprintId);
    }

    [Fact]
    public void UpdateProject_RejectsDueDateBeforeStartDate()
    {
        var project = Project.Criar("OPS", "Operações", "owner-1", WorkNature.Sustainment, WorkType.Support);

        Assert.Throws<DomainException>(() => project.Update(
            "Operações",
            null,
            "owner-1",
            new DateOnly(2026, 8, 10),
            new DateOnly(2026, 8, 9),
            ProjectStatus.Active,
            ProjectMethodology.Scrumban,
            WorkNature.Sustainment,
            WorkType.Support,
            null));
    }

    [Fact]
    public void ArchiveAndReactivateProject_PreserveLifecycleRules()
    {
        var project = Project.Criar("PORTAL", "Portal de serviços", "owner-1", WorkNature.Project, WorkType.Development);
        project.Status = ProjectStatus.Completed;

        project.Archive();

        Assert.True(project.IsArchived);
        Assert.NotNull(project.ArchivedAt);

        project.Reactivate();

        Assert.False(project.IsArchived);
        Assert.Null(project.ArchivedAt);
        Assert.Equal(ProjectStatus.Active, project.Status);
    }

    [Fact]
    public void WorkItemLink_RejectsSelfReference()
    {
        var workItemId = Guid.NewGuid();

        Assert.Throws<DomainException>(() => WorkItemLink.Create(
            workItemId,
            workItemId,
            WorkItemLinkType.DependsOn,
            "user-1"));
    }

    [Fact]
    public void WorkItem_CanBeArchivedAndReactivated()
    {
        var workItem = new WorkItem { Id = Guid.NewGuid() };

        workItem.Archive();
        Assert.True(workItem.IsArchived);
        Assert.NotNull(workItem.ArchivedAt);

        workItem.Reactivate();
        Assert.False(workItem.IsArchived);
        Assert.Null(workItem.ArchivedAt);
    }
}
