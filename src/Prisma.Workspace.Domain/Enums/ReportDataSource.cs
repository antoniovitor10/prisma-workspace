namespace Prisma.Workspace.Domain.Enums;

public enum ReportDataSource
{
    WorkItems = 1,
    ExternalRequests = 2,
    Projects = 3,
    Teams = 4,
    Users = 5,
    Sprints = 6,
    TimeEntries = 7,
    // 8 pertencia a Slas, removido pela D83.
}

public enum ReportVisualization
{
    Table = 1,
    Indicator = 2,
    Bar = 3,
    Column = 4,
    Line = 5,
    Pie = 6,
    Donut = 7
}

public enum ReportMetricOperation
{
    Count = 1,
    Sum = 2,
    Average = 3,
    Percentage = 4,
    Minimum = 5,
    Maximum = 6,
    PlannedVersusActual = 7
}
