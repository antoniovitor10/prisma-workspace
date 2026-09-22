import { describe, expect, it } from 'vitest';
import { compareKanbanWorkItems, compareNewestWorkItems } from './kanbanOrdering';

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
