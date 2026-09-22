using Prisma.Workspace.Api.Services;

namespace Prisma.Workspace.Tests;

public class PortugueseIdentityErrorDescriberTests
{
    [Theory]
    [InlineData(true, "DuplicateUserName")]
    [InlineData(false, "DuplicateEmail")]
    public void DuplicateAccount_UsesPortugueseAndPreservesCode(bool userName, string expectedCode)
    {
        var describer = new PortugueseIdentityErrorDescriber();
        var error = userName
            ? describer.DuplicateUserName("existing@example.test")
            : describer.DuplicateEmail("existing@example.test");
        Assert.Equal(expectedCode, error.Code);
        Assert.Equal("Este e-mail já está cadastrado. Entre na sua conta.", error.Description);
    }

    [Fact]
    public void Password_errors_preserve_identity_codes_and_explain_rules_in_portuguese()
    {
        var describer = new PortugueseIdentityErrorDescriber();
        Assert.Equal("A senha deve conter pelo menos 10 caracteres.", describer.PasswordTooShort(10).Description);
        Assert.Equal("PasswordTooShort", describer.PasswordTooShort(10).Code);
        Assert.Contains("número", describer.PasswordRequiresDigit().Description);
        Assert.Contains("minúscula", describer.PasswordRequiresLower().Description);
        Assert.Contains("maiúscula", describer.PasswordRequiresUpper().Description);
        Assert.Contains("caractere especial", describer.PasswordRequiresNonAlphanumeric().Description);
        Assert.Contains("3 caracteres diferentes", describer.PasswordRequiresUniqueChars(3).Description);
    }
}
