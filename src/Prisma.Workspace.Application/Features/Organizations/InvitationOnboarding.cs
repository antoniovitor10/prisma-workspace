using MediatR;

namespace Prisma.Workspace.Application.Features.Organizations;

public record InvitationPreview(string Email, string OrganizationName, bool AccountExists);
public record InvitationCompletion(string UserId, Guid OrganizationId);
public record PreviewInvitationQuery(string Token) : IRequest<InvitationPreview>;
public record CompleteInvitationCommand(string Token, string Password, bool CreateAccount,
    string? FullName, string? ConfirmPassword) : IRequest<InvitationCompletion>;

public interface IInvitationOnboardingService
{
    Task<InvitationPreview> PreviewAsync(string token, CancellationToken ct);
    Task<InvitationCompletion> CompleteAsync(CompleteInvitationCommand request, CancellationToken ct);
}

public sealed class PreviewInvitationHandler(IInvitationOnboardingService service)
    : IRequestHandler<PreviewInvitationQuery, InvitationPreview>
{
    public Task<InvitationPreview> Handle(PreviewInvitationQuery request, CancellationToken ct)
        => service.PreviewAsync(request.Token, ct);
}

public sealed class CompleteInvitationHandler(IInvitationOnboardingService service)
    : IRequestHandler<CompleteInvitationCommand, InvitationCompletion>
{
    public Task<InvitationCompletion> Handle(CompleteInvitationCommand request, CancellationToken ct)
        => service.CompleteAsync(request, ct);
}
