import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import styled from 'styled-components';
import type { ReportBreakdown, ReportPeriodPoint } from '../../types/reports';

// Paleta Prisma WorkSpace (D68).
export const CHART_COLORS = [
  '#2563EB', '#06B6D4', '#10B981', '#F97316', '#E11D48',
  '#7C3AED', '#DB2777', '#64748B',
  '#7A5CC6', '#00A9A5', '#E8833A', '#546E7A', '#C2185B',
];

const Empty = styled.p`
  padding: 26px 8px;
  color: ${({ theme }) => theme.color.textMuted};
  text-align: center;
  font-size: 13px;
`;

const tooltipStyle = {
  fontSize: 13.5,
  borderRadius: 8,
  border: '1px solid #e2e5ea',
  padding: '6px 10px',
};

export function DonutChart({ data, height = 210 }: { data: ReportBreakdown[]; height?: number }) {
  const items = data.filter((item) => item.value > 0);
  if (!items.length) return <Empty>Sem dados no período.</Empty>;
  return (
    <ResponsiveContainer width="100%" height={height}>
      <PieChart>
        <Pie data={items} dataKey="value" nameKey="label" innerRadius="52%" outerRadius="82%" paddingAngle={2}>
          {items.map((_, index) => <Cell key={index} fill={CHART_COLORS[index % CHART_COLORS.length]} />)}
        </Pie>
        <Tooltip contentStyle={tooltipStyle} />
        <Legend verticalAlign="bottom" height={28} iconType="circle" wrapperStyle={{ fontSize: 13 }} />
      </PieChart>
    </ResponsiveContainer>
  );
}

export function BarsChart({ data, height = 220, color = '#06B6D4' }: { data: ReportBreakdown[]; height?: number; color?: string }) {
  const items = data.filter((item) => item.value > 0).slice(0, 10);
  if (!items.length) return <Empty>Sem dados no período.</Empty>;
  return (
    <ResponsiveContainer width="100%" height={height}>
      <BarChart data={items} layout="vertical" margin={{ left: 6, right: 16, top: 4, bottom: 4 }}>
        <CartesianGrid horizontal={false} stroke="#eef1f5" />
        <XAxis type="number" tick={{ fontSize: 12 }} allowDecimals={false} />
        <YAxis type="category" dataKey="label" width={112} tick={{ fontSize: 12 }} />
        <Tooltip contentStyle={tooltipStyle} cursor={{ fill: 'rgba(0,142,207,.08)' }} />
        <Bar dataKey="value" fill={color} radius={[0, 4, 4, 0]} barSize={14} />
      </BarChart>
    </ResponsiveContainer>
  );
}

export function GroupedBars({
  data, keys, height = 230,
}: { data: Array<Record<string, string | number>>; keys: Array<{ key: string; label: string; color: string }>; height?: number }) {
  if (!data.length) return <Empty>Sem dados no período.</Empty>;
  return (
    <ResponsiveContainer width="100%" height={height}>
      <BarChart data={data} margin={{ left: 0, right: 8, top: 6, bottom: 4 }}>
        <CartesianGrid vertical={false} stroke="#eef1f5" />
        <XAxis dataKey="label" tick={{ fontSize: 12 }} />
        <YAxis tick={{ fontSize: 12 }} allowDecimals={false} />
        <Tooltip contentStyle={tooltipStyle} cursor={{ fill: 'rgba(0,142,207,.08)' }} />
        <Legend iconType="circle" wrapperStyle={{ fontSize: 13 }} />
        {keys.map((series) => <Bar key={series.key} dataKey={series.key} name={series.label} fill={series.color} radius={[4, 4, 0, 0]} barSize={18} />)}
      </BarChart>
    </ResponsiveContainer>
  );
}

export function AreaTrend({
  points, height = 220, color = '#2563EB', label = 'Valor',
}: { points: ReportPeriodPoint[]; height?: number; color?: string; label?: string }) {
  if (!points.length) return <Empty>Sem dados no período.</Empty>;
  return (
    <ResponsiveContainer width="100%" height={height}>
      <AreaChart data={points} margin={{ left: 0, right: 8, top: 6, bottom: 4 }}>
        <defs>
          <linearGradient id={`grad-${color.replace('#', '')}`} x1="0" y1="0" x2="0" y2="1">
            <stop offset="5%" stopColor={color} stopOpacity={0.28} />
            <stop offset="95%" stopColor={color} stopOpacity={0} />
          </linearGradient>
        </defs>
        <CartesianGrid vertical={false} stroke="#eef1f5" />
        <XAxis dataKey="period" tick={{ fontSize: 12 }} />
        <YAxis tick={{ fontSize: 12 }} allowDecimals={false} />
        <Tooltip contentStyle={tooltipStyle} />
        <Area type="monotone" dataKey="value" name={label} stroke={color} strokeWidth={2} fill={`url(#grad-${color.replace('#', '')})`} />
      </AreaChart>
    </ResponsiveContainer>
  );
}
