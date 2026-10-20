export interface CreatedWorkItem {
  id: string;
  createdAt: string;
}
export type KanbanCardSort = 'position' | 'priority' | 'due' | 'title' | 'created';
export interface SortableKanbanWorkItem extends CreatedWorkItem {
  position: number;
  priority: number;
  dueDate?: string | null;
  title: string;
}

export function compareNewestWorkItems(a: CreatedWorkItem, b: CreatedWorkItem): number {
  return b.createdAt.localeCompare(a.createdAt) || a.id.localeCompare(b.id);
}
/** Ordena apenas o conjunto recebido; no Kanban esse conjunto é uma coluna. */
export function compareKanbanWorkItems(
  a: SortableKanbanWorkItem,
  b: SortableKanbanWorkItem,
  sort: KanbanCardSort = 'position',
): number {
  if (sort === 'priority') return b.priority - a.priority || a.position - b.position;
  if (sort === 'due') return (a.dueDate || '9999-12-31').localeCompare(b.dueDate || '9999-12-31') || a.position - b.position;
  if (sort === 'title') return a.title.localeCompare(b.title, 'pt-BR') || a.position - b.position;
  if (sort === 'created') return compareNewestWorkItems(a, b);
  return a.position - b.position || a.id.localeCompare(b.id);
}

/** Insere antes da primeira posição, inclusive quando a coluna já chegou a zero. */
export function positionBeforeColumn(items: ReadonlyArray<{ position: number }>): number {
  return items.length ? items.reduce((first, item) => Math.min(first, item.position), Infinity) - 100 : 100;
}
