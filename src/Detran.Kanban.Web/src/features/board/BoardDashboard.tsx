import { useEffect, useState } from 'react';
import styled from 'styled-components';
import { api } from '../../services/api';

interface Metrics {
  tasksByStage: { stage: string; count: number }[];
  lateCount: number;
  deliveredThisWeek: number;
  hoursByUser: { user: string; hours: number }[];
  burndown: { date: string; open: number }[];
  tasksByType: { type: string; color: string; count: number }[];
}

interface Props { boardId: string; }

const Grid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
  gap: 16px;
`;

const Card = styled.div`
  background: #fff;
  border: 1px solid #E2E8F0;
  border-radius: 10px;
  padding: 18px;
`;

const CardTitle = styled.h3`
  font-size: 15px;
  font-weight: 700;
  color: #64748B;
  text-transform: uppercase;
  letter-spacing: 0.4px;
  margin-bottom: 14px;
`;

const Big = styled.div<{ $danger?: boolean }>`
  font-size: 40px;
  font-weight: 800;
  color: ${p => (p.$danger ? '#E53E3E' : '#1D2D3E')};
`;

const BarRow = styled.div`
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 8px;
  font-size: 15px;
  color: #334155;
`;

const BarLabel = styled.span`
  flex: 0 0 140px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
`;

const BarTrack = styled.div`
  flex: 1;
  height: 14px;
  background: #F1F5F9;
  border-radius: 4px;
  overflow: hidden;
`;

const BarFill = styled.div<{ $pct: number; $color?: string }>`
  height: 100%;
  width: ${p => Math.max(2, p.$pct)}%;
  background: ${p => p.$color || '#1E7BD7'};
`;

export const BoardDashboard: React.FC<Props> = ({ boardId }) => {
  const [m, setM] = useState<Metrics | null>(null);
  const [error, setError] = useState(false);

  useEffect(() => {
    let alive = true;
    api.request(`/api/Boards/${boardId}/metrics`)
      .then(d => { if (alive) setM(d); })
      .catch(() => { if (alive) setError(true); });
    return () => { alive = false; };
  }, [boardId]);

  if (error) return <Card>Erro ao carregar métricas.</Card>;
  if (!m) return <Card>Carregando métricas...</Card>;

  const maxStage = Math.max(1, ...m.tasksByStage.map(s => s.count));
  const maxUser = Math.max(1, ...m.hoursByUser.map(u => u.hours));
  const maxType = Math.max(1, ...m.tasksByType.map(t => t.count));
  const maxOpen = Math.max(1, ...m.burndown.map(b => b.open));

  // Burndown SVG
  const W = 300, H = 90;
  const pts = m.burndown.map((b, i) => {
    const x = (i / Math.max(1, m.burndown.length - 1)) * W;
    const y = H - (b.open / maxOpen) * (H - 10) - 5;
    return `${x},${y}`;
  }).join(' ');

  return (
    <Grid>
      <Card>
        <CardTitle>Tarefas por etapa</CardTitle>
        {m.tasksByStage.map(s => (
          <BarRow key={s.stage}>
            <BarLabel title={s.stage}>{s.stage}</BarLabel>
            <BarTrack><BarFill $pct={(s.count / maxStage) * 100} /></BarTrack>
            <strong>{s.count}</strong>
          </BarRow>
        ))}
        {m.tasksByStage.length === 0 && <span style={{ color: '#94A3B8', fontSize: 15 }}>Sem etapas.</span>}
      </Card>

      <Card>
        <CardTitle>Atrasadas</CardTitle>
        <Big $danger={m.lateCount > 0}>{m.lateCount}</Big>
        <span style={{ fontSize: 15, color: '#64748B' }}>tarefas vencidas e não concluídas</span>
      </Card>

      <Card>
        <CardTitle>Entregues nesta semana</CardTitle>
        <Big>{m.deliveredThisWeek}</Big>
        <span style={{ fontSize: 15, color: '#64748B' }}>chegaram à última etapa</span>
      </Card>

      <Card>
        <CardTitle>Horas por pessoa</CardTitle>
        {m.hoursByUser.map(u => (
          <BarRow key={u.user}>
            <BarLabel title={u.user}>{u.user}</BarLabel>
            <BarTrack><BarFill $pct={(u.hours / maxUser) * 100} $color="#10B981" /></BarTrack>
            <strong>{u.hours}h</strong>
          </BarRow>
        ))}
        {m.hoursByUser.length === 0 && <span style={{ color: '#94A3B8', fontSize: 15 }}>Sem horas apontadas.</span>}
      </Card>

      <Card>
        <CardTitle>Burndown (14 dias) — tarefas abertas</CardTitle>
        <svg viewBox={`0 0 ${W} ${H}`} style={{ width: '100%', height: 'auto' }}>
          <polyline points={pts} fill="none" stroke="#1E7BD7" strokeWidth="2" />
          {m.burndown.map((b, i) => {
            const x = (i / Math.max(1, m.burndown.length - 1)) * W;
            const y = H - (b.open / maxOpen) * (H - 10) - 5;
            return <circle key={b.date} cx={x} cy={y} r="2.5" fill="#1E7BD7" />;
          })}
        </svg>
        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13.5, color: '#94A3B8' }}>
          <span>{m.burndown[0]?.date.slice(5)}</span>
          <span>{m.burndown[m.burndown.length - 1]?.date.slice(5)}</span>
        </div>
      </Card>

      <Card>
        <CardTitle>Tarefas por tipo</CardTitle>
        {m.tasksByType.map(t => (
          <BarRow key={t.type}>
            <BarLabel title={t.type}>{t.type}</BarLabel>
            <BarTrack><BarFill $pct={(t.count / maxType) * 100} $color={t.color} /></BarTrack>
            <strong>{t.count}</strong>
          </BarRow>
        ))}
        {m.tasksByType.length === 0 && <span style={{ color: '#94A3B8', fontSize: 15 }}>Sem tarefas.</span>}
      </Card>
    </Grid>
  );
};
