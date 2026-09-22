using System.Text.Json;
using Prisma.Workspace.Application.Features.ExternalPortal;
using Prisma.Workspace.Application.Features.Projects;
using Prisma.Workspace.Application.Features.Reports;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;

namespace Prisma.Workspace.Tests;

public class ReportingAndCustomFieldTests
{

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
        // 7 e nao 8: a fonte de dados Slas saiu com a remocao do SLA (D83).
        Assert.Equal(7, Enum.GetValues<ReportDataSource>().Length);
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
