using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Infrastructure.Persistence;

namespace Prisma.Workspace.Infrastructure.Services;

public sealed class InstallationSetupService(
    AppDbContext context,
    UserManager<IdentityUser> userManager,
    IConfiguration configuration) : IInstallationSetupService
{
    public async Task<InstallationSetupStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var state = await context.InstallationStates.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == InstallationState.SingletonId, cancellationToken);
        if (state is null)
            return new InstallationSetupStatus(false, false);

        return new InstallationSetupStatus(
            state.IsInitialized,
            !state.IsInitialized && HasValidConfiguration());
    }

    public async Task<InstallationSetupResult> CompleteAsync(
        InstallationSetupRequest request,
        string? suppliedToken,
        CancellationToken cancellationToken = default)
    {
        if (!HasValidConfiguration() || !TokenMatches(suppliedToken))
            return new InstallationSetupResult(InstallationSetupOutcome.Unavailable);

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var state = context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory"
            ? await context.InstallationStates.SingleOrDefaultAsync(x => x.Id == InstallationState.SingletonId, cancellationToken)
            : await context.InstallationStates
                .FromSqlRaw(
                    "SELECT * FROM [InstallationStates] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {0}",
                    InstallationState.SingletonId)
                .SingleOrDefaultAsync(cancellationToken);

        if (state is null)
            return await RollbackAsync(InstallationSetupOutcome.Conflict, cancellationToken);
        if (state.IsInitialized)
            return await RollbackAsync(InstallationSetupOutcome.AlreadyCompleted, cancellationToken);

        var containsExistingInstallation = await context.Users.AnyAsync(cancellationToken)
            || await context.Organizations.IgnoreQueryFilters().AnyAsync(cancellationToken)
            || await context.OrganizationMembers.IgnoreQueryFilters().AnyAsync(cancellationToken);
        if (containsExistingInstallation)
            return await RollbackAsync(InstallationSetupOutcome.Conflict, cancellationToken);

        var email = request.AdministratorEmail.Trim().ToLowerInvariant();
        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        var identityResult = await userManager.CreateAsync(user, request.AdministratorPassword);
        if (!identityResult.Succeeded)
        {
            var errors = new Dictionary<string, string[]>
            {
                ["administratorPassword"] = identityResult.Errors
                    .Select(x => x.Description).Distinct().ToArray()
            };
            await transaction.RollbackAsync(cancellationToken);
            return new InstallationSetupResult(InstallationSetupOutcome.ValidationFailed, errors);
        }

        var organization = Organization.Create(
            request.OrganizationName.Trim(), request.OrganizationSlug.Trim(), user.Id);
        organization.Members.Single().UpdateDisplayName(request.AdministratorName);
        await context.Organizations.AddAsync(organization, cancellationToken);
        state.MarkInitialized(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new InstallationSetupResult(InstallationSetupOutcome.Created);

        async Task<InstallationSetupResult> RollbackAsync(
            InstallationSetupOutcome outcome,
            CancellationToken ct)
        {
            await transaction.RollbackAsync(ct);
            return new InstallationSetupResult(outcome);
        }
    }

    private bool HasValidConfiguration()
        => configuration.GetValue<bool>("Setup:Enabled")
            && TryDecodeBase64Url(configuration["Setup:Token"], out var bytes)
            && bytes.Length >= 32;

    private bool TokenMatches(string? suppliedToken)
    {
        var configured = configuration["Setup:Token"] ?? string.Empty;
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configured));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(suppliedToken ?? string.Empty));
        return CryptographicOperations.FixedTimeEquals(configuredHash, suppliedHash);
    }

    internal static bool TryDecodeBase64Url(string? value, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(value)
            || value.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_')))
            return false;

        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 += (base64.Length % 4) switch { 2 => "==", 3 => "=", _ => string.Empty };
        try
        {
            bytes = Convert.FromBase64String(base64);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
