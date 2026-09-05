import { fireEvent, render, screen } from '@testing-library/react';
import { ThemeProvider } from 'styled-components';
import { describe, expect, it, vi } from 'vitest';
import { theme } from '../../styles/theme';
import type { BacklogItem, Sprint } from '../../types/scrum';
import SprintBacklogPanel from './SprintBacklogPanel';
import SprintHistoryPanel from './SprintHistoryPanel';

const sprint: Sprint = {
  id: 'sprint-1',
  teamId: 'team-1',
  teamName: 'Time Produto',
  name: 'Sprint 12',
  goal: 'Publicar o fluxo principal',
  startDate: '2026-07-01',
  endDate: '2026-07-14',
  status: 2,
  itemCount: 2,
  completedItemCount: 1,
  storyPoints: 8,
  completedStoryPoints: 3,
  capacities: [],
};

const items: BacklogItem[] = [
  { id: 'epic-1', number: 10, boardId: 'board', boardName: 'Produto', kind: 1, title: 'Portal', priority: 2, rank: 1, sprintId: sprint.id },
  { id: 'story-1', number: 11, boardId: 'board', boardName: 'Produto', kind: 3, title: 'Abrir solicitação', priority: 2, rank: 2, sprintId: sprint.id, parentId: 'epic-1', points: 8, acceptanceCriteria: 'Dado que o formulário é válido, gerar protocolo.' },
];

const withTheme = (component: React.ReactNode) => render(<ThemeProvider theme={theme}>{component}</ThemeProvider>);

describe('painéis Scrum', () => {
  it('expõe hierarquia, critérios e acesso ao planejamento no Sprint Backlog', () => {
    const onOpenItem = vi.fn();
    const onPlan = vi.fn();
    withTheme(<SprintBacklogPanel sprint={sprint} items={items} allItems={items} projectKey="PRD" onOpenItem={onOpenItem} onPlan={onPlan} />);

    expect(screen.getByRole('heading', { name: /Sprint Backlog/i })).toBeInTheDocument();
    expect(screen.getByText('Definidos')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Abrir Abrir solicitação' }));
    fireEvent.click(screen.getByRole('button', { name: /Planejar Product Backlog/i }));

    expect(onOpenItem).toHaveBeenCalledWith(items[1]);
    expect(onPlan).toHaveBeenCalledOnce();
  });

  it('mostra somente sprints encerradas no histórico e permite consultá-las', () => {
    const onSelectSprint = vi.fn();
    const closed = { ...sprint, id: 'sprint-closed', name: 'Sprint 11', status: 3 };
    withTheme(<SprintHistoryPanel sprints={[sprint, closed]} selectedSprintId={sprint.id} onSelectSprint={onSelectSprint} />);

    expect(screen.getByText('Sprint 11')).toBeInTheDocument();
    expect(screen.queryByText('Sprint 12')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Consultar resultados de Sprint 11' }));

    expect(onSelectSprint).toHaveBeenCalledWith(closed.id);
  });
});
