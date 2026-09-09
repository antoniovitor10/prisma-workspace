import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom';
import { ThemeProvider } from 'styled-components';
import { api } from '../services/api';
import { OrganizationStateContext, type OrganizationSummary } from '../features/organizations/OrganizationState';
import { theme } from '../styles/theme';
import { ProjectSettings } from './ProjectSettings';
import { Projects, type ProjectSummary } from './Projects';

vi.mock('../features/workflow/ProjectWorkflowSettings', () => ({
  ProjectWorkflowSettings: () => null,
}));
vi.mock('../features/portal/ExternalPortalSettings', () => ({
  ExternalPortalSettings: () => null,
}));
vi.mock('../features/portal/ExternalPortalSettings', () => ({
  ExternalPortalSettings: () => null,
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

function renderWithProviders(children: React.ReactNode) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <ThemeProvider theme={theme}>
        <OrganizationStateContext.Provider value={{
          organizations: [organization],
          current: organization,
          switchOrganization: vi.fn(),
        }}>
          {children}
        </OrganizationStateContext.Provider>
      </ThemeProvider>
    </QueryClientProvider>,
  );
}

function ProjectContext() {
  return <Outlet context={{ project }} />;
}

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe('metodologia suspensa na interface', () => {
  it('não oferece metodologia no cadastro de projeto', async () => {
    vi.spyOn(api, 'getProjects').mockResolvedValue([]);

    renderWithProviders(
      <MemoryRouter initialEntries={['/projects']}>
        <Routes><Route path="/projects" element={<Projects />} /></Routes>
      </MemoryRouter>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Novo projeto' }));

    expect(screen.getByRole('combobox', { name: 'Natureza' })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Tipo de Trabalho' })).toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Estrutura de Trabalho' })).not.toBeInTheDocument();
    expect(screen.queryByText(/estrutura de trabalho/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/metodologia/i)).not.toBeInTheDocument();
  });

  it('não oferece metodologia nas configurações do projeto', () => {
    vi.spyOn(api, 'getOrganizationMembers').mockResolvedValue([]);
    vi.spyOn(api, 'getTags').mockResolvedValue([]);
    vi.spyOn(api, 'getTeams').mockResolvedValue([]);
    vi.spyOn(api, 'getProjectHistory').mockResolvedValue([]);

    renderWithProviders(
      <MemoryRouter initialEntries={['/projects/project-1/settings']}>
        <Routes>
          <Route path="/projects/:projectId" element={<ProjectContext />}>
            <Route path="settings" element={<ProjectSettings />} />
          </Route>
        </Routes>
      </MemoryRouter>,
    );

    expect(screen.getByRole('heading', { name: 'Dados do projeto' })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Natureza' })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Tipo de Trabalho' })).toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Estrutura de Trabalho' })).not.toBeInTheDocument();
    expect(screen.queryByText(/estrutura de trabalho/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/metodologia/i)).not.toBeInTheDocument();
  });
});
