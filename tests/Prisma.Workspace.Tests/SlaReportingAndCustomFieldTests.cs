using System.Text.Json;
using Prisma.Workspace.Application.Features.ExternalPortal;
using Prisma.Workspace.Application.Features.Projects;
using Prisma.Workspace.Application.Features.Reports;
using Prisma.Workspace.Application.Features.Sla;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;

namespace Prisma.Workspace.Tests;

public class SlaReportingAndCustomFieldTests
{
    private static readonly SlaPolicySnapshotDto WeekdayPolicy = new(
        120, 480, "08:00", "18:00", 62, "UTC", true, true, 60,
        [], null, null);

    [Fact]
    public void SlaCalendar_SkipsWeekendsAndHolidays()
    {
        var friday = new DateTimeOffset(2026, 7, 17, 17, 30, 0, TimeSpan.Zero);

        var afterWeekend = SlaCalculator.AddBusinessMinutes(friday, 120, WeekdayPolicy);
        var withHoliday = SlaCalculator.AddBusinessMinutes(
            friday, 120, WeekdayPolicy with { Holidays = ["2026-07-20"] });

        Assert.Equal(new DateTimeOffset(2026, 7, 20, 9, 30, 0, TimeSpan.Zero), afterWeekend);
        Assert.Equal(new DateTimeOffset(2026, 7, 21, 9, 30, 0, TimeSpan.Zero), withHoliday);
    }

    [Fact]
    public void SlaPause_ExtendsDeadlinesOnlyByBusinessMinutes()
    {
        var request = new ExternalRequest
        {
            WorkItem = new WorkItem(),
            SlaPolicySnapshotJson = JsonSerializer.Serialize(WeekdayPolicy, SlaCalculator.JsonOptions),
            FirstResponseDueAt = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero),
            ResolutionDueAt = new DateTimeOffset(2026, 7, 20, 16, 0, 0, TimeSpan.Zero)
        };

        SlaCalculator.Pause(request, new DateTimeOffset(2026, 7, 17, 17, 30, 0, TimeSpan.Zero));
        SlaCalculator.Resume(request, new DateTimeOffset(2026, 7, 20, 9, 30, 0, TimeSpan.Zero));

        Assert.Equal(120, request.SlaPausedBusinessMinutes);
        Assert.Equal(new DateTimeOffset(2026, 7, 20, 14, 0, 0, TimeSpan.Zero), request.FirstResponseDueAt);
        Assert.Equal(new DateTimeOffset(2026, 7, 20, 18, 0, 0, TimeSpan.Zero), request.ResolutionDueAt);
        Assert.Null(request.SlaPausedAt);
    }

    [Fact]
    public void SlaProjection_DistinguishesNearDueOverduePausedAndMet()
    {
        var due = new DateTimeOffset(2026, 7, 20, 15, 0, 0, TimeSpan.Zero);
        var request = new ExternalRequest
        {
            WorkItem = new WorkItem(),
            SlaPolicySnapshotJson = JsonSerializer.Serialize(WeekdayPolicy, SlaCalculator.JsonOptions),
            FirstResponseDueAt = due,
            ResolutionDueAt = due.AddHours(4)
        };

        Assert.Equal(SlaStatus.NearDue,
            SlaCalculator.MapRequest(request, due.AddMinutes(-30)).FirstResponse.Status);
        Assert.Equal(SlaStatus.Overdue,
            SlaCalculator.MapRequest(request, due.AddMinutes(1)).FirstResponse.Status);

        request.SlaPausedAt = due.AddMinutes(-10);
        Assert.Equal(SlaStatus.Paused,
            SlaCalculator.MapRequest(request, due.AddHours(1)).FirstResponse.Status);

        request.SlaPausedAt = null;
        request.FirstRespondedAt = due.AddMinutes(-5);
        Assert.Equal(SlaStatus.Met,
            SlaCalculator.MapRequest(request, due.AddHours(1)).FirstResponse.Status);
    }

    [Fact]
    public void CustomFields_ValidateEveryExtendedDataKind()
    {
        var project = Project.Criar("CAMPOS", "Campos", "owner", WorkNature.Project, WorkType.Development);
        var teamId = Guid.NewGuid();
        project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = "member" });
        project.Teams.Add(new ProjectTeam { ProjectId = project.Id, TeamId = teamId });

        Validate(project, CustomFieldType.LongText, new string('a', 5000));
        Validate(project, CustomFieldType.Percentage, "99.5");
        Validate(project, CustomFieldType.DateTime, "2026-07-20T10:30:00-03:00");
        Validate(project, CustomFieldType.User, "member");
        Validate(project, CustomFieldType.Team, teamId.ToString());
        Validate(project, CustomFieldType.Url, "https://servicos.detran.se.gov.br/consulta");

        Assert.Throws<DomainException>(() => Validate(project, CustomFieldType.Percentage, "101"));
        Assert.Throws<DomainException>(() => Validate(project, CustomFieldType.Url, "javascript:alert(1)"));
        Assert.Throws<DomainException>(() => Validate(project, CustomFieldType.User, "outsider"));
    }

    [Fact]
    public void PublicProtocolContract_DoesNotExposeInternalOrPersonalData()
    {
        var properties = typeof(PublicExternalRequestDto).GetProperties()
            .Select(property => property.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("RequesterEmail", properties);
        Assert.DoesNotContain("RequesterPhone", properties);
        Assert.DoesNotContain("ResponsibleId", properties);
        Assert.DoesNotContain("TeamId", properties);
        Assert.DoesNotContain("TriageEvents", properties);
        Assert.DoesNotContain("SubmittedValues", properties);
        Assert.DoesNotContain("TimeEntries", properties);
        Assert.Contains("Messages", properties);
        Assert.Contains("Attachments", properties);
    }

    [Fact]
    public void AnalyticsContracts_ContainNoFinancialMetrics()
    {
        var reportProperties = typeof(TimeReportDto).GetProperties()
            .Concat(typeof(TimeReportSummaryDto).GetProperties())
            .Select(property => property.Name).ToList();
        var forbidden = new[] { "Cost", "Salary", "Billing", "Profit", "Rate", "ValuePerHour" };

        Assert.DoesNotContain(reportProperties,
            property => forbidden.Any(term => property.Contains(term, StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(8, Enum.GetValues<ReportDataSource>().Length);
        Assert.Equal(7, Enum.GetValues<ReportVisualization>().Length);
        Assert.Contains(ReportMetricOperation.PlannedVersusActual,
            Enum.GetValues<ReportMetricOperation>());
    }

    private static void Validate(Project project, CustomFieldType type, string value)
    {
        var field = ProjectCustomFieldDefinition.Create(
            project.Id, type.ToString(), type, false, null, 100);
        CustomFieldValueRules.Validate(field, value, project);
    }
}
