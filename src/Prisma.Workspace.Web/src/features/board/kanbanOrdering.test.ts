import { describe, expect, it } from 'vitest';
import { compareKanbanWorkItems, compareNewestWorkItems, positionBeforeColumn } from './kanbanOrdering';

describe('compareNewestWorkItems', () => {
  it('mantém os cartões mais recentes no topo', () => {
    const items = [
      { id: 'antigo', createdAt: '2026-08-18T10:00:00.000Z' },
      { id: 'novo', createdAt: '2026-08-20T10:00:00.000Z' },
    ];

    expect(items.sort(compareNewestWorkItems).map(item => item.id)).toEqual(['novo', 'antigo']);
  });

  it('usa o identificador como desempate determinístico', () => {
    const items = [
      { id: 'b', createdAt: '2026-08-20T10:00:00.000Z' },
      { id: 'a', createdAt: '2026-08-20T10:00:00.000Z' },
    ];

    expect(items.sort(compareNewestWorkItems).map(item => item.id)).toEqual(['a', 'b']);
  });
});


describe('compareKanbanWorkItems', () => {
  const items = [
    { id: 'late', createdAt: '2026-09-20T10:00:00.000Z', position: 20, priority: 1, dueDate: null, title: 'Late' },
    { id: 'first', createdAt: '2026-09-19T10:00:00.000Z', position: 10, priority: 2, dueDate: '2026-09-24', title: 'First' },
  ];

  it('sorts only the received column set by manual position', () => {
    expect([...items].sort(compareKanbanWorkItems).map(item => item.id)).toEqual(['first', 'late']);
  });

  it('supports a column-local priority sort without changing another column', () => {
    expect([...items].sort((a, b) => compareKanbanWorkItems(a, b, 'priority')).map(item => item.id)).toEqual(['first', 'late']);
    expect([...items].sort((a, b) => compareKanbanWorkItems(a, b, 'position')).map(item => item.id)).toEqual(['first', 'late']);
  });
});

describe('criação no topo da coluna', () => {
  it('insere repetidamente antes de zero preservando as posições dos demais cartões', () => {
    const items = [
      { id: 'a', position: 0, createdAt: '2030-01-01', title: 'Existente', priority: 1 },
      { id: 'b', position: 200, createdAt: '2030-01-02', title: 'Outro', priority: 1 },
    ];
    const original = structuredClone(items);
    for (const id of ['z', 'y', 'x']) {
      const position = positionBeforeColumn(items);
      expect(position).toBeLessThan(Math.min(...items.map(item => item.position)));
      items.push({ id, position, createdAt: '2020-01-01', title: id, priority: 1 });
      expect([...items].sort(compareKanbanWorkItems)[0].id).toBe(id);
    }
    expect(items.slice(0, 2)).toEqual(original);
  });
  it('começa em100 na coluna vazia e não usa data como desempate manual', () => {
    expect(positionBeforeColumn([])).toBe(100);
    const items = [
      { id: 'z', position: -100, createdAt: '2030-01-01', title: 'Z', priority: 1 },
      { id: 'a', position: -100, createdAt: '2020-01-01', title: 'A', priority: 1 },
    ];
    expect(items.sort(compareKanbanWorkItems).map(item => item.id)).toEqual(['a', 'z']);
  });
});
