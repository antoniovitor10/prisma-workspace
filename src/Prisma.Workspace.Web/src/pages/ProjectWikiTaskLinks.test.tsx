import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ThemeProvider } from 'styled-components';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { api } from '../services/api';
import { theme } from '../styles/theme';
import { WikiTaskLinks } from './ProjectWiki';

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

function renderLinks() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <MemoryRouter>
      <QueryClientProvider client={client}>
        <ThemeProvider theme={theme}>
          <WikiTaskLinks projectId="project-1" pageId="page-1" canEdit />
        </ThemeProvider>
      </QueryClientProvider>
    </MemoryRouter>,
  );
}

describe('vínculos de tarefas na Wiki', () => {
  it('pesquisa pelo título e vincula a tarefa selecionada', async () => {
    vi.spyOn(api, 'getWikiPageLinks').mockResolvedValue([]);
    const search = vi.spyOn(api, 'searchWorkItems').mockResolvedValue([{
      id: 'task-42', projectId: 'project-1', projectKey: 'PRD', number: 42,
      title: 'Documentar processo de compra', status: 'Em andamento',
    }]);
    const link = vi.spyOn(api, 'linkWikiTask').mockResolvedValue({
      workItemId: 'task-42', number: 42, title: 'Documentar processo de compra', reference: 'PRD-42',
    });
    renderLinks();

    const input = screen.getByRole('combobox', { name: 'Buscar tarefa para vincular à Wiki' });
    fireEvent.change(input, { target: { value: 'processo de compra' } });
    fireEvent.click(await screen.findByRole('button', { name: /PRD-42.*Documentar processo de compra/ }));
    fireEvent.click(screen.getByRole('button', { name: 'Vincular' }));

    await waitFor(() => expect(link).toHaveBeenCalledWith('project-1', 'page-1', 42));
    expect(search).toHaveBeenCalledWith('project-1', 'processo de compra');
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Buscar tarefa para vincular à Wiki' })).toHaveValue(''));
  });

  it('não oferece o campo de vínculo para quem possui somente leitura', async () => {
    vi.spyOn(api, 'getWikiPageLinks').mockResolvedValue([]);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <MemoryRouter>
        <QueryClientProvider client={client}>
          <ThemeProvider theme={theme}>
            <WikiTaskLinks projectId="project-1" pageId="page-1" canEdit={false} />
          </ThemeProvider>
        </QueryClientProvider>
      </MemoryRouter>,
    );

    expect(screen.queryByRole('combobox', { name: 'Buscar tarefa para vincular à Wiki' })).not.toBeInTheDocument();
  });
});
