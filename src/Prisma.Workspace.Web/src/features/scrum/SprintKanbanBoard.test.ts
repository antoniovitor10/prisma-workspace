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
});
