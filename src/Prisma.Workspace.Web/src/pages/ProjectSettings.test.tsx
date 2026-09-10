import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom';
import { ThemeProvider } from 'styled-components';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { OrganizationStateContext, type OrganizationSummary } from '../features/organizations/OrganizationState';
import { api } from '../services/api';
import { theme } from '../styles/theme';
import { ProjectSettings } from './ProjectSettings';
import type { ProjectSummary } from './Projects';

vi.mock('../features/workflow/ProjectWorkflowSettings', () => ({
  ProjectWorkflowSettings: () => <section><h2>Fluxo do projeto</h2></section>,
}));
vi.mock('../features/portal/ExternalPortalSettings', () => ({
  ExternalPortalSettings: () => <section><h2>Portal do cidadão</h2></section>,
}));

const project: ProjectSummary = {
  id: 'project-1',
  key: 'KANBAN',
  name: 'Projeto Kanban',
  ownerId: 'user-1',
  methodology: 1,
  boards: [],
  teams: [],
  members: [{ userId: 'user-1', role: 5 }],
};

const organization: OrganizationSummary = {
  id: 'organization-1',
  name: 'Organização de teste',
  slug: 'organizacao-de-teste',
  isActive: true,
  locale: 'pt-BR',
  timeZone: 'America/Sao_Paulo',
  weekStartDay: 1,
  role: 1,
  isAdministrator: true,
};

function ProjectContext() {
  return <Outlet context={{ project }} />;
}

function renderSettings(rota = '/projects/project-1/settings') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <ThemeProvider theme={theme}>
        <OrganizationStateContext.Provider value={{
          organizations: [organization],
          current: organization,
          switchOrganization: vi.fn(),
        }}>
          <MemoryRouter initialEntries={[rota]}>
            <Routes>
              <Route path="/projects/:projectId" element={<ProjectContext />}>
                <Route path="settings" element={<ProjectSettings />} />
              </Route>
            </Routes>
          </MemoryRouter>
        </OrganizationStateContext.Provider>
      </ThemeProvider>
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.spyOn(api, 'getOrganizationMembers').mockResolvedValue([]);
  vi.spyOn(api, 'getTags').mockResolvedValue([]);
  vi.spyOn(api, 'getTeams').mockResolvedValue([]);
  vi.spyOn(api, 'getProjectHistory').mockResolvedValue([]);
});

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe('configurações do projeto por categoria', () => {
  it('abre em Geral e mostra apenas as seções dessa categoria', () => {
    renderSettings();

    expect(screen.getByRole('navigation', { name: 'Categorias de configuração' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Dados do projeto' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Ciclo de vida' })).toBeInTheDocument();
    // O que fazia a tela virar um rolo unico: todas as secoes juntas, sempre.
    expect(screen.queryByRole('heading', { name: 'Membros e papéis' })).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Campos personalizados' })).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Histórico do projeto' })).not.toBeInTheDocument();
  });

  it('troca de categoria ao escolher no menu', () => {
    renderSettings();

    fireEvent.click(screen.getByRole('button', { name: 'Pessoas e equipes' }));

    expect(screen.getByRole('heading', { name: 'Membros e papéis' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Equipes' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Dados do projeto' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Pessoas e equipes' })).toHaveAttribute('aria-current', 'true');
  });

  it('respeita a categoria vinda na URL', () => {
    renderSettings('/projects/project-1/settings?secao=historico');

    expect(screen.getByRole('heading', { name: 'Histórico do projeto' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Dados do projeto' })).not.toBeInTheDocument();
  });

  it('cai em Geral quando a URL traz categoria desconhecida', () => {
    renderSettings('/projects/project-1/settings?secao=inexistente');

    expect(screen.getByRole('heading', { name: 'Dados do projeto' })).toBeInTheDocument();
  });

  it('mantém fluxo de trabalho e portal externo em categorias próprias', () => {
    renderSettings('/projects/project-1/settings?secao=fluxo');
    expect(screen.getByRole('heading', { name: 'Fluxo do projeto' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Portal do cidadão' })).not.toBeInTheDocument();

    cleanup();

    renderSettings('/projects/project-1/settings?secao=portal');
    expect(screen.getByRole('heading', { name: 'Portal do cidadão' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Fluxo do projeto' })).not.toBeInTheDocument();
  });
});
