import type { BacklogItem } from '../../types/scrum';
import { kindNames, priorityNames } from '../../types/scrum';

export type BacklogRelationFilter = 'all' | 'blocked' | 'dependencies' | 'unparented';
export type BacklogGroupBy = 'none' | 'epic' | 'kind' | 'priority' | 'board';

export interface BacklogFilterState {
  /** Tarefa arquivada some de toda listagem por padrão; ligar aqui a torna visível
   *  e alcançável para restaurar. */
  includeArchived: boolean;
  search: string;
  kind: string;
  priority: string;
  boardId: string;
  relation: BacklogRelationFilter;
}

export const defaultBacklogFilters: BacklogFilterState = {
  includeArchived: false,
  search: '',
  kind: 'all',
  priority: 'all',
  boardId: 'all',
  relation: 'all',
};

export function getEpicId(item: BacklogItem, items: BacklogItem[]) {
  if (item.kind === 1) return item.id;
  const byId = new Map(items.map((current) => [current.id, current]));
  const visited = new Set<string>();
  let parentId = item.parentId;

  while (parentId && !visited.has(parentId)) {
    visited.add(parentId);
    const parent = byId.get(parentId);
    if (!parent) return undefined;
    if (parent.kind === 1) return parent.id;
    parentId = parent.parentId;
  }

  return undefined;
}

export function getBacklogRelations(item: BacklogItem) {
  const dependencies = (item.links ?? []).filter((link) =>
    (!link.isIncoming && link.type === 1) || (link.isIncoming && link.type === 2));
  const blocking = (item.links ?? []).filter((link) =>
    (!link.isIncoming && link.type === 2) || (link.isIncoming && link.type === 1));
  return {
    dependencies,
    blocking,
    blocked: item.isBlocked ?? dependencies.some((link) => link.isOpen),
  };
}

export function filterBacklogItems(items: BacklogItem[], filters: BacklogFilterState) {
  const search = filters.search.trim().toLocaleLowerCase('pt-BR');
  return items.filter((item) => {
    // Arquivada só aparece quando explicitamente pedido.
    if (item.isArchived && !filters.includeArchived) return false;
    if (search) {
      const haystack = `${item.number ?? ''} ${item.title} ${item.boardName} ${item.requesterName ?? ''}`
        .toLocaleLowerCase('pt-BR');
      if (!haystack.includes(search)) return false;
    }
    if (filters.kind !== 'all' && item.kind !== Number(filters.kind)) return false;
    if (filters.priority !== 'all' && item.priority !== Number(filters.priority)) return false;
    if (filters.boardId !== 'all' && item.boardId !== filters.boardId) return false;

    const relations = getBacklogRelations(item);
    if (filters.relation === 'blocked' && !relations.blocked) return false;
    if (filters.relation === 'dependencies' && relations.dependencies.length === 0) return false;
    if (filters.relation === 'unparented' && (item.kind === 1 || getEpicId(item, items))) return false;
    return true;
  });
}

export function getBacklogGroupLabel(
  item: BacklogItem,
  items: BacklogItem[],
  groupBy: BacklogGroupBy,
) {
  if (groupBy === 'kind') return kindNames[item.kind] ?? 'Outro tipo';
  if (groupBy === 'priority') return priorityNames[item.priority] ?? 'Sem prioridade';
  if (groupBy === 'board') return item.boardName;
  if (groupBy === 'epic') {
    const epicId = getEpicId(item, items);
    return items.find((candidate) => candidate.id === epicId)?.title ?? 'Sem épico';
  }
  return '';
}

export function sortBacklogItems(
  items: BacklogItem[],
  allItems: BacklogItem[],
  groupBy: BacklogGroupBy,
) {
  return [...items].sort((left, right) => {
    if (groupBy !== 'none') {
      const grouped = getBacklogGroupLabel(left, allItems, groupBy)
        .localeCompare(getBacklogGroupLabel(right, allItems, groupBy), 'pt-BR');
      if (grouped) return grouped;
    }
    return left.rank - right.rank;
  });
}
