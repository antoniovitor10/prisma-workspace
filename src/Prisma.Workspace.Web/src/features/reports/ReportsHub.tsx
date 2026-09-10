import { useMemo, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { FileDown, Printer } from 'lucide-react';
import styled from 'styled-components';
import { api } from '../../services/api';
import type { ProjectSummary } from '../../pages/Projects';
import type {
  CustomFieldReport,
  Dashboard,
  OrganizationHoursReport,
  PreparedReports,
} from '../../types/reports';
import { DashboardView } from './DashboardView';
import { ReportBuilder } from './ReportBuilder';
import { AreaTrend, BarsChart, DonutChart, GroupedBars } from './ReportCharts';
import { exportNodeToPdf } from './reportExport';
import { exportHoursReportPdf } from './hoursPdf';
import { HoursReport } from './HoursReport';
import { useOrganization } from '../organizations/OrganizationState';

const ExportBar = styled.div`
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 9px;
  margin-bottom: 12px;
  padding: 10px 13px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.card};
  background: ${({ theme }) => theme.color.surface};
  p { margin: 0; color: ${({ theme }) => theme.color.textMuted}; font-size: 13px; }
  p b { color: ${({ theme }) => theme.color.text}; font-weight: 750; }
  .spacer { margin-left: auto; }
`;
const ExportButton = styled.button<{ $primary?: boolean }>`
  display: inline-flex;
  min-height: 34px;
  align-items: center;
  gap: 6px;
  padding: 0 12px;
  border: 1px solid ${({ theme, $primary }) => $primary ? theme.color.brand : theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme, $primary }) => $primary ? theme.color.brand : theme.color.surface};
  color: ${({ theme, $primary }) => $primary ? theme.color.onBrand : theme.color.text};
  font-size: 13px;
  font-weight: 750;
  &:disabled { opacity: .55; }
`;

const Filters = styled.div`
  display: flex;
  flex-wrap: wrap;
  align-items: end;
  gap: 8px;
  margin-bottom: 12px;
  padding: 13px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.card};
  background: ${({ theme }) => theme.color.surface};
  label { display: grid; gap: 4px; color: ${({ theme }) => theme.color.textMuted}; font-size: 12px; font-weight: 750; }
  input, select { min-height: 38px; padding: 0 10px; border: 1px solid ${({ theme }) => theme.color.border}; border-radius: ${({ theme }) => theme.radius.md}; background: ${({ theme }) => theme.color.surfaceSubtle}; color: ${({ theme }) => theme.color.text}; font-size: 13px; }
`;

const Tabs = styled.div`
  display: flex;
  gap: 4px;
  margin-bottom: 14px;
  padding: 5px;
  overflow-x: auto;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.card};
  background: ${({ theme }) => theme.color.surface};
  box-shadow: ${({ theme }) => theme.shadow.sm};
`;
const Tab = styled.button<{ $active: boolean }>`
  min-height: 36px;
  padding: 0 13px;
  border: 0;
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme, $active }) => $active ? `color-mix(in srgb, ${theme.color.brand} 10%, ${theme.color.surface})` : 'transparent'};
  color: ${({ theme, $active }) => $active ? theme.color.brand : theme.color.textMuted};
  font-size: 13px;
  font-weight: 750;
  white-space: nowrap;
`;

const Kpis = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(145px, 1fr));
  gap: 9px;
`;
const Kpi = styled.article<{ $danger?: boolean }>`
  position: relative;
  overflow: hidden;
  padding: 16px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.card};
  background: ${({ theme }) => theme.color.surface};
  box-shadow: ${({ theme }) => theme.shadow.sm};
  &::after { content:''; position:absolute; inset:auto 0 0; height:3px; background:${({theme,$danger})=>$danger?theme.color.danger:theme.color.gradient}; opacity:.72; }
  span { color: ${({ theme }) => theme.color.textMuted}; font-size: 11px; font-weight:700; letter-spacing:.03em; text-transform:uppercase; }
  strong { display: block; margin-top: 8px; color: ${({ theme, $danger }) => $danger ? theme.color.danger : theme.color.text}; font-size: 24px; letter-spacing:-.04em; }
`;

const Grid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(270px, 1fr));
  gap: 10px;
  margin-top: 11px;
`;
const Card = styled.article`
  min-width: 0;
  padding: 14px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.card};
  background: ${({ theme }) => theme.color.surface};
  box-shadow: ${({ theme }) => theme.shadow.sm};
  h3 { margin-bottom: 11px; font-size: 14px; letter-spacing:-.01em; }
`;
const TableWrap = styled.div`
  overflow: auto;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};
  table { width: 100%; border-collapse: collapse; font-size: 12px; }
  th, td { min-width: 95px; padding: 9px; border-bottom: 1px solid ${({ theme }) => theme.color.border}; text-align: left; }
  th { background: ${({ theme }) => theme.color.neutral[50]}; color: ${({ theme }) => theme.color.textMuted}; }
`;

const Empty = styled.div`
  padding: 40px;
  border: 1px dashed ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  color: ${({ theme }) => theme.color.textMuted};
  text-align: center;
  font-size: 13.5px;
`;

type TabKey = 'prepared' | 'builder' | 'hours' | 'custom';

function PreparedView({ report, dashboard, showPoints }: { report?: PreparedReports; dashboard?: Dashboard; showPoints: boolean }) {
  if (!report) return <Empty>Carregando indicadores...</Empty>;
  return <>
    {dashboard && <><DashboardView dashboard={dashboard} /><div style={{ height: 14 }} /></>}
    <Kpis>
      <Kpi><span>Tarefas abertas</span><strong>{report.tasks.open}</strong></Kpi>
      <Kpi><span>Concluídas no período</span><strong>{report.tasks.completed}</strong></Kpi>
      <Kpi $danger><span>Tarefas atrasadas</span><strong>{report.tasks.overdue}</strong></Kpi>
      <Kpi><span>Tarefas bloqueadas</span><strong>{report.tasks.blocked}</strong></Kpi>
      <Kpi><span>Solicitações externas</span><strong>{report.externalRequests.total}</strong></Kpi>
      <Kpi><span>Horas previstas</span><strong>{report.hours.planned.toLocaleString('pt-BR')}h</strong></Kpi>
      <Kpi><span>Horas realizadas</span><strong>{report.hours.realized.toLocaleString('pt-BR')}h</strong></Kpi>
    </Kpis>
    <Grid>
      <Card><h3>Tarefas por status</h3><DonutChart data={report.tasksByStatus} /></Card>
      <Card><h3>Tarefas por prioridade</h3><DonutChart data={report.tasksByPriority} /></Card>
      <Card><h3>Tarefas por origem</h3><DonutChart data={report.tasksByOrigin} /></Card>
      <Card><h3>Tarefas por responsável</h3><BarsChart data={report.tasksByResponsible} color="#06B6D4" /></Card>
      <Card><h3>Tarefas por equipe</h3><BarsChart data={report.tasksByTeam} color="#2563EB" /></Card>
      <Card><h3>Tarefas por projeto</h3><BarsChart data={report.tasksByProject} color="#7A5CC6" /></Card>
      <Card><h3>Solicitações por categoria</h3><BarsChart data={report.externalRequests.byCategory} color="#E8833A" /></Card>
      <Card><h3>Solicitações por solicitante</h3><BarsChart data={report.externalRequests.byRequester} color="#00A9A5" /></Card>
    </Grid>
    <Grid>
      {showPoints&&<Card><h3>Velocidade das sprints</h3><BarsChart data={report.sprintVelocity.map(sprint => ({ key: sprint.id, label: sprint.name, value: sprint.velocity }))} color="#10B981" /></Card>}
      <Card><h3>Previsto versus realizado (horas)</h3><GroupedBars
        data={[{ label: 'Horas', planned: report.hours.planned, realized: report.hours.realized }]}
        keys={[{ key: 'planned', label: 'Previsto', color: '#06B6D4' }, { key: 'realized', label: 'Realizado', color: '#10B981' }]}
      /></Card>
      <Card><h3>Volume de trabalho por período</h3><AreaTrend points={report.workloadByPeriod} color="#2563EB" label="Tarefas" /></Card>
      <Card><h3>Burndown da sprint atual</h3><AreaTrend points={report.burndown} color="#D92D20" label="Restante" /></Card>
    </Grid>
  </>;
}


function CustomFieldsView({ report }: { report?: CustomFieldReport }) {
  if (!report) return <Empty>Selecione um projeto para analisar campos personalizados.</Empty>;
  if (!report.fields.length) return <Empty>O projeto ainda não possui campos personalizados ativos.</Empty>;
  return <TableWrap><table><thead><tr><th>Tarefa</th><th>Status</th>{report.fields.map(field => <th key={field.id}>{field.name}</th>)}</tr></thead><tbody>{report.rows.map(row => <tr key={row.workItemId}><td>{row.reference} · {row.title}</td><td>{row.status}</td>{report.fields.map(field => <td key={field.id}>{row.values[field.id] || '—'}</td>)}</tr>)}</tbody></table></TableWrap>;
}

export function ReportsHub({ fixedProjectId }: { fixedProjectId?: string }) {
  const [tab, setTab] = useState<TabKey>('prepared');
  const [selectedProjectId, setSelectedProjectId] = useState(fixedProjectId ?? '');
  const [from, setFrom] = useState(() => {
    const date = new Date(); date.setDate(date.getDate() - 29); return date.toISOString().slice(0, 10);
  });
  const [to, setTo] = useState(() => new Date().toISOString().slice(0, 10));
  const [priority, setPriority] = useState<number | ''>('');
  /**
   * Relatório por pessoa, pedido do PO: o gestor escolhe alguém e todo o relatório passa a
   * falar daquela pessoa. O backend já filtrava por `userId` — considera responsável,
   * participante e, nas horas, quem lançou — mas a tela nunca ofereceu a escolha.
   */
  const [userId, setUserId] = useState('');
  const projectId = fixedProjectId ?? (selectedProjectId || undefined);
  const peopleQuery = useQuery<Array<{ userId: string; name: string; isActive: boolean }>>({
    queryKey: ['organization', 'members'], queryFn: () => api.getOrganizationMembers(),
  });
  const projectsQuery = useQuery<ProjectSummary[]>({
    queryKey: ['projects', false], queryFn: () => api.getProjects(false), enabled: !fixedProjectId,
  });
  const fixedProjectQuery = useQuery<ProjectSummary>({
    queryKey: ['project', fixedProjectId],
    queryFn: () => api.getProject(fixedProjectId!),
    enabled: Boolean(fixedProjectId),
  });
  const selectedProject = fixedProjectId
    ? fixedProjectQuery.data
    : projectsQuery.data?.find(item => item.id === projectId);
  const showPoints = projectId ? Boolean(selectedProject) && selectedProject?.methodology !== 1 : true;
  const preparedQuery = useQuery<PreparedReports>({
    queryKey: ['prepared-reports', projectId, priority, from, to, userId],
    queryFn: () => api.getPreparedReports({
      projectId, priority: priority === '' ? undefined : priority, from, to,
      userId: userId || undefined,
    }),
  });
  const dashboardQuery = useQuery<Dashboard>({
    queryKey: ['project-dashboard', projectId, priority, from, to],
    queryFn: () => api.getProjectDashboard(projectId!, { priority: priority === '' ? undefined : priority, from, to }),
    enabled: !!projectId && tab === 'prepared',
  });
  const hoursQuery = useQuery<OrganizationHoursReport>({
    queryKey: ['org-hours-report', from, to, userId],
    queryFn: () => api.getOrganizationHoursReport({ from, to, userId: userId || undefined }),
    enabled: tab === 'hours',
  });
  const customQuery = useQuery<CustomFieldReport>({
    queryKey: ['project-custom-report', projectId],
    queryFn: () => api.getProjectCustomFieldReport(projectId!),
    enabled: !!projectId && tab === 'custom',
  });
  const error = preparedQuery.error ?? dashboardQuery.error ?? hoursQuery.error ?? customQuery.error;
  const tabs = useMemo<Array<[TabKey, string]>>(() => [
    ['prepared', 'Relatórios prontos'], ['builder', 'Construtor'], ['hours', 'Apontamento de horas'], ['custom', 'Campos personalizados'],
  ], []);

  const { current: currentOrg } = useOrganization();
  const reportRef = useRef<HTMLDivElement>(null);
  const [exporting, setExporting] = useState(false);
  const fmtDate = (iso: string) => { const [y, m, d] = iso.split('-'); return `${d}/${m}/${y}`; };
  const projectLabel = projectId
    ? (projectsQuery.data?.find(item => item.id === projectId)?.name ?? 'Projeto selecionado')
    : 'Todos os projetos';
  const priorityLabel = priority === '' ? 'Todas as prioridades' : ['Baixa', 'Média', 'Alta', 'Crítica'][priority as number];
  const personLabel = userId
    ? (peopleQuery.data?.find(person => person.userId === userId)?.name ?? 'Pessoa selecionada')
    : 'Todas as pessoas';
  // A pessoa entra no resumo porque ele vai no cabeçalho do PDF: um relatório de uma pessoa
  // que não diz de quem é serve de pouco depois de impresso.
  const filterSummary = `${projectLabel} · ${personLabel} · ${priorityLabel} · ${fmtDate(from)} até ${fmtDate(to)}`;
  const handlePdf = async () => {
    setExporting(true);
    try {
      if (tab === 'hours' && hoursQuery.data) {
        // Relatório de horas: PDF limpo e tabular por responsável (não captura de tela).
        await exportHoursReportPdf(hoursQuery.data, currentOrg.name, filterSummary);
      } else if (reportRef.current) {
        await exportNodeToPdf(reportRef.current, `relatorio-${tab}-${to}`, {
          organization: currentOrg.name,
          title: tabs.find(([key]) => key === tab)?.[1] ?? 'Relatório',
          subtitle: filterSummary,
        });
      }
    }
    catch { /* falha de exportacao nao deve quebrar a tela */ }
    finally { setExporting(false); }
  };

  return <>
    <Filters>
      {!fixedProjectId && <label>Projeto<select value={selectedProjectId} onChange={event => setSelectedProjectId(event.target.value)}><option value="">Todos os projetos</option>{projectsQuery.data?.map(project => <option key={project.id} value={project.id}>{project.key} · {project.name}</option>)}</select></label>}
      <label>Prioridade<select value={priority} onChange={event => setPriority(event.target.value === '' ? '' : Number(event.target.value))}><option value="">Todas</option><option value={0}>Baixa</option><option value={1}>Média</option><option value={2}>Alta</option><option value={3}>Crítica</option></select></label>
      <label>Pessoa<select aria-label="Pessoa" value={userId} onChange={event => setUserId(event.target.value)}><option value="">Todas as pessoas</option>{(peopleQuery.data ?? []).filter(person => person.isActive).map(person => <option key={person.userId} value={person.userId}>{person.name}</option>)}</select></label>
      <label>Período inicial<input type="date" value={from} onChange={event => setFrom(event.target.value)} /></label>
      <label>Período final<input type="date" min={from} value={to} onChange={event => setTo(event.target.value)} /></label>
    </Filters>
    <Tabs>{tabs.map(([key, label]) => <Tab key={key} $active={tab === key} onClick={() => setTab(key)}>{label}</Tab>)}</Tabs>
    {error && <Empty role="alert">{(error as Error).message}</Empty>}
    {!error && <ExportBar>
      <p>Filtro aplicado: <b>{filterSummary}</b></p>
      <div className="spacer" />
      <ExportButton type="button" onClick={() => window.print()}><Printer size={13} />Imprimir</ExportButton>
      <ExportButton type="button" $primary onClick={handlePdf} disabled={exporting}><FileDown size={13} />{exporting ? 'Gerando PDF...' : 'Baixar PDF'}</ExportButton>
    </ExportBar>}
    <div ref={reportRef} data-print-root>
      {!error && tab === 'prepared' && <PreparedView report={preparedQuery.data} dashboard={dashboardQuery.data} showPoints={showPoints} />}
      {tab === 'builder' && <ReportBuilder key={projectId ?? 'organization'} projectId={projectId} />}
      {!error && tab === 'hours' && <HoursReport report={hoursQuery.data} orgName={currentOrg.name} filterSummary={filterSummary} />}
      {tab === 'custom' && <CustomFieldsView report={customQuery.data} />}
    </div>
  </>;
}
