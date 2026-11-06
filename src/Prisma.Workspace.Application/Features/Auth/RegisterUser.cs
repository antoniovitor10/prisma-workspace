using FluentValidation;
using MediatR;
using Prisma.Workspace.Application.Interfaces;

namespace Prisma.Workspace.Application.Features.Auth;

public sealed record RegisterUserCommand(string FullName, string Email, string Password) : IRequest<UserRegistrationResult>
{
    public override string ToString() => nameof(RegisterUserCommand);
}

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(request => request.FullName).Cascade(CascadeMode.Stop).NotEmpty().WithMessage("Informe seu nome e sobrenome.")
            .MaximumLength(200).WithMessage("O nome completo deve ter até 200 caracteres.")
            .Must(name => name is not null && name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length >= 2)
            .WithMessage("Informe seu nome e sobrenome.");
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(request => request.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class RegisterUserCommandHandler(IUserRegistrationService registrations)
    : IRequestHandler<RegisterUserCommand, UserRegistrationResult>
{
    public Task<UserRegistrationResult> Handle(RegisterUserCommand request, CancellationToken ct)
        => registrations.RegisterAsync(request.FullName.Trim(), request.Email.Trim(), request.Password, ct);
}
