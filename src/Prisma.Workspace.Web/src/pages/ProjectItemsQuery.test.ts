import { describe, expect, it } from 'vitest';
import type { BacklogItem } from '../types/scrum';
import {
  definitionFromParams,
  definitionToParams,
  runProjectQuery,
  type ProjectQueryDefinition,
} from './ProjectItemsQuery.logic';

const base: ProjectQueryDefinition = {
  scope: 'project-items-v1',
  version: 1,
  segment: 'all',
  search: '',
  priority: 'all',
  boardId: 'all',
  stageId: 'all',
  match: 'all',
  groupBy: 'none',
  sortBy: 'rank',
};

const items: BacklogItem[] = [
  { id: 'epic', boardId: 'a', boardName: 'Produto', kind: 1, title: 'Expansão', priority: 2, rank: 2 },
  { id: 'bug', boardId: 'b', boardName: 'Operação', stageId: 'doing', kind: 4, title: 'Falha no login', description: 'Sessão expirada', priority: 3, rank: 1 },
  { id: 'task', boardId: 'a', boardName: 'Produto', stageId: 'todo', kind: 5, title: 'Revisar acesso', priority: 1, rank: 3 },
];

describe('consultas de itens por projeto', () => {
  it('segmenta por tipo e combina critérios com E ou OU', () => {
    expect(runProjectQuery(items, { ...base, segment: '4' }).map(item => item.id)).toEqual(['bug']);
    expect(runProjectQuery(items, { ...base, search: 'acesso', priority: '3', match: 'all' })).toEqual([]);
    expect(runProjectQuery(items, { ...base, search: 'acesso', priority: '3', match: 'any' }).map(item => item.id)).toEqual(['bug', 'task']);
  });

  it('serializa somente critérios ativos e recompõe uma URL compartilhada', () => {
    const query = { ...base, segment: '3' as const, search: 'valor ao cliente', priority: '2', groupBy: 'kind' as const };
    const params = definitionToParams(query, new URLSearchParams('preserve=1'));

    expect(params.toString()).toContain('preserve=1');
    expect(params.toString()).not.toContain('match=');
    expect(definitionFromParams(params)).toEqual(query);
  });
});
