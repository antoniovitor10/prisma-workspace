using FluentValidation;
using MediatR;
using Prisma.Workspace.Application.Interfaces;

namespace Prisma.Workspace.Application.Features.Installation;

public sealed record GetInstallationStatusQuery : IRequest<InstallationSetupStatus>;

public sealed class GetInstallationStatusQueryHandler(IInstallationSetupService service)
    : IRequestHandler<GetInstallationStatusQuery, InstallationSetupStatus>
{
    public Task<InstallationSetupStatus> Handle(GetInstallationStatusQuery request, CancellationToken ct)
        => service.GetStatusAsync(ct);
}

public sealed record CompleteInstallationSetupCommand(
    string AdministratorName,
    string AdministratorEmail,
    string AdministratorPassword,
    string OrganizationName,
    string OrganizationSlug,
    string? SetupToken) : IRequest<InstallationSetupResult>;

public sealed class CompleteInstallationSetupCommandValidator
    : AbstractValidator<CompleteInstallationSetupCommand>
{
    public CompleteInstallationSetupCommandValidator()
    {
        RuleFor(x => x.AdministratorName).NotEmpty().MinimumLength(2).MaximumLength(200);
        RuleFor(x => x.AdministratorEmail).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.AdministratorPassword).NotEmpty().MinimumLength(10).MaximumLength(128);
        RuleFor(x => x.OrganizationName).NotEmpty().MinimumLength(2).MaximumLength(160);
        RuleFor(x => x.OrganizationSlug)
            .NotEmpty().MinimumLength(2).MaximumLength(80)
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("O identificador deve usar letras minúsculas, números e hífens.");
    }
}

public sealed class CompleteInstallationSetupCommandHandler(IInstallationSetupService service)
    : IRequestHandler<CompleteInstallationSetupCommand, InstallationSetupResult>
{
    public Task<InstallationSetupResult> Handle(CompleteInstallationSetupCommand request, CancellationToken ct)
        => service.CompleteAsync(new InstallationSetupRequest(
            request.AdministratorName, request.AdministratorEmail, request.AdministratorPassword,
            request.OrganizationName, request.OrganizationSlug), request.SetupToken, ct);
}
