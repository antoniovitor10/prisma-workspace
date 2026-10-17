using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Tests;

public class MethodContractTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJodW1hbiJ9.signature")]
    [InlineData("Bearer pmt_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void MethodCredential_DoesNotAcceptLoginJwtOrTransportHeader(string? value)
        => Assert.False(MethodTokenFormat.IsWellFormed(value));

    [Fact]
    public void MethodCredential_RequiresExactAsciiFormat()
    {
        var body = new string('a', 43);
        Assert.True(MethodTokenFormat.IsWellFormed("pmt_" + body));
        Assert.False(MethodTokenFormat.IsWellFormed("PMT_" + body));
        Assert.False(MethodTokenFormat.IsWellFormed("pmt_" + body + " "));
        Assert.False(MethodTokenFormat.IsWellFormed("pmt_" + body[..42]));
        Assert.False(MethodTokenFormat.IsWellFormed("pmt_" + new string('á', 43)));
    }

    [Theory]
    [InlineData(MethodAuthenticationKind.MethodToken, MethodRole.Human)]
    [InlineData(MethodAuthenticationKind.MethodToken, MethodRole.Agent)]
    [InlineData(MethodAuthenticationKind.IntegrationWorker, MethodRole.System)]
    [InlineData(MethodAuthenticationKind.Unknown, MethodRole.Human)]
    public void RoleAlone_DoesNotCreateInteractiveHumanAuthority(MethodAuthenticationKind kind, MethodRole role)
    {
        var actor = new MethodActorIdentity(kind, role, Guid.NewGuid(), "actor", "Nome", null, null,
            null, null, new HashSet<PlatformPermission> { PlatformPermission.ChangeStatus }, "mcp", "test");
        Assert.False(actor.IsInteractiveHuman);
    }
}
