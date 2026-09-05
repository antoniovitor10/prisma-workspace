import type { BacklogItem } from '../../types/scrum';

export interface BacklogHierarchyResult {
  items: BacklogItem[];
  errors: string[];
}

export function includeBacklogAncestors(matches: BacklogItem[], allItems: BacklogItem[]) {
  const byId = new Map(allItems.map(item => [item.id, item]));
  const included = new Map(matches.map(item => [item.id, item]));

  for (const match of matches) {
    const visited = new Set<string>([match.id]);
    let parentId = match.parentId;
    while (parentId && !visited.has(parentId)) {
      visited.add(parentId);
      const parent = byId.get(parentId);
      if (!parent) break;
      included.set(parent.id, parent);
      parentId = parent.parentId;
    }
  }

  return [...included.values()];
}

export function flattenBacklogHierarchy(
  includedItems: BacklogItem[],
  allItems: BacklogItem[],
  collapsedIds: ReadonlySet<string>,
): BacklogHierarchyResult {
  const included = new Map(includedItems.map(item => [item.id, item]));
  const allById = new Map(allItems.map(item => [item.id, item]));
  const children = new Map<string, BacklogItem[]>();
  const roots: BacklogItem[] = [];
  const errors: string[] = [];
  const compare = (left: BacklogItem, right: BacklogItem) =>
    left.rank - right.rank || left.title.localeCompare(right.title, 'pt-BR');

  for (const item of includedItems) {
    if (!item.parentId) {
      roots.push(item);
      continue;
    }
    if (!allById.has(item.parentId)) {
      errors.push(`O item #${item.number ?? item.id} possui um pai inexistente.`);
      roots.push(item);
      continue;
    }
    if (!included.has(item.parentId)) {
      roots.push(item);
      continue;
    }
    const siblings = children.get(item.parentId) ?? [];
    siblings.push(item);
    children.set(item.parentId, siblings);
  }

  roots.sort(compare);
  children.forEach(items => items.sort(compare));
  const result: BacklogItem[] = [];
  const visited = new Set<string>();

  const markCollapsedDescendants = (itemId: string) => {
    for (const child of children.get(itemId) ?? []) {
      if (!visited.add(child.id)) continue;
      markCollapsedDescendants(child.id);
    }
  };

  const visit = (item: BacklogItem, path: Set<string>) => {
    if (path.has(item.id)) {
      errors.push(`Ciclo hierárquico detectado no item #${item.number ?? item.id}.`);
      return;
    }
    if (visited.has(item.id)) return;
    visited.add(item.id);
    result.push(item);
    if (collapsedIds.has(item.id)) {
      markCollapsedDescendants(item.id);
      return;
    }
    const nextPath = new Set(path).add(item.id);
    for (const child of children.get(item.id) ?? []) visit(child, nextPath);
  };

  roots.forEach(root => visit(root, new Set()));
  for (const item of [...includedItems].sort(compare)) {
    if (visited.has(item.id)) continue;
    errors.push(`Ciclo hierárquico detectado no ramo de #${item.number ?? item.id}.`);
    visit(item, new Set());
  }

  return { items: result, errors: [...new Set(errors)] };
}
