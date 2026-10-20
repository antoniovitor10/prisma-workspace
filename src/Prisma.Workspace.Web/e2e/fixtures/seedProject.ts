/** Escolhe o seed explícito, sem confundir projetos criados por outros testes. */
export function selectSeedProject<T extends { key: string }>(projects: T[]): T {
  const project = projects.find(item => item.key === 'DEMO')
    ?? projects.find(item => item.key === 'INFRACOES');
  if (!project) throw new Error('Projeto seed DEMO ou INFRACOES não encontrado para E2E.');
  return project;
}
