using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Infrastructure.Persistence;

namespace Prisma.Workspace.Infrastructure.Services;

/// <summary>Nome provisório no Identity até a primeira associação; o nome funcional continua no membro.</summary>
public sealed class UserRegistrationService(AppDbContext db, UserManager<IdentityUser> users) : IUserRegistrationService
{
    public async Task<UserRegistrationResult> RegisterAsync(string fullName, string email, string password, CancellationToken ct)
        => await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            db.ChangeTracker.Clear();
            await using var transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct) : null;
            var user = new IdentityUser { UserName = email, Email = email };
            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded) return Failure(created);
            try
            {
                var named = await users.AddClaimAsync(user, new Claim(PendingRegistrationName.ClaimType, fullName));
                if (!named.Succeeded)
                {
                    if (transaction is null) await users.DeleteAsync(user);
                    return Failure(named);
                }
                var token = await users.GenerateEmailConfirmationTokenAsync(user);
                if (transaction is not null) await transaction.CommitAsync(ct);
                return new UserRegistrationResult(user.Id, token, []);
            }
            catch
            {
                if (transaction is null) await users.DeleteAsync(user);
                throw;
            }
        });

    private static UserRegistrationResult Failure(IdentityResult result)
        => new(null, null, result.Errors.Select(error => new RegistrationError(error.Code, error.Description)).ToList());
}
