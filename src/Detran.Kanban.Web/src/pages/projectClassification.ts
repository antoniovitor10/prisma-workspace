export const workNatureOptions = [[1, 'Projeto'], [2, 'Melhoria'], [3, 'Sustentação']] as const;

export const workTypeOptions = [
  [1, 'Desenvolvimento'],
  [2, 'Infraestrutura'],
  [3, 'Banco de Dados'],
  [4, 'Suporte'],
  [5, 'Segurança'],
  [6, 'Dados/BI'],
  [7, 'Integração'],
  [8, 'Documentação'],
  [9, 'Gestão'],
] as const;

export const projectClassificationLabel = (
  options: ReadonlyArray<readonly [number, string]>,
  value?: number,
) => options.find(([id]) => id === value)?.[1] ?? 'Não classificado';
