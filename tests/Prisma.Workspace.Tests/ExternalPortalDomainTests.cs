using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using System.Text.Json;

namespace Prisma.Workspace.Tests;

public class ExternalPortalDomainTests
{
    [Fact]
    public void ExternalPortal_NormalizesSlug_AndAcceptsMultipleAccessModes()
    {
        var portal = ExternalPortal.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "  Portal-Servicos  ",
            true, true, ExternalPortalAccessMode.Invitation | ExternalPortalAccessMode.EmailCode);

        Assert.Equal("portal-servicos", portal.PublicSlug);
        Assert.True(portal.AccessModes.HasFlag(ExternalPortalAccessMode.Invitation));
        Assert.True(portal.RequiresAuthentication);
    }

    [Fact]
    public void ExternalPortal_RejectsAuthenticationWithoutAnAuthenticatedMode()
    {
        Assert.Throws<DomainException>(() => ExternalPortal.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "portal",
            true, true, ExternalPortalAccessMode.PublicLink));
    }

    [Fact]
    public void ExternalRequest_OnlyConfirmsCompletedWorkItem_AndValidatesRating()
    {
        var request = new ExternalRequest { WorkItem = new WorkItem() };

        Assert.Throws<DomainException>(request.ConfirmCompletion);
        Assert.Throws<DomainException>(() => request.Rate(0, null));

        request.WorkItem.CompletedAt = DateTimeOffset.UtcNow;
        request.ConfirmCompletion();
        request.Rate(5, "Resolvido");

        Assert.NotNull(request.CompletionConfirmedAt);
        Assert.Equal(5, request.Rating);
        Assert.Equal("Resolvido", request.RatingComment);
    }

    [Fact]
    public void ExternalForm_DefaultDefinition_ContainsConversionFields()
    {
        var form = ExternalForm.CreateDefault(Guid.NewGuid(), "Serviços Digitais");
        using var fields = JsonDocument.Parse(form.FieldsJson);
        var kinds = fields.RootElement.EnumerateArray()
            .Select(field => field.GetProperty("kind").GetInt32()).ToHashSet();

        Assert.True(form.IsDefault);
        Assert.True(form.IsEnabled);
        Assert.Contains((int)ExternalFormFieldKind.Subject, kinds);
        Assert.Contains((int)ExternalFormFieldKind.DetailedDescription, kinds);
        Assert.Contains((int)ExternalFormFieldKind.RequesterEmail, kinds);
        Assert.Contains((int)ExternalFormFieldKind.Attachments, kinds);
    }

    [Fact]
    public void ExternalForm_ValidatesFileAndSpamLimits()
    {
        var form = ExternalForm.CreateDefault(Guid.NewGuid(), "Atendimento");

        Assert.Throws<DomainException>(() => form.Configure(
            "atendimento", "Atendimento", null, null, null, true, true,
            Priority.Medium, null, null, null, 21, 10_000_000,
            ".pdf", "application/pdf", 2, "[]", "[]"));
        Assert.Throws<DomainException>(() => form.Configure(
            "atendimento", "Atendimento", null, null, null, true, true,
            Priority.Medium, null, null, null, 1, 10_000_000,
            ".pdf", "application/pdf", 61, "[]", "[]"));
    }

    [Fact]
    public void ExternalRequest_StartsInNewTriageState()
    {
        var request = new ExternalRequest();
        Assert.Equal(ExternalRequestTriageStatus.New, request.TriageStatus);
    }
}
