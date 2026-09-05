import type { BacklogItem } from '../types/scrum';

export type Segment = 'all' | '1' | '2' | '3' | '4' | '5';
export type MatchMode = 'all' | 'any';
export type GroupBy = 'none' | 'kind' | 'priority' | 'board' | 'stage';
export type SortBy = 'rank' | 'newest' | 'title' | 'due';

export interface ProjectQueryDefinition {
  scope: 'project-items-v1';
  version: 1;
  segment: Segment;
  search: string;
  priority: string;
  boardId: string;
  stageId: string;
  match: MatchMode;
  groupBy: GroupBy;
  sortBy: SortBy;
}

export const defaultProjectQuery: ProjectQueryDefinition = {
  scope: 'project-items-v1', version: 1, segment: 'all', search: '', priority: 'all',
  boardId: 'all', stageId: 'all', match: 'all', groupBy: 'none', sortBy: 'rank',
};

function validValue<T extends string>(value: string | null, allowed: readonly T[], fallback: T): T {
  return value && allowed.includes(value as T) ? value as T : fallback;
}

export function definitionFromParams(params: URLSearchParams): ProjectQueryDefinition {
  return {
    ...defaultProjectQuery,
    segment: validValue(params.get('segment'), ['all', '1', '2', '3', '4', '5'], 'all'),
    search: params.get('q') ?? '',
    priority: validValue(params.get('priority'), ['all', '0', '1', '2', '3'], 'all'),
    boardId: params.get('board') ?? 'all',
    stageId: params.get('stage') ?? 'all',
    match: validValue(params.get('match'), ['all', 'any'], 'all'),
    groupBy: validValue(params.get('group'), ['none', 'kind', 'priority', 'board', 'stage'], 'none'),
    sortBy: validValue(params.get('sort'), ['rank', 'newest', 'title', 'due'], 'rank'),
  };
}

export function definitionToParams(definition: ProjectQueryDefinition, current = new URLSearchParams()) {
  const params = new URLSearchParams(current);
  ['segment', 'q', 'priority', 'board', 'stage', 'match', 'group', 'sort'].forEach(key => params.delete(key));
  if (definition.segment !== 'all') params.set('segment', definition.segment);
  if (definition.search.trim()) params.set('q', definition.search.trim());
  if (definition.priority !== 'all') params.set('priority', definition.priority);
  if (definition.boardId !== 'all') params.set('board', definition.boardId);
  if (definition.stageId !== 'all') params.set('stage', definition.stageId);
  if (definition.match !== 'all') params.set('match', definition.match);
  if (definition.groupBy !== 'none') params.set('group', definition.groupBy);
  if (definition.sortBy !== 'rank') params.set('sort', definition.sortBy);
  return params;
}

export function isProjectQuery(value: unknown): value is ProjectQueryDefinition {
  return Boolean(value && typeof value === 'object' && (value as { scope?: string }).scope === 'project-items-v1');
}

export function runProjectQuery(items: BacklogItem[], query: ProjectQueryDefinition) {
  const search = query.search.trim().toLocaleLowerCase('pt-BR');
  const selectedKind = query.segment === 'all' ? null : Number(query.segment);
  const filtered = items.filter(item => {
    if (selectedKind !== null && item.kind !== selectedKind) return false;
    const criteria: boolean[] = [];
    if (search) criteria.push(`${item.number ?? ''} ${item.title} ${item.description ?? ''} ${item.boardName}`.toLocaleLowerCase('pt-BR').includes(search));
    if (query.priority !== 'all') criteria.push(item.priority === Number(query.priority));
    if (query.boardId !== 'all') criteria.push(item.boardId === query.boardId);
    if (query.stageId !== 'all') criteria.push(item.stageId === query.stageId);
    return criteria.length === 0 || (query.match === 'all' ? criteria.every(Boolean) : criteria.some(Boolean));
  });

  return filtered.sort((left, right) => {
    if (query.sortBy === 'newest') return String(right.createdAt ?? '').localeCompare(String(left.createdAt ?? ''));
    if (query.sortBy === 'title') return left.title.localeCompare(right.title, 'pt-BR');
    if (query.sortBy === 'due') return String(left.dueDate ?? '9999').localeCompare(String(right.dueDate ?? '9999'));
    return left.rank - right.rank;
  });
}
