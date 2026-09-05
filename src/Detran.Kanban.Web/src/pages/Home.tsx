import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import styled from 'styled-components';
import {
  AlertTriangle,
  ArrowRight,
  BarChart3,
  CalendarCheck,
  CheckCircle2,
  CircleDot,
  Clock3,
  FolderKanban,
  Inbox,
  LayoutDashboard,
  ListChecks,
  Sparkles,
} from 'lucide-react';
import { Page } from '../components/PageLayout';
import { useOrganization } from '../features/organizations/OrganizationState';
import { api } from '../services/api';
import type { ProjectSummary } from './Projects';

interface HomeTask {
  id: string;
  number: number;
  title: string;
  boardId: string;
  boardName: string;
  projectKey?: string | null;
  stageName?: string | null;
  priority: number;
  dueDate?: string | null;
  today: boolean;
  overdue: boolean;
  blocked: boolean;
}

interface HomeDashboard {
  summary: {
    assigned: number;
    today: number;
    thisWeek: number;
    overdue: number;
    blocked: number;
    pendingApprovals: number;
  };
  tasks: HomeTask[];
  importantNotifications: Array<{
    type: string;
    severity: string;
    title: string;
    message: string;
    workItemId?: string | null;
  }>;
}

const HomePage = styled(Page)`display:grid;gap:18px;`;
const Welcome = styled.section`
  position:relative;display:grid;grid-template-columns:minmax(0,1fr) auto;align-items:center;gap:26px;
  min-height:200px;padding:30px 32px;overflow:hidden;border:1px solid ${({theme})=>theme.color.border};
  border-radius:${({theme})=>theme.radius.xl};background:
    radial-gradient(circle at 84% 18%, color-mix(in srgb, ${({theme})=>theme.color.accentMagenta} 15%, transparent), transparent 30%),
    radial-gradient(circle at 65% 100%, color-mix(in srgb, ${({theme})=>theme.color.accentBlue} 13%, transparent), transparent 35%),
    ${({theme})=>theme.color.surface};box-shadow:${({theme})=>theme.shadow.md};
  &::before{content:'';position:absolute;inset:0 auto 0 0;width:5px;background:${({theme})=>theme.color.gradient};}
  @media(max-width:760px){grid-template-columns:1fr;padding:24px 20px;}
`;
const WelcomeCopy = styled.div`
  position:relative;z-index:1;max-width:760px;
  .eyebrow{display:flex;align-items:center;gap:7px;margin-bottom:10px;color:${({theme})=>theme.color.brand};font-size:11px;font-weight:850;letter-spacing:.12em;text-transform:uppercase;}
  h1{font-size:clamp(27px,3.1vw,44px);line-height:1.08;letter-spacing:-.045em;}
  p{max-width:630px;margin-top:12px;color:${({theme})=>theme.color.textMuted};font-size:15px;line-height:1.55;}
`;
const WelcomeActions = styled.div`position:relative;z-index:1;display:grid;gap:9px;min-width:190px;@media(max-width:760px){grid-template-columns:1fr 1fr;min-width:0;}@media(max-width:430px){grid-template-columns:1fr;}`;
const Action = styled.button<{$primary?:boolean}>`
  display:flex;min-height:44px;align-items:center;justify-content:center;gap:8px;padding:0 16px;
  border:1px solid ${({theme,$primary})=>$primary?theme.color.brand:theme.color.border};border-radius:${({theme})=>theme.radius.md};
  background:${({theme,$primary})=>$primary?theme.color.gradient:theme.color.surface};color:${({theme,$primary})=>$primary?'#fff':theme.color.text};
  box-shadow:${({theme,$primary})=>$primary?'0 8px 22px rgba(79,70,229,.2)':theme.shadow.sm};font-size:13px;font-weight:800;
  &:hover{transform:translateY(-1px);border-color:${({theme})=>theme.color.brand};}
`;
const Metrics = styled.section`display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:11px;@media(max-width:850px){grid-template-columns:repeat(2,1fr);}@media(max-width:460px){grid-template-columns:1fr;}`;
const Metric = styled.button<{$tone?:'danger'|'warning'|'brand'}>`
  position:relative;display:grid;grid-template-columns:auto 1fr auto;align-items:center;gap:11px;min-height:86px;padding:15px 16px;text-align:left;
  border:1px solid ${({theme,$tone})=>$tone==='danger'?`color-mix(in srgb, ${theme.color.danger} 30%, ${theme.color.border})`:theme.color.border};
  border-radius:${({theme})=>theme.radius.card};background:${({theme})=>theme.color.surface};box-shadow:${({theme})=>theme.shadow.sm};
  .icon{display:grid;width:38px;height:38px;place-items:center;border-radius:12px;background:${({theme,$tone})=>$tone==='danger'?`color-mix(in srgb, ${theme.color.danger} 10%, transparent)`:$tone==='warning'?`color-mix(in srgb, ${theme.color.warning} 12%, transparent)`:`color-mix(in srgb, ${theme.color.brand} 9%, transparent)`};color:${({theme,$tone})=>$tone==='danger'?theme.color.danger:$tone==='warning'?theme.color.warning:theme.color.brand};}
  span{display:block;color:${({theme})=>theme.color.textMuted};font-size:11px;font-weight:800;letter-spacing:.04em;text-transform:uppercase;}
  strong{display:block;margin-top:3px;font-size:23px;letter-spacing:-.04em;}
  >svg{color:${({theme})=>theme.color.textSubtle};}
  &:hover{transform:translateY(-1px);border-color:${({theme})=>theme.color.borderStrong};box-shadow:${({theme})=>theme.shadow.md};}
`;
const MainGrid = styled.div`display:grid;grid-template-columns:minmax(0,1.7fr) minmax(300px,.8fr);gap:16px;align-items:start;@media(max-width:1020px){grid-template-columns:1fr;}`;
const Panel = styled.section`overflow:hidden;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.card};background:${({theme})=>theme.color.surface};box-shadow:${({theme})=>theme.shadow.sm};`;
const PanelHeader = styled.header`
  display:flex;align-items:center;gap:9px;min-height:58px;padding:12px 16px;border-bottom:1px solid ${({theme})=>theme.color.border};
  .title{min-width:0;}h2{font-size:14px;letter-spacing:-.01em;}p{margin-top:3px;color:${({theme})=>theme.color.textMuted};font-size:11.5px;}
  button{display:flex;align-items:center;gap:5px;margin-left:auto;color:${({theme})=>theme.color.brand};font-size:12px;font-weight:800;}
`;
const TaskList = styled.div`display:grid;`;
const TaskRow = styled.button<{$late?:boolean}>`
  display:grid;grid-template-columns:auto minmax(0,1fr) auto;align-items:center;gap:12px;width:100%;padding:13px 16px;text-align:left;
  border-bottom:1px solid ${({theme})=>theme.color.border};background:${({theme,$late})=>$late?`color-mix(in srgb, ${theme.color.danger} 4%, ${theme.color.surface})`:'transparent'};color:${({theme})=>theme.color.text};
  &:last-child{border-bottom:0;}&:hover{background:${({theme})=>theme.color.surfaceSubtle};}
`;
const TaskStatus = styled.span<{$blocked?:boolean;$late?:boolean}>`width:9px;height:9px;border-radius:50%;background:${({theme,$blocked,$late})=>$blocked?theme.color.warning:$late?theme.color.danger:theme.color.success};box-shadow:0 0 0 4px ${({theme,$blocked,$late})=>$blocked?`color-mix(in srgb, ${theme.color.warning} 11%, transparent)`:$late?`color-mix(in srgb, ${theme.color.danger} 10%, transparent)`:`color-mix(in srgb, ${theme.color.success} 10%, transparent)`};`;
const TaskCopy = styled.div`min-width:0;span{color:${({theme})=>theme.color.textMuted};font-size:11.5px;}strong{display:block;margin-top:4px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;font-size:13.5px;}`;
const Due = styled.span<{$late?:boolean}>`display:flex;align-items:center;gap:5px;color:${({theme,$late})=>$late?theme.color.danger:theme.color.textMuted};font-size:11.5px;font-weight:700;white-space:nowrap;@media(max-width:520px){display:none;}`;
const Side = styled.aside`display:grid;gap:14px;`;
const ProjectList = styled.div`display:grid;`;
const ProjectRow = styled.button`
  display:grid;grid-template-columns:38px minmax(0,1fr) auto;align-items:center;gap:10px;padding:12px 14px;text-align:left;border-bottom:1px solid ${({theme})=>theme.color.border};color:${({theme})=>theme.color.text};
  &:last-child{border-bottom:0;}&:hover{background:${({theme})=>theme.color.surfaceSubtle};}
  .mark{display:grid;width:38px;height:38px;place-items:center;border-radius:11px;background:color-mix(in srgb, ${({theme})=>theme.color.brand} 9%, transparent);color:${({theme})=>theme.color.brand};}
  strong{display:block;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;font-size:13px;}small{display:block;margin-top:3px;color:${({theme})=>theme.color.textMuted};font-size:11px;}
  >svg{color:${({theme})=>theme.color.textSubtle};}
`;
const QuickGrid = styled.div`display:grid;grid-template-columns:1fr 1fr;gap:8px;padding:12px;`;
const Quick = styled.button`
  display:grid;justify-items:start;gap:8px;min-height:92px;padding:12px;text-align:left;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.md};background:${({theme})=>theme.color.surfaceSubtle};color:${({theme})=>theme.color.text};
  svg{color:${({theme})=>theme.color.brand};}strong{font-size:12px;}span{color:${({theme})=>theme.color.textMuted};font-size:10.5px;line-height:1.35;}
  &:hover{border-color:${({theme})=>theme.color.brand};background:color-mix(in srgb, ${({theme})=>theme.color.brand} 5%, ${({theme})=>theme.color.surface});}
`;
const Empty = styled.div`display:grid;min-height:170px;place-items:center;padding:28px;color:${({theme})=>theme.color.textMuted};font-size:13px;text-align:center;`;
const ErrorBox = styled.div`padding:13px 16px;border:1px solid color-mix(in srgb, ${({theme})=>theme.color.danger} 34%, ${({theme})=>theme.color.border});border-radius:${({theme})=>theme.radius.md};background:color-mix(in srgb, ${({theme})=>theme.color.danger} 5%, ${({theme})=>theme.color.surface});color:${({theme})=>theme.color.danger};font-size:13px;`;

function greeting() {
  const hour = new Date().getHours();
  if (hour < 12) return 'Bom dia';
  if (hour < 18) return 'Boa tarde';
  return 'Boa noite';
}

function dueLabel(date?: string | null) {
  if (!date) return 'Sem prazo';
  return new Date(`${date}T12:00:00`).toLocaleDateString('pt-BR', { day: '2-digit', month: 'short' });
}

export function Home() {
  const navigate = useNavigate();
  const { current } = useOrganization();
  const workQuery = useQuery<HomeDashboard>({ queryKey: ['my-work-home', current.id], queryFn: () => api.getMyWork() });
  const projectsQuery = useQuery<ProjectSummary[]>({ queryKey: ['projects', false], queryFn: () => api.getProjects(false) });

  const focusTasks = useMemo(() => [...(workQuery.data?.tasks ?? [])]
    .filter(task => !task.stageName?.toLocaleLowerCase('pt-BR').includes('conclu'))
    .sort((left, right) => Number(right.overdue) - Number(left.overdue)
      || Number(right.blocked) - Number(left.blocked)
      || Number(right.today) - Number(left.today)
      || right.priority - left.priority)
    .slice(0, 6), [workQuery.data]);
  const recentProjects = useMemo(() => [...(projectsQuery.data ?? [])]
    .sort((left, right) => String(right.updatedAt ?? right.createdAt ?? '').localeCompare(String(left.updatedAt ?? left.createdAt ?? '')))
    .slice(0, 4), [projectsQuery.data]);
  const summary = workQuery.data?.summary;
  const hasError = workQuery.error || projectsQuery.error;

  return <HomePage>
    <Welcome>
      <WelcomeCopy>
        <span className="eyebrow"><Sparkles size={13}/>{greeting()} · visão geral</span>
        <h1>O trabalho que importa, claro desde o primeiro olhar.</h1>
        <p>Veja o que pede atenção agora, retome seus projetos e avance sem precisar procurar onde começar em {current.name}.</p>
      </WelcomeCopy>
      <WelcomeActions>
        <Action $primary onClick={() => navigate('/me/tasks')}><ListChecks size={15}/>Ver meu trabalho</Action>
        <Action onClick={() => navigate('/projects')}><FolderKanban size={15}/>Explorar projetos</Action>
      </WelcomeActions>
    </Welcome>

    {hasError && <ErrorBox role="alert">Parte da visão geral não pôde ser carregada. Os atalhos continuam disponíveis.</ErrorBox>}

    <Metrics aria-label="Resumo do trabalho">
      <Metric onClick={() => navigate('/me/tasks')}><span className="icon"><ListChecks size={18}/></span><span>Atribuídas<strong>{summary?.assigned ?? '—'}</strong></span><ArrowRight size={14}/></Metric>
      <Metric onClick={() => navigate('/me/tasks')}><span className="icon"><CalendarCheck size={18}/></span><span>Para hoje<strong>{summary?.today ?? '—'}</strong></span><ArrowRight size={14}/></Metric>
      <Metric $tone="danger" onClick={() => navigate('/me/tasks')}><span className="icon"><AlertTriangle size={18}/></span><span>Atrasadas<strong>{summary?.overdue ?? '—'}</strong></span><ArrowRight size={14}/></Metric>
      <Metric $tone="warning" onClick={() => navigate('/me/tasks')}><span className="icon"><CircleDot size={18}/></span><span>Bloqueadas<strong>{summary?.blocked ?? '—'}</strong></span><ArrowRight size={14}/></Metric>
    </Metrics>

    <MainGrid>
      <Panel>
        <PanelHeader><Clock3 size={16}/><div className="title"><h2>Sua prioridade agora</h2><p>Ordenada por atraso, bloqueio, prazo e importância.</p></div><button onClick={() => navigate('/me/tasks')}>Ver tudo<ArrowRight size={12}/></button></PanelHeader>
        {workQuery.isLoading ? <Empty>Organizando suas prioridades...</Empty> : focusTasks.length === 0 ? <Empty><span><CheckCircle2 size={24}/><br/>Tudo em ordem por aqui.</span></Empty> : <TaskList>{focusTasks.map(task => <TaskRow key={task.id} $late={task.overdue} onClick={() => navigate('/me/tasks')}><TaskStatus $late={task.overdue} $blocked={task.blocked}/><TaskCopy><span>{task.projectKey ? `${task.projectKey} · ` : ''}{task.boardName}{task.stageName ? ` / ${task.stageName}` : ''}</span><strong>#{task.number} · {task.title}</strong></TaskCopy><Due $late={task.overdue}><CalendarCheck size={12}/>{dueLabel(task.dueDate)}</Due></TaskRow>)}</TaskList>}
      </Panel>

      <Side>
        <Panel>
          <PanelHeader><FolderKanban size={16}/><div className="title"><h2>Projetos recentes</h2><p>Continue de onde parou.</p></div><button onClick={() => navigate('/projects')}>Todos<ArrowRight size={12}/></button></PanelHeader>
          {projectsQuery.isLoading ? <Empty>Carregando projetos...</Empty> : recentProjects.length === 0 ? <Empty>Nenhum projeto disponível.</Empty> : <ProjectList>{recentProjects.map(project => <ProjectRow key={project.id} onClick={() => navigate(`/projects/${project.id}/backlog`)}><span className="mark"><FolderKanban size={17}/></span><span><strong>{project.name}</strong><small>{project.boards.length} quadro(s) · {project.teams.length} equipe(s)</small></span><ArrowRight size={13}/></ProjectRow>)}</ProjectList>}
        </Panel>

        <Panel>
          <PanelHeader><LayoutDashboard size={16}/><div className="title"><h2>Acesso rápido</h2><p>Atalhos para os fluxos mais usados.</p></div></PanelHeader>
          <QuickGrid>
            <Quick onClick={() => navigate('/projects')}><FolderKanban size={17}/><strong>Projetos</strong><span>Backlog, sprints e quadros.</span></Quick>
            <Quick onClick={() => navigate('/reports')}><BarChart3 size={17}/><strong>Relatórios</strong><span>Indicadores e análises.</span></Quick>
            <Quick onClick={() => navigate('/requests')}><Inbox size={17}/><strong>Solicitações</strong><span>Demandas e atendimento.</span></Quick>
            <Quick onClick={() => navigate('/me/tasks')}><ListChecks size={17}/><strong>Meu trabalho</strong><span>Fila pessoal de atenção.</span></Quick>
          </QuickGrid>
        </Panel>
      </Side>
    </MainGrid>
  </HomePage>;
}
