namespace Prisma.Workspace.Application.Interfaces;

public sealed record InstallationSetupStatus(bool Initialized, bool SetupAvailable);

public sealed record InstallationSetupRequest(
    string AdministratorName,
    string AdministratorEmail,
    string AdministratorPassword,
    string OrganizationName,
    string OrganizationSlug);

public enum InstallationSetupOutcome
{
    Created,
    Unavailable,
    AlreadyCompleted,
    Conflict,
    ValidationFailed
}

public sealed record InstallationSetupResult(
    InstallationSetupOutcome Outcome,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public interface IInstallationSetupService
{
    Task<InstallationSetupStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<InstallationSetupResult> CompleteAsync(
        InstallationSetupRequest request,
        string? suppliedToken,
        CancellationToken cancellationToken = default);
}
