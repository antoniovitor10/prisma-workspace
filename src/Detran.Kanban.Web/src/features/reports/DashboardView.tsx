import styled from 'styled-components';
import type { Dashboard, ReportPeriodPoint } from '../../types/reports';

const KpiGrid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(145px, 1fr));
  gap: 10px;
`;

const Kpi = styled.article<{ $tone?: string }>`
  padding: 14px;
  border: 1px solid ${({ theme, $tone }) => $tone === 'danger'
    ? `color-mix(in srgb, ${theme.color.danger} 35%, ${theme.color.border})`
    : theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};
  box-shadow: ${({ theme }) => theme.shadow.sm};
  span { color: ${({ theme }) => theme.color.textMuted}; font-size: 13px; }
  strong { display: block; margin-top: 7px; color: ${({ theme, $tone }) => $tone === 'danger' ? theme.color.danger : theme.color.text}; font-size: 23px; }
`;

const ChartGrid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(290px, 1fr));
  gap: 11px;
  margin-top: 12px;
`;

const ChartCard = styled.article`
  min-width: 0;
  padding: 15px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};
  box-shadow: ${({ theme }) => theme.shadow.sm};
  h3 { margin-bottom: 13px; font-size: 14px; }
`;

const Bars = styled.div`display: grid; gap: 8px;`;
const BarRow = styled.div`
  display: grid;
  grid-template-columns: minmax(74px, 110px) 1fr auto;
  align-items: center;
  gap: 8px;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 12px;
  span:first-child { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  b { color: ${({ theme }) => theme.color.text}; }
`;
const Track = styled.div`
  height: 7px;
  overflow: hidden;
  border-radius: 99px;
  background: ${({ theme }) => theme.color.neutral[100]};
  i { display: block; height: 100%; border-radius: inherit; background: ${({ theme }) => theme.color.accentBlue}; }
`;

const Line = styled.svg`
  width: 100%;
  height: 150px;
  overflow: visible;
  polyline { fill: none; stroke: ${({ theme }) => theme.color.accentBlue}; stroke-width: 2.5; vector-effect: non-scaling-stroke; }
  .secondary { stroke: ${({ theme }) => theme.color.neutral[400]}; stroke-dasharray: 4 4; }
  circle { fill: ${({ theme }) => theme.color.surface}; stroke: ${({ theme }) => theme.color.accentBlue}; stroke-width: 2; vector-effect: non-scaling-stroke; }
`;

const Axis = styled.div`
  display: flex;
  justify-content: space-between;
  margin-top: 6px;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 11px;
`;

const TaskList = styled.section`
  margin-top: 12px;
  overflow: hidden;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};
  h3 { padding: 14px; border-bottom: 1px solid ${({ theme }) => theme.color.border}; font-size: 14px; }
`;
const Task = styled.div`
  display: grid;
  grid-template-columns: 76px 1fr auto;
  gap: 9px;
  padding: 10px 14px;
  border-bottom: 1px solid ${({ theme }) => theme.color.border};
  font-size: 13px;
  &:last-child { border-bottom: 0; }
  span { color: ${({ theme }) => theme.color.textMuted}; }
  b { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
`;

function linePoints(points: ReportPeriodPoint[], secondary = false) {
  if (!points.length) return '';
  const values = points.flatMap(point => [point.value, point.secondaryValue ?? point.value]);
  const max = Math.max(...values, 1);
  const width = 600;
  return points.map((point, index) => {
    const x = points.length === 1 ? width / 2 : index * width / (points.length - 1);
    const value = secondary ? point.secondaryValue ?? point.value : point.value;
    return `${x},${140 - (value / max) * 125}`;
  }).join(' ');
}

function SeriesChart({ points, visualization }: { points: ReportPeriodPoint[]; visualization: string }) {
  if (!points.length) return <span>Sem dados no período.</span>;
  if (visualization === 'line') {
    const hasSecondary = points.some(point => point.secondaryValue !== undefined);
    return <>
      <Line viewBox="0 0 600 150" preserveAspectRatio="none" role="img" aria-label="Série temporal">
        {hasSecondary && <polyline className="secondary" points={linePoints(points, true)} />}
        <polyline points={linePoints(points)} />
      </Line>
      <Axis><span>{points[0].period}</span><span>{points.at(-1)?.period}</span></Axis>
    </>;
  }
  const max = Math.max(...points.map(point => point.value), 1);
  return <Bars>{points.slice(0, 12).map(point => <BarRow key={point.period}>
    <span title={point.period}>{point.period}</span>
    <Track><i style={{ width: `${Math.max(2, point.value * 100 / max)}%` }} /></Track>
    <b>{point.value}</b>
  </BarRow>)}</Bars>;
}

export function DashboardView({ dashboard }: { dashboard: Dashboard }) {
  return <>
    <KpiGrid>{dashboard.kpis.map(kpi => <Kpi key={kpi.key} $tone={kpi.tone}>
      <span>{kpi.label}</span><strong>{kpi.value.toLocaleString('pt-BR')}{kpi.unit ?? ''}</strong>
    </Kpi>)}</KpiGrid>
    {dashboard.series.length > 0 && <ChartGrid>{dashboard.series.map(series => <ChartCard key={series.key}>
      <h3>{series.title}</h3><SeriesChart points={series.points} visualization={series.visualization} />
    </ChartCard>)}</ChartGrid>}
    {dashboard.items.length > 0 && <TaskList><h3>Itens que exigem atenção</h3>{dashboard.items.map(item => <Task key={item.id}>
      <span>{item.projectKey ? `${item.projectKey}-${item.number}` : `#${item.number}`}</span>
      <b>{item.title}</b><span>{item.dueDate ? new Date(`${item.dueDate}T12:00:00`).toLocaleDateString('pt-BR') : item.status}</span>
    </Task>)}</TaskList>}
  </>;
}
