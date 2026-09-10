import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '../services/api';
import { AppThemeProvider } from '../styles/ThemeMode';
import { OrganizationStateContext, type OrganizationStateValue } from '../features/organizations/OrganizationState';
import { Topbar } from './Topbar';

vi.mock('../preview', () => ({ previewMode: false }));
vi.mock('../components/GlobalActions', () => ({
  GlobalSearchDialog: () => null,
  QuickCreateDialog: () => null,
}));
vi.mock('../features/notifications/NotificationCenter', () => ({
  NotificationCenter: () => null,
}));

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

beforeEach(() => {
  vi.spyOn(api, 'getMyActiveTimer').mockResolvedValue(null);
  vi.spyOn(api, 'logout').mockResolvedValue(undefined as never);
});

const organizations: OrganizationStateValue['organizations'] = [
  {
    id: 'org-1', name: 'Organização Um', slug: 'org-um', isActive: true,
    locale: 'pt-BR', timeZone: 'America/Recife', weekStartDay: 1, role: 1, isAdministrator: true,
  },
  {
    id: 'org-2', name: 'Organização Dois', slug: 'org-dois', isActive: true,
    locale: 'pt-BR', timeZone: 'America/Recife', weekStartDay: 1, role: 1, isAdministrator: false,
  },
];

function renderTopbar(
  initialEntries: string[] = ['/projects'],
  current: OrganizationStateValue['organizations'][number] = organizations[0],
  lista: OrganizationStateValue['organizations'] = organizations,
) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const switchOrganization = vi.fn();
  const orgState: OrganizationStateValue = {
    organizations: lista,
    current,
    switchOrganization,
  };
  render(
    <MemoryRouter initialEntries={initialEntries}>
      <QueryClientProvider client={queryClient}>
        <AppThemeProvider>
          <OrganizationStateContext.Provider value={orgState}>
            <Topbar />
          </OrganizationStateContext.Provider>
        </AppThemeProvider>
      </QueryClientProvider>
    </MemoryRouter>,
  );
  return { switchOrganization };
}

describe('Topbar', () => {
  it('exibe a navegação principal e os controles globais no cabeçalho', async () => {
    vi.spyOn(api, 'getOrganizationAccess').mockResolvedValue({ role: 1, allowedPermissions: [] });
    renderTopbar();

    // D88: a navegação global voltou ao cabeçalho; a lateral é só contexto de projeto.
    const nav = await screen.findByRole('navigation', { name: 'Navegação principal' });
    expect(within(nav).getByRole('link', { name: 'Início' })).toBeInTheDocument();
    expect(within(nav).getByRole('link', { name: 'Projetos' })).toBeInTheDocument();
    expect(within(nav).getByRole('link', { name: 'Solicitações' })).toBeInTheDocument();
    expect(within(nav).queryByRole('link', { name: 'Relatórios' })).not.toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'Novo item' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Abrir pesquisa global (Ctrl K)' })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Selecionar organização' })).toBeInTheDocument();
  });

  it('mostra Relatórios no cabeçalho com a permissão de visualizar relatório', async () => {
    vi.spyOn(api, 'getOrganizationAccess').mockResolvedValue({ role: 1, allowedPermissions: [10] });
    renderTopbar();

    const nav = await screen.findByRole('navigation', { name: 'Navegação principal' });
    await waitFor(() =>
      expect(within(nav).getByRole('link', { name: 'Relatórios' })).toBeInTheDocument());
  });

  it('abre o menu mobile e fecha com Escape devolvendo o foco ao hambúrguer', async () => {
    vi.spyOn(api, 'getOrganizationAccess').mockResolvedValue({ role: 1, allowedPermissions: [] });
    renderTopbar();

    const hamburger = document.querySelector('button[aria-label="Abrir menu"]') as HTMLButtonElement | null;
    expect(hamburger).toBeTruthy();
    fireEvent.click(hamburger!);

    const dialog = await screen.findByRole('dialog', { name: 'Menu de navegação', hidden: true });
    expect(dialog).toBeInTheDocument();

    fireEvent.keyDown(window, { key: 'Escape' });

    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Menu de navegação', hidden: true })).not.toBeInTheDocument());
    await waitFor(() => expect(document.activeElement).toBe(hamburger));
  });
});
