import styled from 'styled-components';

interface Item {
  id: string;
  title: string;
  createdAt: string;
  dueDate?: string;
  stageId: string | null;
  taskTypeColor?: string | null;
}

interface Stage { id: string; name: string; }

interface Props {
  items: Item[];
  stages: Stage[];
  onOpen: (id: string) => void;
}

const DAY_PX = 28;
const LABEL_W = 240;

const Wrap = styled.div`
  background: #fff;
  border: 1px solid #E2E8F0;
  border-radius: 10px;
  overflow-x: auto;
`;

const Inner = styled.div`
  min-width: fit-content;
`;

const HeaderRow = styled.div`
  display: flex;
  border-bottom: 2px solid #E2E8F0;
  position: sticky;
  top: 0;
  background: #fff;
`;

const LabelCell = styled.div`
  flex: 0 0 ${LABEL_W}px;
  padding: 8px 12px;
  font-size: 14px;
  font-weight: 700;
  color: #64748B;
  border-right: 1px solid #E2E8F0;
`;

const DayCell = styled.div<{ $today?: boolean; $weekend?: boolean }>`
  flex: 0 0 ${DAY_PX}px;
  text-align: center;
  font-size: 13px;
  color: ${p => (p.$today ? '#1E7BD7' : '#94A3B8')};
  font-weight: ${p => (p.$today ? 800 : 400)};
  padding: 6px 0;
  background: ${p => (p.$today ? '#EBF4FD' : p.$weekend ? '#F8FAFC' : 'transparent')};
`;

const GroupRow = styled.div`
  padding: 8px 12px;
  font-size: 14px;
  font-weight: 800;
  color: #1D2D3E;
  background: #F1F5F9;
  border-bottom: 1px solid #E2E8F0;
`;

const Row = styled.div`
  display: flex;
  align-items: center;
  border-bottom: 1px solid #F1F5F9;
  position: relative;
  height: 36px;
`;

const RowLabel = styled.button`
  flex: 0 0 ${LABEL_W}px;
  padding: 0 12px;
  font-size: 15px;
  color: #334155;
  text-align: left;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  border-right: 1px solid #E2E8F0;
  height: 100%;
  &:hover { color: #1E7BD7; }
`;

const Track = styled.div`
  position: relative;
  height: 100%;
  flex: 1;
`;

const Bar = styled.button<{ $left: number; $width: number; $late?: boolean; $color?: string }>`
  position: absolute;
  top: 8px;
  height: 18px;
  left: ${p => p.$left}px;
  width: ${p => Math.max(p.$width, 10)}px;
  border-radius: 5px;
  background: ${p => (p.$late ? '#E53E3E' : p.$color || '#1E7BD7')};
  opacity: 0.85;
  &:hover { opacity: 1; }
`;

const TodayLine = styled.div<{ $left: number }>`
  position: absolute;
  top: 0;
  bottom: 0;
  left: ${p => p.$left}px;
  width: 2px;
  background: #1E7BD7;
  z-index: 2;
  pointer-events: none;
`;

const MS_DAY = 86400000;
const startOfDay = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate());

export const BoardGantt: React.FC<Props> = ({ items, stages, onOpen }) => {
  const today = startOfDay(new Date());
  const withDates = items.map(w => {
    const start = startOfDay(new Date(w.createdAt));
    const end = w.dueDate ? startOfDay(new Date(`${w.dueDate}T12:00:00`)) : new Date(start.getTime() + 3 * MS_DAY);
    return { ...w, start, end: end < start ? start : end };
  });

  if (withDates.length === 0) {
    return <Wrap><div style={{ padding: 40, textAlign: 'center', color: '#94A3B8' }}>Nenhuma tarefa para exibir.</div></Wrap>;
  }

  let min = new Date(Math.min(...withDates.map(w => w.start.getTime()), today.getTime()));
  let max = new Date(Math.max(...withDates.map(w => w.end.getTime()), today.getTime()));
  min = new Date(min.getTime() - 2 * MS_DAY);
  max = new Date(max.getTime() + 3 * MS_DAY);
  const totalDays = Math.round((max.getTime() - min.getTime()) / MS_DAY) + 1;
  const days = Array.from({ length: totalDays }, (_, i) => new Date(min.getTime() + i * MS_DAY));
  const dayIndex = (d: Date) => Math.round((d.getTime() - min.getTime()) / MS_DAY);
  const todayLeft = dayIndex(today) * DAY_PX;

  const groups: { stage: Stage | null; rows: typeof withDates }[] = [
    ...stages.map(s => ({ stage: s as Stage | null, rows: withDates.filter(w => w.stageId === s.id) })),
    { stage: null, rows: withDates.filter(w => !w.stageId || !stages.some(s => s.id === w.stageId)) }
  ].filter(g => g.rows.length > 0);

  return (
    <Wrap>
      <Inner>
        <HeaderRow>
          <LabelCell>Tarefa</LabelCell>
          {days.map(d => (
            <DayCell
              key={d.getTime()}
              $today={d.getTime() === today.getTime()}
              $weekend={d.getDay() === 0 || d.getDay() === 6}
            >
              {d.getDate() === 1 || d.getTime() === min.getTime()
                ? d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' })
                : d.getDate()}
            </DayCell>
          ))}
        </HeaderRow>

        {groups.map((g, gi) => (
          <div key={g.stage?.id ?? `none-${gi}`}>
            <GroupRow>{g.stage?.name ?? 'Sem etapa'}</GroupRow>
            {g.rows.map(w => {
              const left = dayIndex(w.start) * DAY_PX;
              const width = (dayIndex(w.end) - dayIndex(w.start) + 1) * DAY_PX;
              const late = !!w.dueDate && w.end < today;
              return (
                <Row key={w.id}>
                  <RowLabel onClick={() => onOpen(w.id)} title={w.title}>{w.title}</RowLabel>
                  <Track style={{ width: totalDays * DAY_PX }}>
                    <TodayLine $left={todayLeft} />
                    <Bar
                      $left={left}
                      $width={width}
                      $late={late}
                      $color={w.taskTypeColor || undefined}
                      title={`${w.title} (${w.start.toLocaleDateString('pt-BR')} → ${w.end.toLocaleDateString('pt-BR')})`}
                      onClick={() => onOpen(w.id)}
                    />
                  </Track>
                </Row>
              );
            })}
          </div>
        ))}
      </Inner>
    </Wrap>
  );
};
