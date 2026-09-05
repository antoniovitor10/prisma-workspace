import { useState } from 'react';
import styled from 'styled-components';
import { ChevronLeft, ChevronRight } from 'lucide-react';

interface Item {
  id: string;
  title: string;
  dueDate?: string;
  stageId: string | null;
  taskTypeColor?: string | null;
}

interface Props {
  items: Item[];
  onOpen: (id: string) => void;
}

const Wrap = styled.div`
  background: #fff;
  border: 1px solid #E2E8F0;
  border-radius: 10px;
  padding: 16px;
`;

const Head = styled.div`
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 16px;
  margin-bottom: 14px;
  strong { font-size: 16px; color: #1D2D3E; text-transform: capitalize; }
`;

const NavBtn = styled.button`
  display: inline-flex;
  align-items: center;
  padding: 6px;
  border-radius: 6px;
  color: #64748B;
  &:hover { background: #F1F5F9; }
`;

const Grid = styled.div`
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 6px;
`;

const DowLabel = styled.div`
  font-size: 13.5px;
  font-weight: 700;
  color: #94A3B8;
  text-transform: uppercase;
  text-align: center;
  padding: 4px 0;
`;

const Day = styled.div<{ $other?: boolean; $today?: boolean }>`
  min-height: 92px;
  border: 1px solid ${p => (p.$today ? '#1E7BD7' : '#EDF2F7')};
  border-radius: 8px;
  padding: 6px;
  background: ${p => (p.$other ? '#F8FAFC' : '#fff')};
  display: flex;
  flex-direction: column;
  gap: 4px;
`;

const DayNum = styled.span<{ $today?: boolean }>`
  font-size: 14px;
  font-weight: 700;
  color: ${p => (p.$today ? '#1E7BD7' : '#64748B')};
`;

const Pill = styled.button<{ $late?: boolean; $color?: string }>`
  display: block;
  width: 100%;
  text-align: left;
  font-size: 13.5px;
  font-weight: 600;
  padding: 3px 6px;
  border-radius: 4px;
  color: ${p => (p.$late ? '#B42318' : '#1D2D3E')};
  background: ${p => (p.$late ? '#FDEEEE' : '#EDF2F7')};
  border-left: 3px solid ${p => p.$color || '#1E7BD7'};
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  &:hover { filter: brightness(0.95); }
`;

const pad = (n: number) => String(n).padStart(2, '0');
const iso = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;

export const BoardCalendar: React.FC<Props> = ({ items, onOpen }) => {
  const [cursor, setCursor] = useState(() => { const d = new Date(); return new Date(d.getFullYear(), d.getMonth(), 1); });

  const year = cursor.getFullYear();
  const month = cursor.getMonth();
  const first = new Date(year, month, 1);
  const startOffset = (first.getDay() + 6) % 7; // segunda = 0
  const gridStart = new Date(year, month, 1 - startOffset);
  const todayIso = iso(new Date());

  const cells: Date[] = [];
  for (let i = 0; i < 42; i++) {
    cells.push(new Date(gridStart.getFullYear(), gridStart.getMonth(), gridStart.getDate() + i));
  }

  const monthLabel = cursor.toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' });

  return (
    <Wrap>
      <Head>
        <NavBtn onClick={() => setCursor(new Date(year, month - 1, 1))}><ChevronLeft size={18} /></NavBtn>
        <strong>{monthLabel}</strong>
        <NavBtn onClick={() => setCursor(new Date(year, month + 1, 1))}><ChevronRight size={18} /></NavBtn>
      </Head>
      <Grid>
        {['Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb', 'Dom'].map(d => <DowLabel key={d}>{d}</DowLabel>)}
        {cells.map(d => {
          const dIso = iso(d);
          const dayItems = items.filter(w => w.dueDate === dIso);
          const other = d.getMonth() !== month;
          return (
            <Day key={dIso} $other={other} $today={dIso === todayIso}>
              <DayNum $today={dIso === todayIso}>{d.getDate()}</DayNum>
              {dayItems.slice(0, 4).map(w => (
                <Pill
                  key={w.id}
                  $late={dIso < todayIso}
                  $color={w.taskTypeColor || undefined}
                  title={w.title}
                  onClick={() => onOpen(w.id)}
                >{w.title}</Pill>
              ))}
              {dayItems.length > 4 && <span style={{ fontSize: 13, color: '#94A3B8' }}>+{dayItems.length - 4} mais</span>}
            </Day>
          );
        })}
      </Grid>
    </Wrap>
  );
};
