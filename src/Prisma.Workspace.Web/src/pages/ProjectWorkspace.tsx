import { useQuery } from '@tanstack/react-query';
import { ChevronRight, FolderKanban } from 'lucide-react';
import { Link, NavLink, Outlet, useOutletContext, useParams } from 'react-router-dom';
import styled from 'styled-components';
import { ContextBarInjector } from '../layout/ContextBar';
import { projectNavEntries } from '../layout/navigation';
import { Kanban } from './Kanban';
import { BacklogPlanner } from '../features/scrum/BacklogPlanner';
import { SprintDashboard } from '../features/scrum/SprintDashboard';
import { ReportsHub } from '../features/reports/ReportsHub';
import { ProjectItemsQuery } from './ProjectItemsQuery';
import { previewMode, previewProject } from '../preview';
import { api } from '../services/api';
import type { ProjectSummary } from './Projects';

type WorkspaceContext = { project: ProjectSummary };

const Page = styled.div`min-height: 100%;`;

/* Em desktop o bloco nome/abas vive na lateral (D88). No mobile a lateral some —
   esta faixa horizontal preserva as abas sem reabrir o hambúrguer a cada troca. */
const MobileProjectNav = styled.header`
  display: none;
  padding: 12px 16px 0;
  border-bottom: 1px solid ${({ theme }) => theme.color.border};
  background: ${({ theme }) => theme.color.surface};

  @media (max-width: 768px) {
    display: block;
  }
`;

const MobileTitle = styled.div`
  display: flex;
  align-items: flex-start;
  gap: 10px;

  h1 { font-size: 18px; line-height: 1.25; }
  p { margin-top: 3px; color: ${({ theme }) => theme.color.textMuted}; font-size: 12.5px; }
  b {
    margin-top: 2px;
    padding: 3px 6px;
    border-radius: ${({ theme }) => theme.radius.sm};
    background: ${({ theme }) => `color-mix(in srgb, ${theme.color.accentBlue} 10%, ${theme.color.surface})`};
    color: ${({ theme }) => theme.color.accentBlue};
    font-size: 11px;
  }
`;

const MobileTabs = styled.nav`
  display: flex;
  gap: 2px;
  margin-top: 12px;
  overflow-x: auto;
`;

const MobileTab = styled(NavLink)`
  padding: 10px 12px;
  border-bottom: 2px solid transparent;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 13.5px;
  font-weight: 750;
  white-space: nowrap;
  &.active { border-color: ${({ theme }) => theme.color.brand}; color: ${({ theme }) => theme.color.brand}; }
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

      <MobileProjectNav>
        <MobileTitle>
          <div>
            <h1>{project.name}</h1>
            <p>{project.description || 'Workspace integrado do projeto.'}</p>
          </div>
          <b>{project.key}</b>
        </MobileTitle>
        <MobileTabs aria-label="Áreas do projeto">
          {projectNavEntries.map(({ segment, label, end }) => (
            <MobileTab key={segment} to={segment} end={end}>{label}</MobileTab>
          ))}
        </MobileTabs>
      </MobileProjectNav>

      <Outlet context={{ project } satisfies WorkspaceContext} />
    </Page>
  );
}

/**
 * Alternância Backlog ↔ Quadro na barra de contexto. Fonte única: as duas visões
 * injetam o mesmo componente, em vez de repetir a marcação em cada uma.
 */
function ProjectViewSwitcher({ projectId }: { projectId: string }) {
  return (
    <ContextBarInjector>
      <ViewSwitcherWrap aria-label="Alternar visão do projeto">
        <ViewBtn to={`/projects/${projectId}/backlog`} end>Backlog</ViewBtn>
        <ViewBtn to={`/projects/${projectId}/boards`}>Quadro</ViewBtn>
      </ViewSwitcherWrap>
    </ContextBarInjector>
  );
}

export function ProjectBacklog() {
  const { project } = useOutletContext<WorkspaceContext>();
  return (
    <>
      <ProjectViewSwitcher projectId={project.id} />
      <BacklogPlanner project={project} />
    </>
  );
}

/**
 * Aba Kanban do projeto: abre o quadro direto, sem tela intermediária de escolha. O
 * fluxo é do projeto (D83), e criar ou excluir quadro vive dentro do próprio Kanban.
 */
export function ProjectBoardView() {
  const { project } = useOutletContext<WorkspaceContext>();
  return (
    <>
      <ProjectViewSwitcher projectId={project.id} />
      <Kanban />
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

const Card = styled.article`
  padding: 16px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};
  box-shadow: ${({ theme }) => theme.shadow.sm};
  h3 { margin-top: 9px; font-size: 15px; }
  p { min-height: 34px; margin: 7px 0 13px; color: ${({ theme }) => theme.color.textMuted}; font-size: 13px; line-height: 1.5; }
`;

export function ProjectReports() {
  const { project } = useOutletContext<WorkspaceContext>();
  return <Content><Toolbar><div><h2>Relatórios do projeto</h2><p>Dashboard, relatórios prontos e análises personalizadas.</p></div></Toolbar><ReportsHub fixedProjectId={project.id} /></Content>;
}

export function ProjectPlaceholder() {
  return <Content><Card><h3>Configurações do projeto</h3><p>Membros, papéis, times e regras do fluxo ficam centralizados nesta área.</p></Card></Content>;
}
