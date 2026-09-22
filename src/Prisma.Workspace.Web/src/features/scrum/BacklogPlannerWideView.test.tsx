import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ThemeProvider } from 'styled-components';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { ProjectSummary } from '../../pages/Projects';
import { api } from '../../services/api';
import { theme } from '../../styles/theme';
import type { BacklogItem, Sprint } from '../../types/scrum';
import { BacklogPlanner } from './BacklogPlanner';

const project: ProjectSummary = {
  id: 'project-1',
  key: 'PRD',
  name: 'Produto',
  ownerId: 'user-1',
  methodology: 2,
  boards: [],
  teams: [],
  members: [{ userId: 'user-1', role: 5 }],
};

const sprint: Sprint = {
  id: 'sprint-1',
  teamId: 'team-1',
  teamName: 'Time Produto',
  name: 'Sprint 12',
  goal: 'Publicar o fluxo principal',
  startDate: '2026-07-01',
  endDate: '2026-07-14',
  status: 2,
  itemCount: 0,
  completedItemCount: 0,
  storyPoints: 0,
  completedStoryPoints: 0,
  capacities: [],
};

const items: BacklogItem[] = [
  { id: 'story-1', number: 11, boardId: 'board', boardName: 'Produto', kind: 3, title: 'Abrir solicitação', priority: 2, rank: 1 },
];

function renderPlanner() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <ThemeProvider theme={theme}>
        <MemoryRouter>
          <BacklogPlanner project={project} />
        </MemoryRouter>
      </ThemeProvider>
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  localStorage.clear();
  vi.spyOn(api, 'getProjectBacklog').mockResolvedValue(items);
  vi.spyOn(api, 'getProjectSprints').mockResolvedValue([sprint]);
});

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  localStorage.clear();
});

const painelSprint = () => screen.queryByRole('heading', { name: 'Sprint 12' });

describe('visão ampla do backlog', () => {
  it('mostra o painel da sprint ao lado por padrão', async () => {
    renderPlanner();

    await waitFor(() => expect(painelSprint()).toBeInTheDocument());
    expect(screen.getByRole('button', { name: 'Ampliar backlog' })).toHaveAttribute('aria-pressed', 'false');
  });

  it('esconde o painel da sprint ao ampliar e o traz de volta', async () => {
    renderPlanner();
    await waitFor(() => expect(painelSprint()).toBeInTheDocument());

    fireEvent.click(screen.getByRole('button', { name: 'Ampliar backlog' }));

    await waitFor(() => expect(painelSprint()).not.toBeInTheDocument());
    const voltar = screen.getByRole('button', { name: 'Ver sprint ao lado' });
    expect(voltar).toHaveAttribute('aria-pressed', 'true');
    // O backlog nao pode desaparecer junto: e ele que ganha a largura.
    expect(screen.getByRole('heading', { name: /Product backlog/i })).toBeInTheDocument();

    fireEvent.click(voltar);
    await waitFor(() => expect(painelSprint()).toBeInTheDocument());
  });

  it('guarda a preferência e volta ampliado', async () => {
    renderPlanner();
    await waitFor(() => expect(painelSprint()).toBeInTheDocument());
    fireEvent.click(screen.getByRole('button', { name: 'Ampliar backlog' }));
    await waitFor(() => expect(localStorage.getItem('prisma_workspace_backlog_amplo')).toBe('true'));

    cleanup();
    renderPlanner();

    await waitFor(() => expect(screen.getByRole('button', { name: 'Ver sprint ao lado' })).toBeInTheDocument());
    expect(painelSprint()).not.toBeInTheDocument();
  });

  it('mantém como planejar sem o painel: seleção abre a barra em massa com sprint e destino', async () => {
    localStorage.setItem('prisma_workspace_backlog_amplo', 'true');
    renderPlanner();

    await waitFor(() => expect(
      screen.getByRole('textbox', { name: 'Título de Abrir solicitação' })).toBeInTheDocument());
    expect(painelSprint()).not.toBeInTheDocument();

    // Sem o painel da sprint nao ha alvo de arraste; planejar por selecao precisa continuar
    // possivel, senao o modo amplo tiraria funcao em silencio.
    fireEvent.click(screen.getByRole('checkbox', { name: 'Selecionar Abrir solicitação' }));

    expect(await screen.findByRole('combobox', { name: 'Sprint para ação em massa' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Mover para sprint' })).toBeEnabled();
  });
});

describe('faixa de filtros recolhida', () => {
  it('abre recolhida: nenhum campo de filtro visível', async () => {
    renderPlanner();

    const alternar = await screen.findByRole('button', { name: /Filtros/ });
    expect(alternar).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('combobox', { name: /Agrupar por|Mostrar tarefas arquivadas/ })).not.toBeInTheDocument();
  });

  it('mostra os campos ao abrir e guarda a preferência', async () => {
    renderPlanner();

    fireEvent.click(await screen.findByRole('button', { name: /Filtros/ }));

    expect(await screen.findByRole('combobox', { name: 'Mostrar tarefas arquivadas' })).toBeInTheDocument();
    await waitFor(() =>
      expect(localStorage.getItem('prisma_workspace_backlog_filtros_abertos')).toBe('true'));

    cleanup();
    renderPlanner();
    expect(await screen.findByRole('combobox', { name: 'Mostrar tarefas arquivadas' })).toBeInTheDocument();
  });

  it('recolhida, um filtro ativo continua visível e limpável', async () => {
    localStorage.setItem('prisma_workspace_backlog_filtros_abertos', 'true');
    renderPlanner();

    // Liga um filtro com a faixa aberta e depois recolhe.
    fireEvent.change(await screen.findByRole('combobox', { name: 'Mostrar tarefas arquivadas' }),
      { target: { value: 'yes' } });
    fireEvent.click(screen.getByRole('button', { name: /Filtros/ }));

    // Filtro ligado nunca pode ficar escondido: some o campo, fica o aviso e o limpar.
    await waitFor(() =>
      expect(screen.queryByRole('combobox', { name: 'Mostrar tarefas arquivadas' })).not.toBeInTheDocument());
    expect(screen.getByText(/1 ativo/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Limpar filtros' })).toBeInTheDocument();
  });
});
