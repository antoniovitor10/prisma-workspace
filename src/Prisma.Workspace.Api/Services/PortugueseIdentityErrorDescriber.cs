using Microsoft.AspNetCore.Identity;

namespace Prisma.Workspace.Api.Services;

/// <summary>Traduz a duplicidade de cadastro; o login usa o e-mail como nome de usuário.</summary>
public sealed class PortugueseIdentityErrorDescriber : IdentityErrorDescriber
{
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
