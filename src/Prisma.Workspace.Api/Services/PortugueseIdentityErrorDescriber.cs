using Microsoft.AspNetCore.Identity;

namespace Prisma.Workspace.Api.Services;

/// <summary>Traduz a duplicidade de cadastro; o login usa o e-mail como nome de usuário.</summary>
public sealed class PortugueseIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError PasswordTooShort(int length) => new()
    {
        Code = nameof(PasswordTooShort),
        Description = $"A senha deve conter pelo menos {length} caracteres."
    };

    public override IdentityError PasswordRequiresDigit() => new()
    {
        Code = nameof(PasswordRequiresDigit), Description = "A senha deve conter pelo menos um número."
    };

    public override IdentityError PasswordRequiresLower() => new()
    {
        Code = nameof(PasswordRequiresLower), Description = "A senha deve conter pelo menos uma letra minúscula."
    };

    public override IdentityError PasswordRequiresUpper() => new()
    {
        Code = nameof(PasswordRequiresUpper), Description = "A senha deve conter pelo menos uma letra maiúscula."
    };

    public override IdentityError PasswordRequiresNonAlphanumeric() => new()
    {
        Code = nameof(PasswordRequiresNonAlphanumeric), Description = "A senha deve conter pelo menos um caractere especial."
    };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => new()
    {
        Code = nameof(PasswordRequiresUniqueChars), Description = $"A senha deve conter pelo menos {uniqueChars} caracteres diferentes."
    };

    public override IdentityError DuplicateUserName(string userName) => new()
    {
        Code = nameof(DuplicateUserName),
        Description = "Este e-mail já está cadastrado. Entre na sua conta."
    };

    public override IdentityError DuplicateEmail(string email) => new()
    {
        Code = nameof(DuplicateEmail),
        Description = "Este e-mail já está cadastrado. Entre na sua conta."
    };
}
