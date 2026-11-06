import { defaultBacklogFilters, type BacklogFilterState, type BacklogGroupBy } from './BacklogFilters';

export interface BacklogUrlState {
  filters: BacklogFilterState;
  groupBy: BacklogGroupBy;
  selectedIds: Set<string>;
  collapsedIds: Set<string>;
  sprintId: string;
  scrollY: number;
}
const key = (name: string) => `bl.${name}`;
const member = <T extends string>(value: string | null, allowed: readonly T[], fallback: T): T =>
  allowed.includes(value as T) ? value as T : fallback;
const identifiers = (params: URLSearchParams, name: string) => new Set(params.getAll(key(name)).filter(value => /^[\w-]{1,128}$/.test(value)));
export function readBacklogUrl(params: URLSearchParams): BacklogUrlState {
  const y = Number(params.get(key('y')));
  return {
    filters: {
      ...defaultBacklogFilters,
      search: params.get(key('q')) ?? '',
      kind: member(params.get(key('kind')), ['all', '1','2','3','4','5','6','7','8','9','10'], 'all'),
      priority: member(params.get(key('priority')), ['all', '0','1','2','3'], 'all'),
      boardId: params.get(key('board')) || 'all',
      relation: member(params.get(key('relation')), ['all','blocked','dependencies','unparented'], 'all'),
      includeArchived: params.get(key('archived')) === '1',
    },
    groupBy: member(params.get(key('group')), ['none','epic','kind','priority','board'], 'none'),
    selectedIds: identifiers(params, 'selected'),
    collapsedIds: identifiers(params, 'collapsed'),
    sprintId: params.get(key('sprint')) ?? '',
    scrollY: Number.isFinite(y) && y >= 0 ? Math.floor(y) : 0,
  };
}
export function writeBacklogUrl(params: URLSearchParams, state: BacklogUrlState): URLSearchParams {
  const next = new URLSearchParams(params);
  for (const name of [...next.keys()]) if (name.startsWith('bl.')) next.delete(name);
  const put = (name: string, value: string, defaultValue = '') => { if (value !== defaultValue) next.set(key(name), value); };
  put('q', state.filters.search);
  put('kind', state.filters.kind, 'all');
  put('priority', state.filters.priority, 'all');
  put('board', state.filters.boardId, 'all');
  put('relation', state.filters.relation, 'all');
  if (state.filters.includeArchived) put('archived', '1');
  put('group', state.groupBy, 'none');
  put('sprint', state.sprintId);
  if (state.scrollY > 0) put('y', String(Math.floor(state.scrollY)));
  for (const id of [...state.selectedIds].sort()) next.append(key('selected'), id);
  for (const id of [...state.collapsedIds].sort()) next.append(key('collapsed'), id);
  return next;
}
