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
) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const switchOrganization = vi.fn();
  const orgState: OrganizationStateValue = {
    organizations,
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
  it('exibe navegação e controles principais para papel comum sem permissões extras', async () => {
    vi.spyOn(api, 'getOrganizationAccess').mockResolvedValue({ role: 1, allowedPermissions: [] });
    renderTopbar();

    const nav = await screen.findByRole('navigation', { name: 'Navegação principal' });
    expect(nav).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Início/ })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Meu trabalho/ })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Projetos/ })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Solicitações/ })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Configurações/ })).not.toBeInTheDocument();

    expect(screen.getByRole('button', { name: 'Novo item' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Abrir pesquisa global (Ctrl K)' })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Selecionar organização' })).toBeInTheDocument();
  });

  it('marca o link Projetos como página atual na rota /projects', async () => {
    vi.spyOn(api, 'getOrganizationAccess').mockResolvedValue({ role: 1, allowedPermissions: [] });
    renderTopbar(['/projects']);

    await screen.findAllByRole('navigation', { name: 'Navegação principal' });
    const nav = screen.getAllByRole('navigation', { name: 'Navegação principal' })[0];
    const projectsLink = within(nav).getByRole('link', { name: /Projetos/ });
    await waitFor(() => expect(projectsLink).toHaveAttribute('aria-current', 'page'));
  });

  it('oculta Meu trabalho e Projetos da navegação para papel 8', async () => {
    vi.spyOn(api, 'getOrganizationAccess').mockResolvedValue({ role: 8, allowedPermissions: [] });
    renderTopbar(['/requests'], { ...organizations[0], role: 8 });

    const nav = await screen.findByRole('navigation', { name: 'Navegação principal' });
    expect(within(nav).queryByRole('link', { name: /^Meu trabalho$/ })).not.toBeInTheDocument();
    expect(within(nav).queryByRole('link', { name: /^Início$/ })).not.toBeInTheDocument();
    expect(within(nav).queryByRole('link', { name: /^Projetos$/ })).not.toBeInTheDocument();
    expect(within(nav).getByRole('link', { name: /^Solicitações$/ })).toBeInTheDocument();
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
