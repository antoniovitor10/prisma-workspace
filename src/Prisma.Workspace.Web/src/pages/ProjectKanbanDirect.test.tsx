import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom';
import { ThemeProvider } from 'styled-components';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '../services/api';
import { theme } from '../styles/theme';
import { Kanban } from './Kanban';
import { ProjectBoardView } from './ProjectWorkspace';

vi.mock('../preview', () => ({ previewMode: false }));
vi.mock('../features/board/useBoardRealtime', () => ({ useBoardRealtime: () => undefined }));
vi.mock('../layout/ContextBar', () => ({
  ContextBarInjector: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

const PROJETO = 'project-1';

const quadros = [
  { id: 'board-1', name: 'Planejamento', projectId: PROJETO },
  { id: 'board-2', name: 'Sustentação', projectId: PROJETO },
  { id: 'board-outro', name: 'De outro projeto', projectId: 'project-9' },
];

const etapas = [
  { id: 'stage-1', projectId: PROJETO, name: 'A fazer', position: 1000, category: 1 },
  { id: 'stage-2', projectId: PROJETO, name: 'Concluído', position: 2000, category: 4 },
];

/** Um cartão de cada quadro: o Kanban do projeto tem de mostrar os dois. */
const cartoes = [
  { id: 'item-1', number: 1, boardId: 'board-1', title: 'Cartão do quadro um', stageId: 'stage-1', position: 1000, priority: 2, createdAt: '2026-09-01T10:00:00Z' },
  { id: 'item-2', number: 2, boardId: 'board-2', title: 'Cartão do quadro dois', stageId: 'stage-1', position: 2000, priority: 2, createdAt: '2026-09-02T10:00:00Z' },
];

function ContextoDoProjeto() {
  return <Outlet context={{ project: { id: PROJETO, key: 'DEMO', name: 'Produto Demo', boards: [], teams: [] } }} />;
}

function renderRota(rota: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <ThemeProvider theme={theme}>
        <MemoryRouter initialEntries={[rota]}>
          <Routes>
            {/* A rota real passa pelo invólucro que injeta a alternância Backlog/Quadro. */}
            <Route path="/projects/:projectId" element={<ContextoDoProjeto />}>
              <Route path="boards" element={<ProjectBoardView />} />
            </Route>
            <Route path="/boards/:boardId" element={<Kanban />} />
          </Routes>
        </MemoryRouter>
      </ThemeProvider>
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.spyOn(api, 'getBoards').mockResolvedValue(quadros);
  vi.spyOn(api, 'getStages').mockResolvedValue(etapas);
  vi.spyOn(api, 'getWorkItemsByProject').mockResolvedValue(cartoes);
  vi.spyOn(api, 'getWorkItems').mockResolvedValue([cartoes[0]]);
  vi.spyOn(api, 'getProject').mockResolvedValue({
    id: PROJETO, key: 'DEMO', methodology: 1, teams: [{ id: 'team-1', name: 'Produtos digitais' }],
  });
  vi.spyOn(api, 'getProjectSprints').mockResolvedValue([]);
  vi.spyOn(api, 'getTags').mockResolvedValue([]);
  vi.spyOn(api, 'getSavedFilters').mockResolvedValue([]);
  vi.spyOn(api, 'getAssignableUsers').mockResolvedValue([]);
  vi.spyOn(api, 'getMyActiveTimer').mockResolvedValue(null);
  vi.spyOn(api, 'getBoardLeadTime').mockResolvedValue([]);
});

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe('Kanban do projeto abre direto', () => {
  it('mantém a alternância Backlog ↔ Quadro, que vinha da tela removida', async () => {
    renderRota(`/projects/${PROJETO}/boards`);

    const alternar = await screen.findByRole('navigation', { name: 'Alternar visão do projeto' });
    expect(alternar).toBeInTheDocument();
    // Sem isto não haveria caminho de volta ao backlog dentro da barra de contexto.
    expect(await screen.findByRole('link', { name: 'Backlog' })).toHaveAttribute(
      'href', `/projects/${PROJETO}/backlog`);
  });

  it('carrega o fluxo e os cartões do projeto sem ninguém escolher quadro', async () => {
    renderRota(`/projects/${PROJETO}/boards`);

    // Era isto que a tela intermediária exigia: escolher um quadro antes de ver qualquer coisa.
    await waitFor(() => expect(api.getStages).toHaveBeenCalledWith(PROJETO));
    await waitFor(() => expect(api.getWorkItemsByProject).toHaveBeenCalledWith(PROJETO));
    expect(await screen.findByText('A fazer')).toBeInTheDocument();
  });

  it('mostra cartões de todos os quadros do projeto, não só de um', async () => {
    renderRota(`/projects/${PROJETO}/boards`);

    // O título do cartão é renderizado em dois nós ("#1 · " e o título), então a âncora
    // estável é o rótulo acessível da caixa de seleção do cartão.
    expect(await screen.findByRole('checkbox',
      { name: 'Selecionar #1 Cartão do quadro um' })).toBeInTheDocument();
    expect(await screen.findByRole('checkbox',
      { name: 'Selecionar #2 Cartão do quadro dois' })).toBeInTheDocument();
  });

  it('no modo quadro segue buscando por quadro, sem alargar o escopo', async () => {
    renderRota('/boards/board-1');

    await waitFor(() => expect(api.getWorkItems).toHaveBeenCalledWith('board-1'));
    expect(api.getWorkItemsByProject).not.toHaveBeenCalled();
  });

  it('permite criar quadro com equipe, capacidade que vinha da tela removida', async () => {
    const createBoard = vi.spyOn(api, 'createBoard').mockResolvedValue('board-novo');
    renderRota(`/projects/${PROJETO}/boards`);

    await screen.findByText('A fazer');
    fireEvent.click(await screen.findByRole('button', { name: /Novo quadro/i }));

    fireEvent.change(await screen.findByRole('textbox', { name: 'Nome do quadro' }),
      { target: { value: 'Atendimento digital' } });
    fireEvent.change(await screen.findByRole('combobox', { name: 'Equipe do quadro' }),
      { target: { value: 'team-1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Criar' }));

    await waitFor(() => expect(createBoard).toHaveBeenCalledWith(
      'Atendimento digital', PROJETO, 'team-1'));
  });

  it('permite editar o nome e classificação da coluna pelo botão de editar no cabeçalho', async () => {
    const updateStage = vi.spyOn(api, 'updateStage').mockResolvedValue(undefined);
    renderRota(`/projects/${PROJETO}/boards`);

    await screen.findByText('A fazer');
    const editBtn = await screen.findByRole('button', { name: 'Editar coluna A fazer' });
    fireEvent.click(editBtn);

    expect(await screen.findByText('Editar Coluna')).toBeInTheDocument();
    const input = screen.getByPlaceholderText('Nome da Coluna');
    expect(input).toHaveValue('A fazer');

    fireEvent.change(input, { target: { value: 'Ideias Novas' } });
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }));

    await waitFor(() => expect(updateStage).toHaveBeenCalledWith(
      'stage-1',
      expect.objectContaining({ name: 'Ideias Novas' })
    ));
  });
});
