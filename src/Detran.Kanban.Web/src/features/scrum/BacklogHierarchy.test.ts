import { describe, expect, it } from 'vitest';
import type { BacklogItem } from '../../types/scrum';
import { flattenBacklogHierarchy, includeBacklogAncestors } from './BacklogHierarchy';

const item = (id: string, rank: number, parentId?: string): BacklogItem => ({
  id, number: rank, boardId: 'board', boardName: 'Produto', kind: 5,
  title: id, priority: 1, rank, parentId,
});

describe('BacklogHierarchy', () => {
  it('ordena pai antes dos descendentes e colapsa todo o ramo', () => {
    const items = [item('child', 1, 'root'), item('grandchild', 1, 'child'), item('root', 9)];
    expect(flattenBacklogHierarchy(items, items, new Set()).items.map(current => current.id))
      .toEqual(['root', 'child', 'grandchild']);
    expect(flattenBacklogHierarchy(items, items, new Set(['root'])).items.map(current => current.id))
      .toEqual(['root']);
  });

  it('preserva todos os ancestrais quando somente o descendente corresponde ao filtro', () => {
    const items = [item('root', 1), item('child', 2, 'root'), item('grandchild', 3, 'child')];
    const contextual = includeBacklogAncestors([items[2]], items);
    expect(flattenBacklogHierarchy(contextual, items, new Set()).items.map(current => current.id))
      .toEqual(['root', 'child', 'grandchild']);
  });

  it('interrompe ciclos sem recursão infinita e registra o problema', () => {
    const items = [item('a', 1, 'b'), item('b', 2, 'a')];
    const result = flattenBacklogHierarchy(items, items, new Set());
    expect(result.items).toHaveLength(2);
    expect(result.errors.join(' ')).toMatch(/Ciclo hierárquico/);
  });
});
