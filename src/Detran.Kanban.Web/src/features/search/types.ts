export interface GlobalSearchHit {
  kind: 'tasks'|'requests'|'projects'|'users'|'teams'|'sprints'|'wiki';
  id: string;
  title: string;
  subtitle: string;
  path: string;
  updatedAt?: string | null;
}

export interface GlobalSearchResponse {
  query: string;
  groups: Partial<Record<GlobalSearchHit['kind'], GlobalSearchHit[]>>;
  total: number;
}
