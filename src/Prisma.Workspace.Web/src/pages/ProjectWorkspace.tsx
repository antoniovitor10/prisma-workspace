import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ChevronRight, CircleDot, FolderKanban, Gauge, Plus } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { Link, NavLink, Outlet, useNavigate, useOutletContext, useParams } from 'react-router-dom';
import styled from 'styled-components';
import { ContextBarInjector } from '../layout/ContextBar';
import { BacklogPlanner } from '../features/scrum/BacklogPlanner';
import { SprintDashboard } from '../features/scrum/SprintDashboard';
import { ReportsHub } from '../features/reports/ReportsHub';
import { ProjectItemsQuery } from './ProjectItemsQuery';
import { previewMode, previewProject } from '../preview';
import { api } from '../services/api';
import type { ProjectSummary } from './Projects';

type WorkspaceContext = { project: ProjectSummary };

const Page = styled.div`min-height: 100%;`;

const Header = styled.header`
  padding: 14px 28px 0;
  border-bottom: 1px solid ${({ theme }) => theme.color.border};
  background: ${({ theme }) => theme.color.surface};

  @media (max-width: 760px) { padding-inline: 16px; }
`;

/* breadcrumb row rendered inside ContextBar via portal */
const CrumbLink = styled(Link)`
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 13px;
  font-weight: 700;
  text-decoration: none;
  &:hover { color: ${({ theme }) => theme.color.brand}; }
`;

const CrumbSep = styled(ChevronRight)`
  color: ${({ theme }) => theme.color.neutral[400]};
`;

const CrumbCurrent = styled.span`
  color: ${({ theme }) => theme.color.text};
  font-size: 13px;
  font-weight: 700;
  max-width: 240px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
`;

/* compact view-switcher (Backlog ↔ Quadro) injected when on those routes */
const ViewSwitcherWrap = styled.nav`
  display: flex;
  align-items: center;
  gap: 2px;
  margin-left: 6px;
  padding-left: 10px;
  border-left: 1px solid ${({ theme }) => theme.color.border};
`;

const ViewBtn = styled(NavLink)`
  display: flex;
  align-items: center;
  gap: 5px;
  height: 28px;
  padding: 0 9px;
  border-radius: ${({ theme }) => theme.radius.md};
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 12.5px;
  font-weight: 750;
  text-decoration: none;
  white-space: nowrap;
  &:hover { background: ${({ theme }) => theme.color.neutral[100]}; color: ${({ theme }) => theme.color.text}; }
  &.active {
    background: ${({ theme }) => `color-mix(in srgb, ${theme.color.brand} 10%, transparent)`};
    color: ${({ theme }) => theme.color.brand};
    font-weight: 800;
  }
`;

const Title = styled.div`
  display: flex;
  align-items: flex-start;
  gap: 12px;
  margin-top: 9px;

  h1 { font-size: 21px; line-height: 1.2; }
  p { margin-top: 4px; color: ${({ theme }) => theme.color.textMuted}; font-size: 13px; }
  b {
    margin-top: 2px;
    padding: 3px 6px;
    border-radius: ${({ theme }) => theme.radius.sm};
    background: ${({ theme }) => `color-mix(in srgb, ${theme.color.accentBlue} 10%, ${theme.color.surface})`};
    color: ${({ theme }) => theme.color.accentBlue};
    font-size: 12px;
  }
`;

const Tabs = styled.nav`
  display: flex;
  gap: 2px;
  margin-top: 15px;
  overflow-x: auto;
`;

const Tab = styled(NavLink)`
  padding: 10px 12px;
  border-bottom: 2px solid transparent;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 13.5px;
  font-weight: 750;
  white-space: nowrap;
  &.active { border-color: ${({ theme }) => theme.color.brand}; color: ${({ theme }) => theme.color.brand}; }
`;

const Loading = styled.div`
  display: grid;
  min-height: 280px;
  place-items: center;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 14px;
`;

export function ProjectWorkspace() {
  const { projectId = '' } = useParams();
  const { data: project, isLoading } = useQuery<ProjectSummary>({
    queryKey: ['project', projectId],
    retry: false,
    queryFn: async () => {
      try { return await api.getProject(projectId); }
      catch { if (previewMode) return previewProject; throw new Error('Projeto não encontrado.'); }
    },
  });

  if (isLoading) return <Loading>Carregando projeto...</Loading>;
  if (!project) return <Loading>Não foi possível abrir este projeto.</Loading>;

  return (
    <Page>
      {/* inject breadcrumbs into the global ContextBar via portal */}
      <ContextBarInjector>
        <CrumbLink to="/projects"><FolderKanban size={13} /> Projetos</CrumbLink>
        <CrumbSep size={11} />
        <CrumbCurrent aria-current="page">{project.name}</CrumbCurrent>
      </ContextBarInjector>

      <Header>
        <Title><div><h1>{project.name}</h1><p>{project.description || 'Workspace integrado do projeto.'}</p></div><b>{project.key}</b></Title>
        <Tabs>
          <Tab to="items">Itens</Tab>
          <Tab to="backlog">Backlog</Tab>
          <Tab to="sprints">Sprints</Tab>
          <Tab to="boards">Kanban</Tab>
          <Tab to="reports">Relatórios</Tab>
          <Tab to="wiki">Wiki</Tab>
          <Tab to="settings">Configurações</Tab>
        </Tabs>
      </Header>
      <Outlet context={{ project } satisfies WorkspaceContext} />
    </Page>
  );
}

export function ProjectBacklog() {
  const { project } = useOutletContext<WorkspaceContext>();
  return (
    <>
      <ContextBarInjector>
        <ViewSwitcherWrap aria-label="Alternar visão do projeto">
          <ViewBtn to={`/projects/${project.id}/backlog`} end>Backlog</ViewBtn>
          <ViewBtn to={`/projects/${project.id}/boards`}>Quadro</ViewBtn>
        </ViewSwitcherWrap>
      </ContextBarInjector>
      <BacklogPlanner project={project} />
    </>
  );
}

export function ProjectItems() {
  const { project } = useOutletContext<WorkspaceContext>();
  return <ProjectItemsQuery project={project} />;
}

export function ProjectSprints() {
  const { project } = useOutletContext<WorkspaceContext>();
  return <SprintDashboard project={project} />;
}

const Content = styled.section`
  padding: 20px 28px 32px;
  @media (max-width: 760px) { padding: 16px; }
`;

const Toolbar = styled.div`
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 14px;
  h2 { font-size: 18px; }
  p { margin-top: 4px; color: ${({ theme }) => theme.color.textMuted}; font-size: 13.5px; }
`;

const Primary = styled.button`
  display: inline-flex;
  min-height: 36px;
  align-items: center;
  gap: 6px;
  padding: 0 12px;
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.brand};
  color: ${({ theme }) => theme.color.onBrand};
  font-size: 13.5px;
  font-weight: 800;
`;

const BoardCreator = styled.form`
  display: grid;
  grid-template-columns: minmax(220px, 1fr) minmax(180px, .65fr) auto auto;
  gap: 8px;
  margin: -3px 0 14px;
  padding: 12px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};

  input,
  select {
    min-height: 36px;
    padding: 0 10px;
    border: 1px solid ${({ theme }) => theme.color.border};
    border-radius: ${({ theme }) => theme.radius.md};
    background: ${({ theme }) => theme.color.surface};
    color: ${({ theme }) => theme.color.text};
    font: inherit;
    font-size: 13.5px;
  }

  p {
    grid-column: 1 / -1;
    color: ${({ theme }) => theme.color.danger};
    font-size: 13px;
  }

  @media (max-width: 760px) { grid-template-columns: 1fr; }
`;

const Secondary = styled.button`
  min-height: 36px;
  padding: 0 12px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  color: ${({ theme }) => theme.color.text};
  font-size: 13.5px;
  font-weight: 750;
`;

const Cards = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 11px;
`;

const Card = styled.article`
  padding: 16px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};
  box-shadow: ${({ theme }) => theme.shadow.sm};
  h3 { margin-top: 9px; font-size: 15px; }
  p { min-height: 34px; margin: 7px 0 13px; color: ${({ theme }) => theme.color.textMuted}; font-size: 13px; line-height: 1.5; }
`;

const BoardCard = styled.button`
  padding: 16px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};
  color: ${({ theme }) => theme.color.text};
  text-align: left;
  box-shadow: ${({ theme }) => theme.shadow.sm};
  &:hover { border-color: ${({ theme }) => theme.color.accentBlue}; }
  h3 { margin-top: 9px; font-size: 15px; }
  p { min-height: 34px; margin: 7px 0 13px; color: ${({ theme }) => theme.color.textMuted}; font-size: 13px; line-height: 1.5; }
`;

const CardMeta = styled.div`
  display: grid;
  gap: 6px;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 12px;
  span { display: flex; align-items: center; gap: 5px; }
`;

export function ProjectBoards() {
  const { project } = useOutletContext<WorkspaceContext>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [creating, setCreating] = useState(false);
  const [boardName, setBoardName] = useState('');
  const [teamId, setTeamId] = useState('');
  const createBoard = useMutation({
    mutationFn: () => previewMode
      ? Promise.resolve(`preview-board-${Date.now()}`)
      : api.createBoard(boardName.trim(), project.id, teamId || undefined),
    onSuccess: async (boardId: string) => {
      setBoardName('');
      setTeamId('');
      setCreating(false);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['project', project.id] }),
        queryClient.invalidateQueries({ queryKey: ['projects'] }),
      ]);
      navigate(`/boards/${boardId}`);
    },
  });
  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (boardName.trim()) createBoard.mutate();
  };

  return (
    <>
      <ContextBarInjector>
        <ViewSwitcherWrap aria-label="Alternar visão do projeto">
          <ViewBtn to={`/projects/${project.id}/backlog`} end>Backlog</ViewBtn>
          <ViewBtn to={`/projects/${project.id}/boards`}>Quadro</ViewBtn>
        </ViewSwitcherWrap>
      </ContextBarInjector>
      <Content>
        <Toolbar><div><h2>Quadros Kanban</h2><p>Fluxos, limites de trabalho em andamento e itens do projeto.</p></div><Primary type="button" aria-expanded={creating} onClick={() => setCreating(current => !current)}><Plus size={14} />Novo quadro</Primary></Toolbar>
        {creating && <BoardCreator onSubmit={submit}>
          <input autoFocus required maxLength={200} aria-label="Nome do quadro" placeholder="Nome do quadro" value={boardName} onChange={event => setBoardName(event.target.value)} />
          <select aria-label="Equipe do quadro" value={teamId} onChange={event => setTeamId(event.target.value)}>
            <option value="">Sem equipe específica</option>
            {project.teams.map(team => <option key={team.id} value={team.id}>{team.name}</option>)}
          </select>
          <Secondary type="button" onClick={() => { setCreating(false); setBoardName(''); setTeamId(''); }}>Cancelar</Secondary>
          <Primary type="submit" disabled={!boardName.trim() || createBoard.isPending}>{createBoard.isPending ? 'Criando...' : 'Criar quadro'}</Primary>
          {createBoard.error && <p role="alert">{(createBoard.error as Error).message}</p>}
        </BoardCreator>}
        <Cards>
          {project.boards.length === 0 && <Card><FolderKanban size={18} /><h3>Nenhum quadro</h3><p>Crie o primeiro quadro para configurar colunas, status e limites de WIP.</p></Card>}
          {project.boards.map((board) => (
            <BoardCard key={board.id} onClick={() => navigate(`/boards/${board.id}`)}>
              <FolderKanban size={18} /><h3>{board.name}</h3><p>Visualize as etapas e mova o trabalho de forma fluida.</p>
              <CardMeta><span><CircleDot size={12} />Fluxo Kanban</span><span><Gauge size={12} />Lead time e limites WIP</span></CardMeta>
            </BoardCard>
          ))}
        </Cards>
      </Content>
    </>
  );
}

export function ProjectReports() {
  const { project } = useOutletContext<WorkspaceContext>();
  return <Content><Toolbar><div><h2>Relatórios do projeto</h2><p>Dashboard, relatórios prontos e análises personalizadas.</p></div></Toolbar><ReportsHub fixedProjectId={project.id} /></Content>;
}

export function ProjectPlaceholder() {
  return <Content><Card><h3>Configurações do projeto</h3><p>Membros, papéis, times e regras do fluxo ficam centralizados nesta área.</p></Card></Content>;
}
