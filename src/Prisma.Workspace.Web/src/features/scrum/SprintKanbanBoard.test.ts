import { describe, expect, it } from 'vitest';
import type { BacklogItem } from '../../types/scrum';
import { groupSprintItemsByBoard } from './SprintKanbanBoard';

const item = (id: string, boardId: string, boardName: string): BacklogItem => ({
  id,
  boardId,
  boardName,
  kind: 5,
  title: id,
  priority: 1,
  rank: 1,
});

describe('SprintKanbanBoard', () => {
  it('separa itens por BoardId mesmo quando o nome do quadro se repete', () => {
    const groups = groupSprintItemsByBoard([
      item('a', 'board-b', 'Operação'),
      item('b', 'board-a', 'Operação'),
      item('c', 'board-b', 'Operação'),
    ]);

    expect(groups).toHaveLength(2);
    expect(groups.find((group) => group.boardId === 'board-b')?.items.map((entry) => entry.id)).toEqual(['a', 'c']);
    expect(groups.find((group) => group.boardId === 'board-a')?.items.map((entry) => entry.id)).toEqual(['b']);
  });

  it('mostra só os cards do quadro: Épico, Funcionalidade e História ficam fora (T-0022)', () => {
    const groups = groupSprintItemsByBoard([
      { ...item('historia', 'board-a', 'Operação'), kind: 3, isBoardCard: false },
      { ...item('tarefa-da-historia', 'board-a', 'Operação'), parentId: 'historia', isBoardCard: true },
      { ...item('epico', 'board-b', 'Planejamento'), kind: 1, isBoardCard: false },
      item('sem-marca', 'board-a', 'Operação'),
    ]);

    expect(groups).toHaveLength(1);
    expect(groups[0].items.map((entry) => entry.id)).toEqual(['tarefa-da-historia', 'sem-marca']);
  });
});
