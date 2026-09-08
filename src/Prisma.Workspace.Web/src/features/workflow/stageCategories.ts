/**
 * Classificação funcional de uma coluna do quadro, espelhando o enum StageCategory
 * do domínio. É ela que define se uma tarefa na coluna conta como concluída.
 *
 * Fonte única: a configuração de workflow do projeto e a criação de coluna no
 * Kanban precisam oferecer exatamente as mesmas opções.
 */
export const StageCategory = {
  Backlog: 1,
  Ready: 2,
  InProgress: 3,
  Review: 4,
  Done: 5,
} as const;

export type StageCategoryValue = (typeof StageCategory)[keyof typeof StageCategory];

export const stageCategoryOptions = [
  [StageCategory.Backlog, 'Backlog'],
  [StageCategory.Ready, 'Pronta'],
  [StageCategory.InProgress, 'Em andamento'],
  [StageCategory.Review, 'Revisão'],
  [StageCategory.Done, 'Concluída'],
] as const;
