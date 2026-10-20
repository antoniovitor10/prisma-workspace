using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Services;

namespace Prisma.Workspace.Tests;

public class PublicRegistrationSqlTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IdentityAndPendingFullNameAreCommittedTogether(bool refuseClaim)
    {
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException("Configure o SQL E2E exclusivo.");
        var connection = new SqlConnectionStringBuilder(configured);
        Assert.StartsWith("Prisma_BoardColumns_", connection.InitialCatalog);
        connection.InitialCatalog = "Prisma_BoardColumns_Oficina_Registration_" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var db = new AppDbContext(options);
        try
        {
            await db.Database.EnsureCreatedAsync();
            using var manager = new PublicRegistrationTests.RegistrationUserManager(db, refuseClaim);
            var result = await new UserRegistrationService(db, manager).RegisterAsync("Ana Alves", "qa@example.test",
                "Aa1!" + Guid.NewGuid().ToString("N"), default);
            db.ChangeTracker.Clear();
            Assert.Equal(!refuseClaim, result.Succeeded);
            Assert.Equal(refuseClaim ? 0 : 1, await db.Users.CountAsync());
            Assert.Equal(refuseClaim ? 0 : 1, await db.UserClaims.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
