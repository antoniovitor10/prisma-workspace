using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Features.Organizations;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Repositories;

namespace Prisma.Workspace.Tests;

public class OrganizationCreationSqlTests
{
    [Theory]
    [InlineData(false, true, 1)]
    [InlineData(true, true, 2)]
    [InlineData(false, false, 2)]
    public async Task ConcurrentCreation_RespectsAuthorityInDatabase(bool platformAdministrator, bool sameUser, int expectedCreated)
    {
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException("Configure o banco SQL E2E exclusivo.");
        var connection = new SqlConnectionStringBuilder(configured);
        Assert.StartsWith("Prisma_BoardColumns_", connection.InitialCatalog);
        connection.InitialCatalog = "Prisma_BoardColumns_OrgCreation_" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var setup = new AppDbContext(options);
        try
        {
            await setup.Database.EnsureCreatedAsync();
            for (var round = 0; round < 3; round++)
            {
                var user = new IdentityUser { Id = Guid.NewGuid().ToString(), UserName = "organization-race-" + round };
                setup.Users.Add(user);
                setup.Entry(user).Property("IsPlatformAdministrator").CurrentValue = platformAdministrator;
                var other = sameUser ? user : new IdentityUser { Id = Guid.NewGuid().ToString(), UserName = "other-race-" + round };
                if (!sameUser) setup.Users.Add(other);
                await setup.SaveChangesAsync();
                var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var arrived = 0;
                async Task<bool> CreateAsync(string suffix, string userId)
                {
                    await using var db = new AppDbContext(options);
                    if (Interlocked.Increment(ref arrived) == 2) start.SetResult();
                    await start.Task;
                    try
                    {
                        await new CreateOrganizationCommandHandler(new OrganizationRepository(db)).Handle(
                            new CreateOrganizationCommand("Concorrente " + suffix + round, null, userId), default);
                        return true;
                    }
                    catch (AcessoNegadoException) { return false; }
                }

                var results = await Task.WhenAll(CreateAsync("A", user.Id), CreateAsync("B", other.Id));
                Assert.Equal(expectedCreated, results.Count(created => created));
                Assert.Equal(sameUser ? expectedCreated : 1, await setup.OrganizationMembers.IgnoreQueryFilters()
                    .CountAsync(member => member.UserId == user.Id));
                if (!sameUser) Assert.Equal(1, await setup.OrganizationMembers.IgnoreQueryFilters()
                    .CountAsync(member => member.UserId == other.Id));
            }
        }
        finally
        {
            // Nome aleatório atribuído acima: nunca apaga o banco configurado nem um banco existente.
            await setup.Database.EnsureDeletedAsync();
        }
    }
}
