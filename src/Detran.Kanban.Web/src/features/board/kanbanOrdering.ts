export interface CreatedWorkItem {
  id: string;
  createdAt: string;
}

export function compareNewestWorkItems(a: CreatedWorkItem, b: CreatedWorkItem): number {
  return b.createdAt.localeCompare(a.createdAt) || a.id.localeCompare(b.id);
}
