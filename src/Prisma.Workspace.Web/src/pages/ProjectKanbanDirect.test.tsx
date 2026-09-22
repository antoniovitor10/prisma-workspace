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
  vi.spyOn(api, 'getBoardStages').mockResolvedValue(etapas);
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
  vi.spyOn(api, 'getRunningTimeEntry').mockResolvedValue(null);
  vi.spyOn(api, 'startTimer').mockResolvedValue({ id: 'timer-default', workItemId: 'item-1', startedAt: '2026-09-22T10:00:00Z' } as never);
  vi.spyOn(api, 'getBoardLeadTime').mockResolvedValue([]);
});

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe('confirmação D62 por quadro', () => {
  it('exige consentimento separado da descendência e envia a fotografia do impacto', async () => {
    vi.spyOn(api, 'getStageImpact').mockResolvedValue({
      stageId:'stage-1', name:'A fazer', totalItems:1, changedItems:1, openDescendants:2, snapshotToken:'snapshot-1'
    });
    const update = vi.spyOn(api, 'updateStage').mockResolvedValue(undefined);
    renderRota('/boards/board-1');
    fireEvent.click(await screen.findByRole('button', {name:'Editar coluna A fazer'}));
    fireEvent.change(screen.getByLabelText('Classificação da coluna'), {target:{value:'4'}});
    expect(await screen.findByText(/1 tarefa\(s\) na coluna; 1 terão o estado alterado; 2 descendente/)).toBeInTheDocument();
    fireEvent.click(screen.getByLabelText('Confirmo alterar o estado das tarefas desta coluna'));
    fireEvent.click(screen.getByRole('button', {name:'Salvar'}));
    expect(update).not.toHaveBeenCalled();
    fireEvent.click(screen.getByLabelText('Aceito concluir também todos os 2 descendentes abertos, sem movê-los'));
    fireEvent.click(screen.getByRole('button', {name:'Salvar'}));
    await waitFor(()=>expect(update).toHaveBeenCalledWith('stage-1', {
      name:'A fazer', category:4, confirmCategoryChange:true, confirmDescendants:true, impactToken:'snapshot-1'
    }));
  });

  it('recarrega impacto obsoleto e exige nova confirmação', async () => {
    const impact = vi.spyOn(api, 'getStageImpact')
      .mockResolvedValueOnce({stageId:'stage-1',name:'A fazer',totalItems:1,changedItems:1,openDescendants:0,snapshotToken:'old'})
      .mockResolvedValue({stageId:'stage-1',name:'A fazer',totalItems:2,changedItems:2,openDescendants:0,snapshotToken:'new'});
    vi.spyOn(api, 'updateStage').mockRejectedValue(new Error('O impacto da classificação mudou.'));
    renderRota('/boards/board-1');
    fireEvent.click(await screen.findByRole('button', {name:'Editar coluna A fazer'}));
    fireEvent.change(screen.getByLabelText('Classificação da coluna'), {target:{value:'4'}});
    fireEvent.click(await screen.findByLabelText('Confirmo alterar o estado das tarefas desta coluna'));
    fireEvent.click(screen.getByRole('button', {name:'Salvar'}));
    await waitFor(()=>expect(impact).toHaveBeenCalledTimes(2));
    expect(screen.getByLabelText('Confirmo alterar o estado das tarefas desta coluna')).not.toBeChecked();
    expect(await screen.findByText(/2 tarefa\(s\) na coluna/)).toBeInTheDocument();
  });
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
    await waitFor(() => expect(api.getBoardStages).toHaveBeenCalledWith('board-1'));
    await waitFor(() => expect(api.getWorkItems).toHaveBeenCalledWith('board-1'));
    expect(await screen.findByText('A fazer')).toBeInTheDocument();
  });

  it('mostra erro ao carregar quadros e permite tentar novamente sem dados incorretos', async () => {
    const getBoards = vi.mocked(api.getBoards);
    getBoards.mockReset();
    getBoards.mockRejectedValueOnce(new Error('Falha temporária ao carregar quadros')).mockResolvedValueOnce(quadros);
    renderRota(`/projects/${PROJETO}/boards`);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Não foi possível carregar os quadros disponíveis');
    expect(screen.queryByText('Planejamento')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Tentar carregar os quadros novamente' }));
    await waitFor(() => expect(getBoards).toHaveBeenCalledTimes(2));
    expect(await screen.findByText('Planejamento')).toBeInTheDocument();
  });

  it('mostra somente cartões do quadro selecionado do projeto', async () => {
    renderRota(`/projects/${PROJETO}/boards`);

    // O título do cartão é renderizado em dois nós ("#1 · " e o título), então a âncora
    // estável é o rótulo acessível da caixa de seleção do cartão.
    expect(await screen.findByRole('checkbox',
      { name: 'Selecionar #1 Cartão do quadro um' })).toBeInTheDocument();
    expect(screen.queryByRole('checkbox',
      { name: 'Selecionar #2 Cartão do quadro dois' })).not.toBeInTheDocument();
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
      'Atendimento digital', PROJETO, 'team-1', undefined));
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

  it('permite iniciar outro cronometro e delega o encerramento do anterior a API', async () => {
    vi.spyOn(api, 'getRunningTimeEntry').mockResolvedValue({
      id: 'timer-1', workItemId: 'item-2', startedAt: '2026-09-22T10:00:00Z',
    } as never);
    const startTimer = vi.spyOn(api, 'startTimer').mockResolvedValue({
      id: 'timer-2', workItemId: 'item-1', startedAt: '2026-09-22T10:05:00Z',
    } as never);

    renderRota('/boards/board-1');

    await screen.findByRole('checkbox', { name: /Selecionar #1 Cart/i });
    const start = await screen.findByRole('button', { name: /Iniciar cron/i });
    fireEvent.click(start);

    await waitFor(() => expect(startTimer).toHaveBeenCalledWith('item-1'));
  });
});
