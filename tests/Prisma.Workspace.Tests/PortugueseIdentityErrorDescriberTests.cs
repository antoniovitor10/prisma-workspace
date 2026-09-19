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
}
