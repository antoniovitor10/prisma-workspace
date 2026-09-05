using Prisma.Workspace.Domain.Authorization;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Application.Features.Organizations;

namespace Prisma.Workspace.Tests;

public class OrganizationDomainTests
{
    [Fact]
    public void CreateOrganization_AddsActiveAdministrator()
    {
        var organization = Organization.Create("Acme", "acme", "user-1");

        var member = Assert.Single(organization.Members);
        Assert.Equal(OrganizationRole.Administrator, member.Role);
        Assert.True(member.IsActive);
        Assert.Equal(organization.Id, member.OrganizationId);
    }

    [Fact]
    public void ExternalRequester_IsRestrictedToRequestAndFormScopes()
    {
        Assert.True(RolePermissionCatalog.Allows(
            OrganizationRole.ExternalRequester, PlatformPermission.Create, PermissionScope.Request));
        Assert.False(RolePermissionCatalog.Allows(
            OrganizationRole.ExternalRequester, PlatformPermission.View, PermissionScope.Project));
    }

    [Fact]
    public void TeamLeader_MustBeATeamMember()
    {
        var team = Team.Criar("Produto");

        Assert.Throws<DomainException>(() => team.Editar("Produto", "not-a-member", 40));
        team.AdicionarMembro("leader", 32);
        team.Editar("Produto", "leader", 36);

        Assert.Equal("leader", team.LeaderId);
        Assert.Equal(36, team.DefaultWeeklyCapacityHours);
    }

    [Fact]
    public void OrganizationMember_DisplayName_IsTrimmedAndCanBeCleared()
    {
        var member = OrganizationMember.Create(Guid.NewGuid(), "user-1", OrganizationRole.TeamMember);

        member.UpdateDisplayName("  Maria da Silva  ");
        Assert.Equal("Maria da Silva", member.DisplayName);

        member.UpdateDisplayName(null);
        Assert.Null(member.DisplayName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("A")]
    public void OrganizationMember_DisplayName_RejectsInvalidLength(string displayName)
    {
        var member = OrganizationMember.Create(Guid.NewGuid(), "user-1", OrganizationRole.TeamMember);

        Assert.Throws<DomainException>(() => member.UpdateDisplayName(displayName));
    }

    [Fact]
    public void OrganizationMember_DisplayName_RejectsMoreThanTwoHundredCharacters()
    {
        var member = OrganizationMember.Create(Guid.NewGuid(), "user-1", OrganizationRole.TeamMember);

        Assert.Throws<DomainException>(() => member.UpdateDisplayName(new string('A', 201)));
    }

    [Fact]
    public void UpdateOrganizationMember_ValidatesDisplayNameBeforeTheHandler()
    {
        var validator = new UpdateOrganizationMemberCommandValidator();
        var invalid = validator.Validate(new UpdateOrganizationMemberCommand(
            "user-1", OrganizationRole.TeamMember, true, "A", "admin"));
        var validClear = validator.Validate(new UpdateOrganizationMemberCommand(
            "user-1", OrganizationRole.TeamMember, true, null, "admin"));

        Assert.False(invalid.IsValid);
        Assert.True(validClear.IsValid);
    }
}
