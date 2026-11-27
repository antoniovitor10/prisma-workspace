import { notifyManager, QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ThemeProvider } from 'styled-components';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '../services/api';
import { darkTheme, lightTheme } from '../styles/theme';
import { Kanban } from './Kanban';

vi.mock('../preview', () => ({ previewMode: false }));
vi.mock('../features/board/useBoardRealtime', () => ({ useBoardRealtime: () => undefined }));
vi.mock('../layout/ContextBar', () => ({
  ContextBarInjector: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

const PROJETO = 'project-1';
const etapas = [
  { id: 'stage-1', projectId: PROJETO, name: 'A fazer', position: 1000, category: 1 },
  { id: 'stage-2', projectId: PROJETO, name: 'Concluído', position: 2000, category: 5 },
];
const pessoa = { id: 'user-1', email: 'pessoa@example.test', fullName: 'Pessoa Teste' };

async function renderQuadro(mode: 'light' | 'dark') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  await act(async () => {
    render(
      <QueryClientProvider client={queryClient}>
        <ThemeProvider theme={mode === 'dark' ? darkTheme : lightTheme}>
          <MemoryRouter initialEntries={['/boards/board-1']}>
            <Routes>
              <Route path="/boards/:boardId" element={<Kanban />} />
            </Routes>
          </MemoryRouter>
        </ThemeProvider>
      </QueryClientProvider>,
    );
  });
}

beforeEach(() => {
  notifyManager.setScheduler(queueMicrotask);
  notifyManager.setNotifyFunction(callback => act(callback));
  vi.spyOn(api, 'getBoards').mockResolvedValue([{ id: 'board-1', name: 'Sustentação', projectId: PROJETO }]);
  vi.spyOn(api, 'getBoardStages').mockResolvedValue(etapas);
  vi.spyOn(api, 'getWorkItems').mockResolvedValue([]);
  vi.spyOn(api, 'getWorkItemsByProject').mockResolvedValue([]);
  vi.spyOn(api, 'getProject').mockResolvedValue({ id: PROJETO, key: 'DEMO', methodology: 1, teams: [] });
  vi.spyOn(api, 'getProjectSprints').mockResolvedValue([]);
  vi.spyOn(api, 'getTags').mockResolvedValue([]);
  vi.spyOn(api, 'getSavedFilters').mockResolvedValue([]);
  vi.spyOn(api, 'getAssignableUsers').mockResolvedValue([pessoa]);
  vi.spyOn(api, 'getMyActiveTimer').mockResolvedValue(null);
  vi.spyOn(api, 'getRunningTimeEntry').mockResolvedValue(null);
  vi.spyOn(api, 'getBoardLeadTime').mockResolvedValue([]);
});

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  notifyManager.setScheduler(callback => setTimeout(callback, 0));
  notifyManager.setNotifyFunction(callback => callback());
});

describe('Novo card sem itens de planejamento (T-0022, CA-13)', () => {
  it.each(['light', 'dark'] as const)('no tema %s oferece só Tarefa e Bug e explica onde criar Épicos e Histórias', async mode => {
    await renderQuadro(mode);
    const coluna = await screen.findByRole('button', { name: 'Editar coluna A fazer' });
    expect(coluna).toBeInTheDocument();

    fireEvent.click(screen.getAllByRole('button', { name: 'Novo Card' })[0]);

    const tipo = await screen.findByRole('combobox', { name: 'Tipo do Card' });
    const opcoes = within(tipo).getAllByRole('option').map(option => option.textContent);
    expect(opcoes).toEqual(['Tarefa', 'Bug']);
    expect(within(tipo).queryByRole('option', { name: 'Épico' })).not.toBeInTheDocument();
    expect(within(tipo).queryByRole('option', { name: 'Feature' })).not.toBeInTheDocument();
    expect(within(tipo).queryByRole('option', { name: 'História' })).not.toBeInTheDocument();
    expect(tipo).toHaveAccessibleDescription('Épicos e Histórias são criados no backlog');
    expect(screen.getByRole('note')).toHaveTextContent('Épicos e Histórias são criados no backlog');
    expect(screen.getByRole('note')).toBeVisible();
  });

  it('cria o card com o tipo escolhido no seletor reduzido', async () => {
    const criar = vi.spyOn(api, 'createWorkItem').mockResolvedValue('item-novo' as never);
    await renderQuadro('light');
    await screen.findByRole('button', { name: 'Editar coluna A fazer' });
    fireEvent.click(screen.getAllByRole('button', { name: 'Novo Card' })[0]);

    fireEvent.change(await screen.findByPlaceholderText('Título do Card'), { target: { value: 'Corrigir login' } });
    fireEvent.change(screen.getByRole('combobox', { name: 'Tipo do Card' }), { target: { value: '4' } });
    fireEvent.change(screen.getByRole('combobox', { name: 'Responsável principal da nova tarefa' }), { target: { value: 'user-1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Criar Card' }));

    await waitFor(() => expect(criar).toHaveBeenCalledWith(expect.objectContaining({ title: 'Corrigir login', kind: 4, stageId: 'stage-1' })));
  });
});
