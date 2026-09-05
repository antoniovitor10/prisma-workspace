import { CalendarDays, CheckCircle2, Gauge, History } from 'lucide-react';
import { useMemo } from 'react';
import styled from 'styled-components';
import type { Sprint } from '../../types/scrum';
import { buildSprintHistory } from '../../utils/scrumAnalytics';

const Panel = styled.section`
  min-width: 0;
  margin-top: 12px;
  padding: 16px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};
`;

const Header = styled.header`
  margin-bottom: 12px;
  h3 { display: flex; align-items: center; gap: 7px; font-size: 14px; }
  p { margin-top: 3px; color: ${({ theme }) => theme.color.textMuted}; font-size: 12px; }
`;

const Summary = styled.div`
  display: grid;
  grid-template-columns: repeat(3, minmax(130px, 1fr));
  gap: 8px;
  margin-bottom: 12px;
  @media (max-width: 620px) { grid-template-columns: 1fr; }
`;

const SummaryCard = styled.article`
  display: flex;
  align-items: center;
  gap: 9px;
  padding: 11px;
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.neutral[50]};
  > span { display: grid; width: 30px; height: 30px; place-items: center; border-radius: ${({ theme }) => theme.radius.md}; background: ${({ theme }) => theme.color.surface}; color: ${({ theme }) => theme.color.brand}; }
  small, strong { display: block; }
  small { color: ${({ theme }) => theme.color.textMuted}; font-size: 11px; font-weight: 800; text-transform: uppercase; }
  strong { margin-top: 2px; font-size: 15px; }
`;

const List = styled.div`
  display: grid;
  gap: 8px;
`;

const SprintRow = styled.button<{ $active: boolean }>`
  display: grid;
  grid-template-columns: minmax(220px, 1.6fr) minmax(125px, .8fr) minmax(130px, .8fr) minmax(130px, 1fr);
  gap: 10px;
  align-items: center;
  width: 100%;
  padding: 12px;
  border: 1px solid ${({ $active, theme }) => $active ? theme.color.brand : theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ $active, theme }) => $active
    ? `color-mix(in srgb, ${theme.color.brand} 4%, ${theme.color.surface})`
    : theme.color.surface};
  color: ${({ theme }) => theme.color.text};
  text-align: left;
  &:hover { background: ${({ theme }) => theme.color.neutral[50]}; }
  @media (max-width: 800px) { grid-template-columns: minmax(200px, 1fr) minmax(120px, .7fr); > *:nth-child(2), > *:nth-child(3) { display: none; } }
`;

const Identity = styled.div`
  min-width: 0;
  strong, small { display: block; }
  strong { overflow: hidden; font-size: 13.5px; text-overflow: ellipsis; white-space: nowrap; }
  small { overflow: hidden; margin-top: 4px; color: ${({ theme }) => theme.color.textMuted}; font-size: 11px; text-overflow: ellipsis; white-space: nowrap; }
`;

const Metric = styled.div`
  small, strong { display: block; }
  small { color: ${({ theme }) => theme.color.textMuted}; font-size: 11px; font-weight: 750; text-transform: uppercase; }
  strong { margin-top: 3px; color: ${({ theme }) => theme.color.neutral[700]}; font-size: 13px; }
`;

const Delivery = styled.div`
  min-width: 0;
  > div { display: flex; justify-content: space-between; gap: 8px; color: ${({ theme }) => theme.color.neutral[700]}; font-size: 12px; font-weight: 750; }
`;

const Track = styled.span`
  display: block;
  height: 6px;
  margin-top: 7px;
  overflow: hidden;
  border-radius: ${({ theme }) => theme.radius.pill};
  background: ${({ theme }) => theme.color.neutral[100]};
`;

const Fill = styled.span<{ $value: number }>`
  display: block;
  width: ${({ $value }) => Math.max(0, Math.min(100, $value))}%;
  height: 100%;
  border-radius: inherit;
  background: ${({ $value, theme }) => $value >= 80 ? theme.color.success : theme.color.warning};
`;

const Empty = styled.div`
  padding: 40px 16px;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 13px;
  text-align: center;
`;

const formatPeriod = (start: string, end: string) => {
  const formatter = new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: 'short', timeZone: 'UTC' });
  return `${formatter.format(new Date(`${start}T00:00:00Z`))} — ${formatter.format(new Date(`${end}T00:00:00Z`))}`;
};

interface SprintHistoryPanelProps {
  sprints: Sprint[];
  selectedSprintId: string;
  onSelectSprint: (sprintId: string) => void;
  showPoints?: boolean;
}

export default function SprintHistoryPanel({ sprints, selectedSprintId, onSelectSprint, showPoints = true }: SprintHistoryPanelProps) {
  const history = useMemo(() => buildSprintHistory(sprints), [sprints]);
  const averageVelocity = history.length
    ? Math.round(history.reduce((sum, sprint) => sum + sprint.deliveredPoints, 0) / history.length * 10) / 10
    : 0;
  const averageCompletion = history.length
    ? Math.round(history.reduce((sum, sprint) => sum + sprint.completionRate, 0) / history.length)
    : 0;
  const deliveredItems = history.reduce((sum, sprint) => sum + sprint.completedItemCount, 0);

  return (
    <Panel>
      <Header>
        <h3><History size={15} />Histórico de sprints</h3>
        <p>Metas e resultados preservados para acompanhar evolução e previsibilidade.</p>
      </Header>

      <Summary>
        <SummaryCard><span><History size={14} /></span><div><small>Sprints encerradas</small><strong>{history.length}</strong></div></SummaryCard>
        {showPoints&&<SummaryCard><span><Gauge size={14} /></span><div><small>Velocity média</small><strong>{averageVelocity} pts</strong></div></SummaryCard>}
        <SummaryCard><span><CheckCircle2 size={14} /></span><div><small>Entrega média</small><strong>{averageCompletion}% · {deliveredItems} itens</strong></div></SummaryCard>
      </Summary>

      <List>
        {history.map((sprint) => (
          <SprintRow
            key={sprint.id}
            $active={sprint.id === selectedSprintId}
            onClick={() => onSelectSprint(sprint.id)}
            aria-label={`Consultar resultados de ${sprint.name}`}
          >
            <Identity><strong>{sprint.name}</strong><small>{sprint.goal || 'Sprint sem meta registrada'} · {sprint.teamName}</small></Identity>
            <Metric><small>Período</small><strong>{formatPeriod(sprint.startDate, sprint.endDate)} · {sprint.durationDays} dias</strong></Metric>
            <Metric><small>Escopo entregue</small><strong>{sprint.completedItemCount}/{sprint.itemCount} itens{showPoints?` · ${sprint.deliveredPoints}/${sprint.plannedPoints} pts`:''}</strong></Metric>
            <Delivery><div><span>Entrega</span><strong>{sprint.completionRate}%</strong></div><Track><Fill $value={sprint.completionRate} /></Track></Delivery>
          </SprintRow>
        ))}
        {history.length === 0 && <Empty><CalendarDays size={20} /><br />O histórico será formado quando a primeira sprint for encerrada.</Empty>}
      </List>
    </Panel>
  );
}
