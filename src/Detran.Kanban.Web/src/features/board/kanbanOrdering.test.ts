import { describe, expect, it } from 'vitest';
import { compareNewestWorkItems } from './kanbanOrdering';

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
