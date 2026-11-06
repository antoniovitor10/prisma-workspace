namespace Prisma.Workspace.Application.Interfaces;

public sealed record RegistrationError(string Code, string Description);
public sealed record UserRegistrationResult(string? UserId, string? ConfirmationToken, IReadOnlyList<RegistrationError> Errors)
{
    public bool Succeeded => UserId is not null && Errors.Count == 0;
    public override string ToString() => nameof(UserRegistrationResult);
}

public interface IUserRegistrationService
{
    Task<UserRegistrationResult> RegisterAsync(string fullName, string email, string password, CancellationToken ct);
}
