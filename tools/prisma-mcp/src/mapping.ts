// Valores numéricos de StageCategory na API REST existente.
export type StageCategory = 1 | 2 | 3 | 4 | 5;
export const STATUS_MAP = {
  backlog: { name: 'Backlog', category: 1 },
  pronta: { name: 'Pronta', category: 2 },
  'em-desenvolvimento': { name: 'Em desenvolvimento', category: 3 },
  'em-revisao': { name: 'Em revisão', category: 4 },
  'em-teste': { name: 'Em teste', category: 4 },
  concluida: { name: 'Concluída', category: 5 },
  bloqueada: { name: 'Bloqueada', category: 3 },
} as const satisfies Record<string, { name: string; category: StageCategory }>;
export type MetodoStatus = keyof typeof STATUS_MAP;
export const STATUSES = Object.keys(STATUS_MAP) as MetodoStatus[];
export function isMetodoStatus(value: unknown): value is MetodoStatus {
  return typeof value === 'string' && Object.hasOwn(STATUS_MAP, value);
}
export const FIELD_NAMES = { id: 'ID do metodo', hu: 'HU', arquivo: 'Arquivo', cobre: 'Cobre', risco: 'Risco', pr: 'PR' } as const;
