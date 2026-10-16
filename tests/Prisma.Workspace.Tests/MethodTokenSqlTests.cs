using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Identity;
using Prisma.Workspace.Infrastructure.Identity.MethodTokens;
using Prisma.Workspace.Infrastructure.Tenancy;
using Xunit;

namespace Prisma.Workspace.Tests;

// Reusa a fixture pública de SQL descartável3132, sem alterar os testes da Ponte.
[Collection("MethodGit SQL")]
public sealed class MethodTokenSqlTests(GitDeliverySqlFixture fixture) : IClassFixture<GitDeliverySqlFixture>
{
    [MethodGitSqlFact]
    public async Task OpaqueTokenHashUsesSqlEqualityAndUniqueIndexWithImmediateRevocation()
    {
        await using var c = await fixture.CaseAsync();
        var grant = await c.Context.Set<MethodAuthorizationEvent>().SingleAsync(x => x.WorkItemId == c.ItemId);
        var humanId = grant.ActorUserId!;
        var user = await c.Context.Users.SingleAsync(x => x.Id == humanId);
        user.EmailConfirmed = true;
        c.Context.OrganizationMembers.Add(new OrganizationMember { OrganizationId = grant.OrganizationId,
            UserId = humanId, Role = OrganizationRole.Administrator, IsActive = true });
        await c.Context.SaveChangesAsync();

        var tenant = new CurrentOrganizationContext(); tenant.Set(grant.OrganizationId);
        var access = new MethodAccessContext();
        access.Initialize(new MethodActorIdentity(MethodAuthenticationKind.UiSession, MethodRole.Human,
            null, humanId, "Humano SQL", grant.OrganizationId, c.ProjectId, null, null,
            Enum.GetValues<PlatformPermission>().ToHashSet(), MethodChannels.Ui, "token-sql"));
        var service = new MethodTokenService(c.Context, access, new PermissionService(c.Context, tenant),
            new MethodPermissionEvaluator(c.Context, c.Clock), c.Clock);
        var issued = await service.IssueAsync(c.ProjectId, new("Executor SQL", "agente"));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(issued.Token));
        var query = c.Context.Set<MethodApiToken>().IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TokenHash.SequenceEqual(hash));
        var sql = query.ToQueryString();
        Assert.True(sql.Contains("[TokenHash] = @", StringComparison.Ordinal),
            "A consulta deve comparar o hash por igualdade parametrizada no SQL.");
        Assert.True((await query.SingleAsync()).Id == issued.Metadata.Id,
            "A igualdade precisa ser executável no SQL Server real.");

        var connection = c.Context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT COUNT(*) FROM sys.indexes i
                JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
                JOIN sys.columns col ON col.object_id=ic.object_id AND col.column_id=ic.column_id
                WHERE i.object_id=OBJECT_ID(N'dbo.MethodApiTokens') AND i.is_unique=1
                  AND col.name=N'TokenHash' AND ic.key_ordinal=1
                """;
            Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync()));
        }

        var actor = await service.AuthenticateAsync(issued.Token, "token-sql-auth");
        Assert.NotNull(actor); Assert.Equal(MethodRole.Agent, actor.Role);
        Assert.Equal(c.ProjectId, actor.ProjectId);
        var first = await c.Context.Set<MethodApiToken>().AsNoTracking().SingleAsync(x => x.Id == issued.Metadata.Id);
        Assert.Equal(c.Clock.Now, first.LastUsedAtUtc);
        c.Clock.Now = c.Clock.Now.AddMinutes(1);
        Assert.NotNull(await service.AuthenticateAsync(issued.Token, "token-sql-repeat"));
        var second = await c.Context.Set<MethodApiToken>().AsNoTracking().SingleAsync(x => x.Id == issued.Metadata.Id);
        Assert.True(first.RowVersion.SequenceEqual(second.RowVersion), "Uso na janela de5min não deve regravar o token.");
        // Authenticate usa ExecuteUpdate; descarta o token antigo rastreado pela emissão antes de revogar.
        c.Context.ChangeTracker.Clear();
        await service.RevokeAsync(c.ProjectId, issued.Metadata.Id);
        Assert.Null(await service.AuthenticateAsync(issued.Token, "token-sql-revoked"));
    }
}
