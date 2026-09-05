import { describe, expect, it } from 'vitest';
import type { BacklogItem } from '../../types/scrum';
import {
  defaultBacklogFilters,
  filterBacklogItems,
  getBacklogGroupLabel,
  getBacklogRelations,
  getEpicId,
} from './BacklogFilters';

const item = (values: Partial<BacklogItem>): BacklogItem => ({
  id: crypto.randomUUID(),
  boardId: 'board-1',
  boardName: 'Produto',
  kind: 5,
  title: 'Item',
  priority: 1,
  rank: 1000,
  ...values,
});

describe('BacklogFilters', () => {
  it('encontra o épico mesmo quando existe uma feature intermediária', () => {
    const epic = item({ id: 'epic', kind: 1, title: 'Portal' });
    const feature = item({ id: 'feature', kind: 2, parentId: epic.id });
    const story = item({ id: 'story', kind: 3, parentId: feature.id });

    expect(getEpicId(story, [epic, feature, story])).toBe(epic.id);
    expect(getBacklogGroupLabel(story, [epic, feature, story], 'epic')).toBe('Portal');
  });

  it('distingue dependências que bloqueiam o item de itens bloqueados por ele', () => {
    const current = item({
      links: [
        { id: 'a', type: 1, isIncoming: false, relatedWorkItemId: 'dep', relatedNumber: 2, relatedTitle: 'API', isOpen: true },
        { id: 'b', type: 2, isIncoming: false, relatedWorkItemId: 'child', relatedNumber: 3, relatedTitle: 'Tela', isOpen: true },
      ],
    });

    const result = getBacklogRelations(current);
    expect(result.blocked).toBe(true);
    expect(result.dependencies).toHaveLength(1);
    expect(result.blocking).toHaveLength(1);
  });

  it('filtra por pesquisa, tipo, prioridade e quadro', () => {
    const story = item({ id: 'story', number: 42, kind: 3, title: 'Login do cliente', priority: 2 });
    const bug = item({ id: 'bug', kind: 4, title: 'Falha no relatório', priority: 3, boardId: 'board-2' });
    const result = filterBacklogItems([story, bug], {
      ...defaultBacklogFilters,
      search: '42',
      kind: '3',
      priority: '2',
      boardId: 'board-1',
    });

    expect(result.map((current) => current.id)).toEqual(['story']);
  });

  it('localiza itens sem épico', () => {
    const epic = item({ id: 'epic', kind: 1 });
    const linked = item({ id: 'linked', kind: 3, parentId: epic.id });
    const orphan = item({ id: 'orphan', kind: 3 });
    const result = filterBacklogItems([epic, linked, orphan], {
      ...defaultBacklogFilters,
      relation: 'unparented',
    });

    expect(result.map((current) => current.id)).toEqual(['orphan']);
  });
});
