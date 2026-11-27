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
    fireEvent.click(await screen.findByRole('button', { name: /#42.*Documentar processo de compra/ }));
    fireEvent.click(screen.getByRole('button', { name: 'Vincular' }));

    await waitFor(() => expect(link).toHaveBeenCalledWith('project-1', 'page-1', 42));
    expect(search).toHaveBeenCalledWith('project-1', 'processo de compra');
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Buscar tarefa para vincular à Wiki' })).toHaveValue(''));
  });

  it('mostra o loading padrão em "Vincular" e vincula uma vez só (HU-0001)', async () => {
    vi.spyOn(api, 'getWikiPageLinks').mockResolvedValue([]);
    vi.spyOn(api, 'searchWorkItems').mockResolvedValue([{
      id: 'task-42', projectId: 'project-1', projectKey: 'PRD', number: 42,
      title: 'Documentar processo de compra', status: 'Em andamento',
    }]);
    let finish!: (value: { workItemId: string; number: number; title: string; reference: string }) => void;
    const link = vi.spyOn(api, 'linkWikiTask').mockImplementation(() => new Promise(resolve => { finish = resolve; }));
    renderLinks();

    fireEvent.change(screen.getByRole('combobox', { name: 'Buscar tarefa para vincular à Wiki' }), { target: { value: 'processo de compra' } });
    fireEvent.click(await screen.findByRole('button', { name: /#42.*Documentar processo de compra/ }));
    const button = screen.getByRole('button', { name: 'Vincular' });
    fireEvent.click(button);
    fireEvent.click(button);

    expect(button).toHaveAttribute('aria-busy', 'true');
    expect(button).toHaveTextContent('Vincular');
    expect(button.querySelector('[data-action-spinner]')).toHaveAttribute('aria-hidden', 'true');
    await waitFor(() => expect(link).toHaveBeenCalledTimes(1));
    fireEvent.click(button);
    expect(link).toHaveBeenCalledTimes(1);
    finish({ workItemId: 'task-42', number: 42, title: 'Documentar processo de compra', reference: 'PRD-42' });
    await waitFor(() => expect(button).not.toHaveAttribute('aria-busy'));
    expect(link).toHaveBeenCalledTimes(1);
  });

  it('mostra o loading só no vínculo que está sendo desfeito (HU-0001)', async () => {
    vi.spyOn(api, 'getWikiPageLinks').mockResolvedValue([
      { workItemId: 'task-1', number: 1, title: 'Primeira tarefa', reference: 'PRD-1' },
      { workItemId: 'task-2', number: 2, title: 'Segunda tarefa', reference: 'PRD-2' },
    ]);
    let finish!: () => void;
    const unlink = vi.spyOn(api, 'unlinkWikiTask').mockImplementation(() => new Promise<void>(resolve => { finish = resolve; }) as never);
    renderLinks();
    await screen.findByText('Primeira tarefa', { exact: false });
    const [first, second] = screen.getAllByTitle('Desvincular');

    fireEvent.click(first);
    fireEvent.click(first);

    await waitFor(() => expect(unlink).toHaveBeenCalledTimes(1));
    expect(unlink).toHaveBeenCalledWith('project-1', 'page-1', 'task-1');
    expect(first).toHaveAttribute('aria-busy', 'true');
    expect(second).not.toHaveAttribute('aria-busy');
    finish();
    await waitFor(() => expect(first).not.toHaveAttribute('aria-busy'));
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
