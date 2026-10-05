import { useQuery } from '@tanstack/react-query';
import {
  BarChart3, BookOpen, CalendarRange, ChevronLeft, ChevronRight, Columns3,
  ListChecks, ListTree, Settings, type LucideIcon,
} from 'lucide-react';
import { NavLink, useLocation } from 'react-router-dom';
import { useState } from 'react';
import styled from 'styled-components';
import { previewMode, previewProject } from '../preview';
import type { ProjectSummary } from '../pages/Projects';
import { api } from '../services/api';
import { projectNavEntries } from './navigation';

/** Extrai o id do projeto da URL. O Sidebar vive no AppShell (pai das rotas), então
 *  não depende só de useParams da rota filha. */
const projectIdFromPath = (pathname: string) =>
  pathname.match(/^\/projects\/([^/]+)/)?.[1];

/**
 * Painel contextual do projeto (D88).
 *
 * Só aparece dentro de `/projects/:projectId`. Fora do projeto a navegação global vive
 * no cabeçalho — esta lateral não é trilho global. Em telas estreitas some: as abas do
 * projeto ficam na faixa horizontal do workspace, e o hambúrguer cobre o restante.
 */

const LARGURA = '260px';
const LARGURA_RECOLHIDA = '64px';
const STORAGE_KEY = 'project-context-menu-collapsed';

const projectIcons: Record<string, LucideIcon> = {
  items: ListChecks,
  backlog: ListTree,
  sprints: CalendarRange,
  boards: Columns3,
  reports: BarChart3,
  wiki: BookOpen,
  settings: Settings,
};

const Painel = styled.nav<{ $collapsed: boolean }>`
  position: sticky;
  top: 0;
  z-index: 25;
  display: flex;
  flex-direction: column;
  flex: 0 0 auto;
  width: ${({ $collapsed }) => $collapsed ? LARGURA_RECOLHIDA : LARGURA};
  height: 100vh;
  padding: ${({ $collapsed }) => $collapsed ? '12px 8px 18px' : '12px 14px 18px'};
  border-right: 1px solid ${({ theme }) => theme.color.border};
  background: ${({ theme }) => theme.color.surface};
  overflow: hidden auto;
  transition: width .18s ease, padding .18s ease;

  @media (max-width: 768px) {
    display: none;
  }
`;

const CollapseButton = styled.button<{ $collapsed: boolean }>`
  display: inline-flex;
  width: ${({ $collapsed }) => $collapsed ? '100%' : 'auto'};
  min-height: 32px;
  align-items: center;
  justify-content: center;
  align-self: ${({ $collapsed }) => $collapsed ? 'stretch' : 'flex-end'};
  gap: 6px;
  margin: 0 0 12px;
  padding: 0 8px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.surface};
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 12px;
  font-weight: 750;
  white-space: nowrap;
  &:hover { border-color: ${({ theme }) => theme.color.accentBlue}; color: ${({ theme }) => theme.color.text}; }
  &:focus-visible { outline: 2px solid ${({ theme }) => theme.color.accentBlueAccessible}; outline-offset: 1px; }
`;

const Cabecalho = styled.div`
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-bottom: 14px;
  padding-bottom: 14px;
  border-bottom: 1px solid ${({ theme }) => theme.color.border};
`;

const TituloLinha = styled.div`
  display: flex;
  align-items: flex-start;
  gap: 8px;
  min-width: 0;

  h1 {
    min-width: 0;
    margin: 0;
    color: ${({ theme }) => theme.color.text};
    font-size: 16px;
    font-weight: 800;
    line-height: 1.25;
    word-break: break-word;
  }
`;

const Chave = styled.b`
  flex: 0 0 auto;
  margin-top: 2px;
  padding: 3px 6px;
  border-radius: ${({ theme }) => theme.radius.sm};
  background: ${({ theme }) => `color-mix(in srgb, ${theme.color.accentBlue} 10%, ${theme.color.surface})`};
  color: ${({ theme }) => theme.color.accentBlueAccessible};
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 0.02em;
`;

const Descricao = styled.p`
  margin: 0;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 12.5px;
  line-height: 1.45;
`;

const ProjectMark = styled.div`
  display: grid;
  width: 38px;
  height: 38px;
  margin: 0 auto;
  place-items: center;
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => `color-mix(in srgb, ${theme.color.accentBlue} 12%, ${theme.color.surface})`};
  color: ${({ theme }) => theme.color.accentBlueAccessible};
  font-size: 11px;
  font-weight: 850;
  letter-spacing: .04em;
`;

const Abas = styled.nav`
  display: flex;
  flex-direction: column;
  gap: 2px;
`;

const Aba = styled(NavLink)<{ $collapsed: boolean }>`
  display: flex;
  align-items: center;
  min-height: 36px;
  justify-content: ${({ $collapsed }) => $collapsed ? 'center' : 'flex-start'};
  gap: 8px;
  padding: ${({ $collapsed }) => $collapsed ? '0' : '0 10px'};
  border-radius: ${({ theme }) => theme.radius.md};
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 13.5px;
  font-weight: 750;
  text-decoration: none;

  > svg { flex: 0 0 auto; }
  > span { display: ${({ $collapsed }) => $collapsed ? 'none' : 'inline'}; }

  &:hover {
    background: ${({ theme }) => theme.color.neutral[100]};
    color: ${({ theme }) => theme.color.text};
  }

  &:focus-visible {
    outline: 2px solid ${({ theme }) => theme.color.accentBlueAccessible};
    outline-offset: 1px;
  }

  &.active {
    background: ${({ theme }) => `color-mix(in srgb, ${theme.color.brand} 10%, ${theme.color.surface})`};
    color: ${({ theme }) => theme.color.brandAccessible};
    font-weight: 800;
  }
`;

const Placeholder = styled.div`
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 13px;
`;

export function Sidebar() {
  const { pathname } = useLocation();
  const projectId = projectIdFromPath(pathname);
  const [collapsed, setCollapsed] = useState(() => {
    try { return localStorage.getItem(STORAGE_KEY) === 'true'; }
    catch { return false; }
  });

  const { data: project, isLoading } = useQuery<ProjectSummary>({
    queryKey: ['project', projectId],
    enabled: Boolean(projectId),
    retry: false,
    queryFn: async () => {
      try {
        return await api.getProject(projectId!);
      } catch {
        if (previewMode) return previewProject as ProjectSummary;
        throw new Error('Projeto não encontrado.');
      }
    },
  });

  // Fora do projeto a lateral não existe — navegação global está no cabeçalho (D88).
  if (!projectId) return null;

  const toggleCollapsed = () => setCollapsed(current => {
    const next = !current;
    try { localStorage.setItem(STORAGE_KEY, String(next)); } catch { /* armazenamento indisponível */ }
    return next;
  });

  return (
    <Painel aria-label="Navegação do projeto" $collapsed={collapsed} data-collapsed={collapsed}>
      <CollapseButton type="button" $collapsed={collapsed} onClick={toggleCollapsed} aria-label={collapsed ? 'Expandir menu do projeto' : 'Recolher menu do projeto'} title={collapsed ? 'Expandir menu' : 'Recolher menu'}>
        {collapsed ? <ChevronRight size={15} /> : <><ChevronLeft size={15} /><span>Recolher</span></>}
      </CollapseButton>
      {isLoading && <Placeholder>Carregando projeto...</Placeholder>}
      {!isLoading && !project && <Placeholder>Projeto indisponível.</Placeholder>}
      {project && (
        <>
          <Cabecalho>
            {collapsed ? <ProjectMark title={`${project.key} · ${project.name}`}>{project.key.slice(0, 3)}</ProjectMark> : <>
              <TituloLinha>
                <h1>{project.name}</h1>
                <Chave>{project.key}</Chave>
              </TituloLinha>
              <Descricao>
                {project.description || 'Workspace integrado do projeto.'}
              </Descricao>
            </>}
          </Cabecalho>

          <Abas aria-label="Áreas do projeto">
            {projectNavEntries.map(({ segment, label, end }) => {
              const Icon = projectIcons[segment] ?? ListChecks;
              return (
              <Aba
                key={segment}
                to={`/projects/${project.id}/${segment}`}
                end={end}
                $collapsed={collapsed}
                aria-label={label}
                title={collapsed ? label : undefined}
              >
                <Icon size={15} />
                <span>{label}</span>
              </Aba>
            );})}
          </Abas>
        </>
      )}
    </Painel>
  );
}
