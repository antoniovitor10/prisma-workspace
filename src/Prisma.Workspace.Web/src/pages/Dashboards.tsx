import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import styled from 'styled-components';
import { DashboardView } from '../features/reports/DashboardView';
import { api } from '../services/api';
import type { Dashboard } from '../types/reports';
import type { ProjectSummary } from './Projects';

const Page = styled.main`
  max-width: 1500px;
  margin: 0 auto;
  padding: 26px 30px 40px;
  @media (max-width: 760px) { padding: 18px 14px 30px; }
`;
const Header = styled.header`
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 16px;
  h1 { font-size: 23px; }
  p { margin-top: 5px; color: ${({ theme }) => theme.color.textMuted}; font-size: 13.5px; }
`;
const Tabs = styled.div`display: flex; gap: 5px;`;
const Tab = styled.button<{ $active: boolean }>`
  min-height: 35px;
  padding: 0 12px;
  border: 1px solid ${({ theme, $active }) => $active ? theme.color.brand : theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme, $active }) => $active ? theme.color.brand : theme.color.surface};
  color: ${({ theme, $active }) => $active ? theme.color.onBrand : theme.color.text};
  font-size: 13px;
  font-weight: 800;
`;
const Filters = styled.div`
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 13px;
  padding: 11px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};
  label { display: grid; gap: 4px; color: ${({ theme }) => theme.color.textMuted}; font-size: 12px; font-weight: 750; }
  input, select { min-height: 33px; padding: 0 8px; border: 1px solid ${({ theme }) => theme.color.border}; border-radius: ${({ theme }) => theme.radius.md}; background: ${({ theme }) => theme.color.surface}; color: ${({ theme }) => theme.color.text}; font-size: 13px; }
`;
const State = styled.div`
  display: grid;
  min-height: 260px;
  place-items: center;
  padding: 30px;
  color: ${({ theme }) => theme.color.textMuted};
  text-align: center;
  font-size: 13.5px;
`;

export function Dashboards() {
  const [view, setView] = useState<'collaborator' | 'manager'>('collaborator');
  const [projectId, setProjectId] = useState('');
  const [priority, setPriority] = useState<number | ''>('');
  const [from, setFrom] = useState(() => {
    const date = new Date(); date.setDate(date.getDate() - 6); return date.toISOString().slice(0, 10);
  });
  const [to, setTo] = useState(() => {
    const date = new Date(); date.setDate(date.getDate() + 14); return date.toISOString().slice(0, 10);
  });
  const projectsQuery = useQuery<ProjectSummary[]>({ queryKey: ['projects', false], queryFn: () => api.getProjects(false) });
  const dashboardQuery = useQuery<Dashboard>({
    queryKey: ['dashboard', view, projectId, priority, from, to],
    queryFn: () => view === 'collaborator'
      ? api.getCollaboratorDashboard({ from, to })
      : api.getManagerDashboard({ projectId: projectId || undefined, priority: priority === '' ? undefined : priority, from, to }),
  });

  return <Page>
    <Header><div><h1>Dashboards</h1><p>O que precisa de atenção agora e a visão operacional da gestão.</p></div><Tabs><Tab $active={view === 'collaborator'} onClick={() => setView('collaborator')}>Colaborador</Tab><Tab $active={view === 'manager'} onClick={() => setView('manager')}>Gestor</Tab></Tabs></Header>
    <Filters>
      {view === 'manager' && <label>Projeto<select value={projectId} onChange={event => setProjectId(event.target.value)}><option value="">Todos os projetos</option>{projectsQuery.data?.map(project => <option key={project.id} value={project.id}>{project.key} · {project.name}</option>)}</select></label>}
      {view === 'manager' && <label>Prioridade<select value={priority} onChange={event => setPriority(event.target.value === '' ? '' : Number(event.target.value))}><option value="">Todas</option><option value={0}>Baixa</option><option value={1}>Média</option><option value={2}>Alta</option><option value={3}>Crítica</option></select></label>}
      <label>Data inicial<input type="date" value={from} onChange={event => setFrom(event.target.value)} /></label>
      <label>Data final<input type="date" min={from} value={to} onChange={event => setTo(event.target.value)} /></label>
    </Filters>
    {dashboardQuery.isLoading && <State>Carregando indicadores...</State>}
    {dashboardQuery.error && <State role="alert">{(dashboardQuery.error as Error).message}</State>}
    {dashboardQuery.data && <DashboardView dashboard={dashboardQuery.data} />}
  </Page>;
}
