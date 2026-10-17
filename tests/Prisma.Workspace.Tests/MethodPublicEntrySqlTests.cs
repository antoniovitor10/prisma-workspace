using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.ExternalPortal;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodSecurity;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Identity.MethodTokens;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Repositories;
using Prisma.Workspace.Infrastructure.Tenancy;
using Xunit;

namespace Prisma.Workspace.Tests;

[Collection("MethodGit SQL")]
public sealed class MethodPublicEntrySqlTests(GitDeliverySqlFixture fixture) : IClassFixture<GitDeliverySqlFixture>
{
    [MethodGitSqlFact]
    public async Task RealPortalAndFormHandlersNeverDeriveProjectAuthorizationFromRequesterIdentity()
    {
        foreach (var method in new[] { true, false })
        foreach (var development in new[] { true, false })
        foreach (var formRoute in new[] { true, false })
        {
            await using var c = await fixture.CaseAsync();
            var human = (await c.Context.Set<MethodAuthorizationEvent>().SingleAsync(x => x.WorkItemId == c.ItemId)).ActorUserId!;
            Guid projectId; Guid boardId; Guid stageId;
            if (method)
            {
                projectId = c.ProjectId;
                var settings = await c.Context.Set<MethodProjectSettings>().Include(x => x.StageMappings).SingleAsync(x => x.ProjectId == projectId);
                boardId = settings.BoardId!.Value;
                stageId = settings.StageMappings.Single(x => x.StatusCode == (development ? MethodStatusCodes.InDevelopment : MethodStatusCodes.Backlog)).StageId;
                foreach (var stage in await c.Context.Stages.Where(x => x.BoardId == boardId).ToArrayAsync()) stage.Position = stage.Id == stageId ? 0 : 100;
            }
            else
            {
                var project = Project.Criar("PUB" + Guid.NewGuid().ToString("N")[..6], "Projeto sem método", human, WorkNature.Project, WorkType.Development);
                project.OrganizationId = c.Repository.OrganizationId; projectId = project.Id;
                var board = new Board { Id = Guid.NewGuid(), ProjectId = projectId, OrganizationId = project.OrganizationId, OwnerId = human, Name = "Portal normal" };
                boardId = board.Id;
                var stage = new Stage { Id = Guid.NewGuid(), ProjectId = projectId, BoardId = board.Id, Name = "Coluna normal",
                    Category = development ? StageCategory.InProgress : StageCategory.Backlog };
                stageId = stage.Id; c.Context.AddRange(project, board, stage);
            }
            var portal = ExternalPortal.Create(c.Repository.OrganizationId, projectId, boardId,
                "public-method-" + Guid.NewGuid().ToString("N"), true, false, ExternalPortalAccessMode.PublicLink);
            var form = ExternalForm.CreateDefault(portal.Id, "Formulário próprio"); form.InitialStageId = stageId;
            c.Context.AddRange(portal, form); await c.Context.SaveChangesAsync();
            var before = await c.Context.Set<MethodAuthorizationEvent>().CountAsync(x => x.ProjectId == projectId);
            var tenant = new CurrentOrganizationContext();
            var audit = new CurrentAuditContext(); audit.Set(human, "portal", null, "public-entry-test");
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(c.Context.Database.GetConnectionString()).Options;
            await using var db = new AppDbContext(options, tenant, audit, new MethodAccessContext(), new EnabledFeature(), c.Clock);
            var repository = new ExternalPortalRepository(db);
            var title = "Pedido público " + Guid.NewGuid().ToString("N");
            async Task<CreatedExternalRequestDto> Submit()
            {
                if (!formRoute) return await new CreateExternalRequestCommandHandler(repository).Handle(new(portal.PublicSlug,
                    title, "Descrição sintética da solicitação", "Solicitante", "test@example.invalid", null, null, human, "test@example.invalid"), default);
                return await new SubmitExternalFormCommandHandler(repository, null!, null!).Handle(new(portal.PublicSlug, form.PublicSlug,
                    new Dictionary<string, string?> { ["requesterName"] = "Solicitante", ["requesterEmail"] = "test@example.invalid",
                        ["subject"] = title, ["description"] = "Descrição sintética da solicitação", ["category"] = "infra" },
                    [], DateTimeOffset.UtcNow.AddSeconds(-5), null, null, null, human, "test@example.invalid"), default);
            }
            if (method && development)
            {
                var denied = await Assert.ThrowsAsync<MethodOperationException>(Submit);
                Assert.Equal("interactive_authorization_required", denied.Code);
                Assert.False(await c.Context.WorkItems.IgnoreQueryFilters().AnyAsync(x => x.Title == title));
            }
            else
            {
                var created = await Submit();
                Assert.True(await c.Context.WorkItems.IgnoreQueryFilters().AnyAsync(x => x.Id == created.WorkItemId && x.StageId == stageId));
                Assert.False(await c.Context.Set<MethodTaskState>().AnyAsync(x => x.WorkItemId == created.WorkItemId));
            }
            Assert.Equal(before, await c.Context.Set<MethodAuthorizationEvent>().CountAsync(x => x.ProjectId == projectId));
        }
    }
    private sealed class EnabledFeature : IMethodFeatureConfiguration { public bool Enabled => true; }
}
