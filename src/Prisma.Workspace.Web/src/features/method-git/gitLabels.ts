/** Resumo Git do cartão, calculado no servidor (WorkItemCardProjection, HU-0002 RF1 a RF3). */
export type GitPrState = 'open' | 'draft' | 'approved' | 'changes_requested' | 'merged' | 'closed';
export type GitBranchState = 'local' | 'remote';

export interface WorkItemGitSummary {
  repoName: string;
  repoFullName: string;
  branch: string | null;
  branchState: GitBranchState;
  prNumber: number | null;
  prState: GitPrState | null;
  prUrl: string | null;
}

const prLabels: Record<GitPrState, string> = {
  open: 'aberto',
  draft: 'rascunho',
  approved: 'aprovado',
  changes_requested: 'mudanças pedidas',
  merged: 'mergeado',
  closed: 'fechado',
};

/** Rótulo em pt-BR do estado do PR; estado desconhecido aparece como veio. */
export function gitPrStateLabel(state: string | null | undefined): string {
  if (!state) return '';
  return prLabels[state as GitPrState] ?? state;
}

/**
 * Estado exibido de um PR a partir da observação (mesma regra do servidor, D4):
 * rascunho e revisões só valem com o PR aberto.
 */
export function gitPrState(input: { state: string; isDraft: boolean; merged: boolean; approvalsCount: number; changesRequested: boolean }): GitPrState {
  if (input.state === 'closed') return input.merged ? 'merged' : 'closed';
  if (input.isDraft) return 'draft';
  if (input.changesRequested) return 'changes_requested';
  return input.approvalsCount > 0 ? 'approved' : 'open';
}

export function gitBranchStateLabel(state: string | null | undefined): string {
  return state === 'local' ? 'não enviada' : 'enviada';
}

/** Link da branch no GitHub; cada segmento é codificado, a barra continua separando os níveis. */
export function gitBranchUrl(repoFullName: string, branch: string): string {
  return `https://github.com/${repoFullName}/tree/${branch.split('/').map(encodeURIComponent).join('/')}`;
}
