import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { api } from '../services/api';
import { AppThemeProvider } from '../styles/ThemeMode';
import { Sidebar } from './Sidebar';

vi.mock('../preview', () => ({
  previewMode: false,
  previewProject: {
    id: 'proj-1', key: 'DEMO', name: 'Produto Demo',
    description: 'Exemplo genérico.', boards: [], teams: [],
  },
}));

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

const projeto = {
  id: 'proj-1',
  key: 'DEMO',
  name: 'Produto Demo',
  description: 'Exemplo genérico para explorar planejamento e execução de trabalho.',
  boards: [],
  teams: [],
};

function renderSidebar(rota = '/projects/proj-1/items') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <MemoryRouter initialEntries={[rota]}>
      <QueryClientProvider client={queryClient}>
        <AppThemeProvider>
          <Routes>
            <Route path="/projects/:projectId/*" element={<Sidebar />} />
            <Route path="*" element={<Sidebar />} />
          </Routes>
        </AppThemeProvider>
      </QueryClientProvider>
    </MemoryRouter>,
  );
}

describe('Sidebar', () => {
  it('não renderiza fora do projeto', () => {
    vi.spyOn(api, 'getProject').mockResolvedValue(projeto as never);
    renderSidebar('/projects');
    expect(screen.queryByRole('navigation', { name: 'Navegação do projeto' })).not.toBeInTheDocument();
  });

  it('mostra nome, chave, descrição e abas dentro do projeto', async () => {
    vi.spyOn(api, 'getProject').mockResolvedValue(projeto as never);
    renderSidebar('/projects/proj-1/items');

    expect(await screen.findByRole('heading', { name: 'Produto Demo' })).toBeInTheDocument();
    const nav = screen.getByRole('navigation', { name: 'Navegação do projeto' });
    expect(within(nav).getByText('DEMO')).toBeInTheDocument();
    expect(within(nav).getByText(/Exemplo genérico/)).toBeInTheDocument();

    const areas = within(nav).getByRole('navigation', { name: 'Áreas do projeto' });
    expect(within(areas).getByRole('link', { name: 'Itens' })).toHaveAttribute('aria-current', 'page');
    expect(within(areas).getByRole('link', { name: 'Backlog' })).toBeInTheDocument();
    expect(within(areas).getByRole('link', { name: 'Kanban' })).toBeInTheDocument();
    expect(within(areas).getByRole('link', { name: 'Configurações' })).toBeInTheDocument();
  });

  it('marca a aba ativa conforme a rota', async () => {
    vi.spyOn(api, 'getProject').mockResolvedValue(projeto as never);
    renderSidebar('/projects/proj-1/boards');

    const areas = await screen.findByRole('navigation', { name: 'Áreas do projeto' });
    await waitFor(() =>
      expect(within(areas).getByRole('link', { name: 'Kanban' })).toHaveAttribute('aria-current', 'page'));
  });
});
