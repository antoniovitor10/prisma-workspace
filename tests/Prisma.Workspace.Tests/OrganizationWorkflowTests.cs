using Prisma.Workspace.Application.Features.Workflow;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Tests;

public sealed class OrganizationWorkflowTests
{
    [Fact]
    public void Validate_RequiresExactlyOneActiveInitialStatus()
    {
        var input = new OrganizationWorkflowTemplateInput("Padrão", true,
        [
            new("TODO", "A fazer", "#64748B", 0, StageCategory.Ready, false, false),
            new("DOING", "Em andamento", "#3B82F6", 1, StageCategory.InProgress, false, false)
        ], []);

        var error = Assert.Throws<DomainException>(() => WorkflowTemplateProjection.Validate(input));

        Assert.Contains("exatamente um status inicial", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyToNewProject_MaterializesStableLocalStatusesAndTransitions()
    {
        var organizationId = Guid.NewGuid();
        var template = OrganizationWorkflowTemplate.Create(organizationId, "Padrão", true);
        var ready = OrganizationWorkflowStatus.Create(template.Id, "TODO", "A fazer", "#64748B", 0,
            StageCategory.Ready, true, false);
        var done = OrganizationWorkflowStatus.Create(template.Id, "DONE", "Concluído", "#22C55E", 1,
            StageCategory.Done, false, true);
        template.Statuses.Add(ready);
        template.Statuses.Add(done);
        template.Transitions.Add(OrganizationWorkflowTransition.Create(template.Id, ready.Id, done.Id));
        var project = Project.Criar("PRJ", "Projeto", "owner", WorkNature.Project, WorkType.Development);

        WorkflowTemplateProjection.ApplyToNewProject(project, template);

        Assert.Equal(WorkflowInheritanceMode.Inherited, project.WorkflowInheritanceMode);
        Assert.Equal(template.Id, project.WorkflowTemplateId);
        Assert.Equal(organizationId, project.OrganizationId);
        Assert.Equal(2, project.WorkflowStatuses.Count);
        Assert.All(project.WorkflowStatuses, status => Assert.NotNull(status.OrganizationWorkflowStatusId));
        var localReady = Assert.Single(project.WorkflowStatuses, status => status.OrganizationWorkflowStatusId == ready.Id);
        var transition = Assert.Single(localReady.OutgoingTransitions);
        Assert.Equal(project.WorkflowStatuses.Single(status => status.OrganizationWorkflowStatusId == done.Id).Id,
            transition.TargetStatusId);
    }

    [Fact]
    public async Task SetInheritance_PreservesEquivalentTransitionsAndPersistsWithoutConcurrencyConflict()
    {
        var organizationId = Guid.NewGuid();
        var template = OrganizationWorkflowTemplate.Create(organizationId, "Padrão", true);
        var templateReady = OrganizationWorkflowStatus.Create(template.Id, "READY", "A fazer", "#64748B", 0,
            StageCategory.Ready, true, false);
        var templateDone = OrganizationWorkflowStatus.Create(template.Id, "DONE", "Concluído", "#22C55E", 1,
            StageCategory.Done, false, true);
        template.Statuses.Add(templateReady);
        template.Statuses.Add(templateDone);
        template.Transitions.Add(OrganizationWorkflowTransition.Create(template.Id, templateReady.Id, templateDone.Id));

        var project = Project.Criar("FLOW", "Fluxo", "owner", WorkNature.Project, WorkType.Development);
        project.OrganizationId = organizationId;
        var localReady = WorkflowStatus.Create(project.Id, "A fazer", "#64748B", 0,
            StageCategory.Ready, true, false);
        var localDone = WorkflowStatus.Create(project.Id, "Concluído", "#22C55E", 1,
            StageCategory.Done, false, true);
        localReady.OutgoingTransitions.Add(WorkflowTransition.Create(localReady.Id, localDone.Id));
        project.WorkflowStatuses.Add(localReady);
        project.WorkflowStatuses.Add(localDone);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"workflow-inheritance-{Guid.NewGuid()}").Options;
        await using (var seed = new AppDbContext(options))
        {
            seed.Organizations.Add(new Organization
            {
                Id = organizationId, Name = "Organização", Slug = "organizacao", IsActive = true,
                Locale = "pt-BR", TimeZone = "America/Sao_Paulo",
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
            });
            seed.OrganizationWorkflowTemplates.Add(template);
            seed.Projects.Add(project);
            await seed.SaveChangesAsync();
        }

        await using (var context = new AppDbContext(options, new OrganizationContext(organizationId)))
        {
            var handler = new SetProjectWorkflowInheritanceCommandHandler(
                new OrganizationWorkflowRepository(context), new ProjectAccess());

            await handler.Handle(new SetProjectWorkflowInheritanceCommand(
                project.Id, WorkflowInheritanceMode.Inherited, template.Id, "owner"), default);
        }

        await using var assertion = new AppDbContext(options, new OrganizationContext(organizationId));
        var saved = await assertion.Projects.Include(x => x.WorkflowStatuses)
            .ThenInclude(x => x.OutgoingTransitions).SingleAsync(x => x.Id == project.Id);
        Assert.Equal(WorkflowInheritanceMode.Inherited, saved.WorkflowInheritanceMode);
        Assert.Equal(template.Id, saved.WorkflowTemplateId);
        Assert.All(saved.WorkflowStatuses, status => Assert.NotNull(status.OrganizationWorkflowStatusId));
        Assert.Single(saved.WorkflowStatuses.SelectMany(status => status.OutgoingTransitions));
    }

    private sealed class OrganizationContext(Guid organizationId) : IOrganizationContext
    {
        public Guid? OrganizationId { get; } = organizationId;
    }

    private sealed class ProjectAccess : IProjectAccessService
    {
        public Task<ProjectRole?> GetRoleAsync(Guid projectId, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectRole?>(ProjectRole.ProjectAdmin);

        public Task EnsureAtLeastAsync(Guid projectId, string userId, ProjectRole minimumRole,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
