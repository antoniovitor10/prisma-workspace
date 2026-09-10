import { useQuery } from '@tanstack/react-query';
import { NavLink, useLocation } from 'react-router-dom';
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

const Painel = styled.nav`
  position: sticky;
  top: 0;
  z-index: 25;
  display: flex;
  flex-direction: column;
  flex: 0 0 auto;
  width: ${LARGURA};
  height: 100vh;
  padding: 16px 14px 18px;
  border-right: 1px solid ${({ theme }) => theme.color.border};
  background: ${({ theme }) => theme.color.surface};
  overflow: auto;

  @media (max-width: 768px) {
    display: none;
  }
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

  h2 {
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
  color: ${({ theme }) => theme.color.accentBlue};
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

const Abas = styled.nav`
  display: flex;
  flex-direction: column;
  gap: 2px;
`;

const Aba = styled(NavLink)`
  display: flex;
  align-items: center;
  min-height: 36px;
  padding: 0 10px;
  border-radius: ${({ theme }) => theme.radius.md};
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 13.5px;
  font-weight: 750;
  text-decoration: none;

  &:hover {
    background: ${({ theme }) => theme.color.neutral[100]};
    color: ${({ theme }) => theme.color.text};
  }

  &:focus-visible {
    outline: 2px solid ${({ theme }) => theme.color.accentBlue};
    outline-offset: 1px;
  }

  &.active {
    background: ${({ theme }) => `color-mix(in srgb, ${theme.color.brand} 10%, ${theme.color.surface})`};
    color: ${({ theme }) => theme.color.brand};
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

  return (
    <Painel aria-label="Navegação do projeto">
      {isLoading && <Placeholder>Carregando projeto...</Placeholder>}
      {!isLoading && !project && <Placeholder>Projeto indisponível.</Placeholder>}
      {project && (
        <>
          <Cabecalho>
            <TituloLinha>
              <h2>{project.name}</h2>
              <Chave>{project.key}</Chave>
            </TituloLinha>
            <Descricao>
              {project.description || 'Workspace integrado do projeto.'}
            </Descricao>
          </Cabecalho>

          <Abas aria-label="Áreas do projeto">
            {projectNavEntries.map(({ segment, label, end }) => (
              <Aba
                key={segment}
                to={`/projects/${project.id}/${segment}`}
                end={end}
              >
                {label}
              </Aba>
            ))}
          </Abas>
        </>
      )}
    </Painel>
  );
}
