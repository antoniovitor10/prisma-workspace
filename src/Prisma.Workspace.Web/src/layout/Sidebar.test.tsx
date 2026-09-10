import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { OrganizationStateContext, type OrganizationStateValue } from '../features/organizations/OrganizationState';
import { api } from '../services/api';
import { AppThemeProvider } from '../styles/ThemeMode';
import { Sidebar } from './Sidebar';

vi.mock('../preview', () => ({ previewMode: false }));

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  localStorage.clear();
});

beforeEach(() => {
  localStorage.clear();
});

const organizacao: OrganizationStateValue['organizations'][number] = {
  id: 'org-1', name: 'Organização Um', slug: 'org-um', isActive: true,
  locale: 'pt-BR', timeZone: 'America/Recife', weekStartDay: 1, role: 1, isAdministrator: true,
};

function renderSidebar(
  rotas: string[] = ['/projects'],
  current: OrganizationStateValue['organizations'][number] = organizacao,
) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const orgState: OrganizationStateValue = {
    organizations: [organizacao],
    current,
    switchOrganization: vi.fn(),
  };
  render(
    <MemoryRouter initialEntries={rotas}>
      <QueryClientProvider client={queryClient}>
        <AppThemeProvider>
          <OrganizationStateContext.Provider value={orgState}>
            <Sidebar />
          </OrganizationStateContext.Provider>
        </AppThemeProvider>
      </QueryClientProvider>
    </MemoryRouter>,
  );
}

const trilho = () => screen.findByRole('navigation', { name: 'Navegação lateral' });

describe('Sidebar', () => {
  it('lista a navegação para papel comum sem permissões extras', async () => {
    vi.spyOn(api, 'getOrganizationAccess').mockResolvedValue({ role: 1, allowedPermissions: [] });
    renderSidebar();

    const nav = await trilho();
    expect(within(nav).getByRole('link', { name: 'Início' })).toBeInTheDocument();
    expect(within(nav).getByRole('link', { name: 'Meu trabalho' })).toBeInTheDocument();
    expect(within(nav).getByRole('link', { name: 'Projetos' })).toBeInTheDocument();
    expect(within(nav).getByRole('link', { name: 'Solicitações' })).toBeInTheDocument();
    // Sem a permissão 10 nem perfil de leitura, relatórios não aparecem.
    expect(within(nav).queryByRole('link', { name: 'Relatórios' })).not.toBeInTheDocument();
  });

  it('marca o link Projetos como página atual na rota /projects', async () => {
    vi.spyOn(api, 'getOrganizationAccess').mockResolvedValue({ role: 1, allowedPermissions: [] });
    renderSidebar(['/projects']);

    const nav = await trilho();
    const link = within(nav).getByRole('link', { name: 'Projetos' });
    await waitFor(() => expect(link).toHaveAttribute('aria-current', 'page'));
  });

  it('oculta Início, Meu trabalho e Projetos para o solicitante externo (papel 8)', async () => {
    vi.spyOn(api, 'getOrganizationAccess').mockResolvedValue({ role: 8, allowedPermissions: [] });
    renderSidebar(['/requests'], { ...organizacao, role: 8 });

    const nav = await trilho();
    await waitFor(() =>
      expect(within(nav).queryByRole('link', { name: 'Início' })).not.toBeInTheDocument());
    expect(within(nav).queryByRole('link', { name: 'Meu trabalho' })).not.toBeInTheDocument();
    expect(within(nav).queryByRole('link', { name: 'Projetos' })).not.toBeInTheDocument();
    expect(within(nav).getByRole('link', { name: 'Solicitações' })).toBeInTheDocument();
  });

  it('mostra Relatórios com a permissão de visualizar relatório', async () => {
    vi.spyOn(api, 'getOrganizationAccess').mockResolvedValue({ role: 1, allowedPermissions: [10] });
    renderSidebar();

    const nav = await trilho();
    await waitFor(() =>
      expect(within(nav).getByRole('link', { name: 'Relatórios' })).toBeInTheDocument());
  });

  it('alterna entre recolhido e expandido e guarda a preferência', async () => {
    vi.spyOn(api, 'getOrganizationAccess').mockResolvedValue({ role: 1, allowedPermissions: [] });
    renderSidebar();

    const expandir = await screen.findByRole('button', { name: 'Expandir navegação' });
    expect(expandir).toHaveAttribute('aria-expanded', 'false');

    fireEvent.click(expandir);

    const recolher = await screen.findByRole('button', { name: 'Recolher navegação' });
    expect(recolher).toHaveAttribute('aria-expanded', 'true');
    await waitFor(() =>
      expect(localStorage.getItem('prisma_workspace_nav_expandido')).toBe('true'));
  });

  it('abre já expandido quando a preferência guardada diz isso', async () => {
    localStorage.setItem('prisma_workspace_nav_expandido', 'true');
    vi.spyOn(api, 'getOrganizationAccess').mockResolvedValue({ role: 1, allowedPermissions: [] });
    renderSidebar();

    expect(await screen.findByRole('button', { name: 'Recolher navegação' })).toBeInTheDocument();
  });
});
