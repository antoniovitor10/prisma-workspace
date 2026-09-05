export interface TimeReportSummary {
  key: string;
  name: string;
  plannedHours: number;
  realizedHours: number;
  varianceHours: number;
  automaticHours: number;
  manualHours: number;
  businessHours: number;
}

export interface TimeReportEntry {
  id: string;
  workItemId: string;
  workItemNumber: number;
  reference: string;
  workItemTitle: string;
  userId: string;
  userName: string;
  teamId?: string;
  teamName: string;
  startedAt: string;
  endedAt?: string;
  durationSeconds: number;
  note?: string;
  isManual: boolean;
}

export interface TimeReport {
  projectId: string;
  projectKey: string;
  projectName: string;
  from: string;
  to: string;
  project: TimeReportSummary;
  byUser: TimeReportSummary[];
  byTeam: TimeReportSummary[];
  entries: TimeReportEntry[];
}

export interface HoursProjectLine {
  projectKey: string;
  projectName: string;
  automaticHours: number;
  manualHours: number;
  totalHours: number;
}

export interface HoursResponsible {
  userId: string;
  name: string;
  businessHours: number;
  automaticHours: number;
  manualHours: number;
  totalHours: number;
  projects: HoursProjectLine[];
}

export interface OrganizationHoursReport {
  from: string;
  to: string;
  businessHours: number;
  automaticHours: number;
  manualHours: number;
  totalHours: number;
  byResponsible: HoursResponsible[];
}

export interface CustomFieldReportDefinition {
  id: string;
  name: string;
  type: number;
  isRequired: boolean;
  optionsJson?: string;
}

export interface CustomFieldReportRow {
  workItemId: string;
  workItemNumber: number;
  reference: string;
  title: string;
  status: string;
  values: Record<string, string | undefined>;
}

export interface CustomFieldReport {
  projectId: string;
  projectKey: string;
  fields: CustomFieldReportDefinition[];
  rows: CustomFieldReportRow[];
}

export interface AnalyticsFilters {
  projectId?: string;
  teamId?: string;
  userId?: string;
  status?: string;
  priority?: number;
  from?: string;
  to?: string;
}

export interface ReportBreakdown {
  key: string;
  label: string;
  value: number;
}

export interface ReportPeriodPoint {
  period: string;
  value: number;
  secondaryValue?: number;
}

export interface PreparedReports {
  from: string;
  to: string;
  tasks: { open: number; completed: number; overdue: number; blocked: number; total: number };
  tasksByResponsible: ReportBreakdown[];
  tasksByTeam: ReportBreakdown[];
  tasksByProject: ReportBreakdown[];
  tasksByStatus: ReportBreakdown[];
  tasksByPriority: ReportBreakdown[];
  tasksByOrigin: ReportBreakdown[];
  externalRequests: {
    total: number;
    byCategory: ReportBreakdown[];
    byRequester: ReportBreakdown[];
  };
  sla: {
    applicable: number;
    met: number;
    overdue: number;
    paused: number;
    compliancePercentage: number;
    averageFirstResponseMinutes?: number;
    averageResolutionMinutes?: number;
  };
  hours: { planned: number; realized: number; variance: number };
  sprintVelocity: Array<{
    id: string;
    name: string;
    status: string;
    plannedPoints: number;
    completedPoints: number;
    velocity: number;
    progressPercentage: number;
  }>;
  burndown: ReportPeriodPoint[];
  workloadByPeriod: ReportPeriodPoint[];
}

export interface Dashboard {
  kind: 'collaborator' | 'manager' | 'project';
  from: string;
  to: string;
  kpis: Array<{ key: string; label: string; value: number; unit?: string; tone?: string }>;
  series: Array<{ key: string; title: string; visualization: string; points: ReportPeriodPoint[] }>;
  items: Array<{
    id: string;
    number: number;
    title: string;
    status?: string;
    dueDate?: string;
    projectKey?: string;
  }>;
}

export interface ReportField {
  key: string;
  label: string;
  type: string;
  numeric: boolean;
}

export interface ReportSourceCatalog {
  source: number;
  name: string;
  fields: ReportField[];
}

export interface ReportCatalog {
  sources: ReportSourceCatalog[];
  filterOperators: string[];
  metrics: number[];
  visualizations: number[];
}

export interface ReportFilter {
  field: string;
  operator: string;
  value?: string;
  valueTo?: string;
}

export interface ReportMetric {
  operation: number;
  field?: string;
  label: string;
}

export interface ReportDefinition {
  columns: string[];
  metrics: ReportMetric[];
  filters: ReportFilter[];
  groupBy?: string;
  orderBy?: string;
  descending: boolean;
  from?: string;
  to?: string;
}

export interface SavedReport {
  id: string;
  projectId?: string;
  ownerId: string;
  name: string;
  source: number;
  visualization: number;
  definition: ReportDefinition;
  isShared: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface ReportExecution {
  columns: ReportField[];
  rows: Array<Record<string, unknown>>;
  sourceRowCount: number;
  resultRowCount: number;
  visualization: number;
}
