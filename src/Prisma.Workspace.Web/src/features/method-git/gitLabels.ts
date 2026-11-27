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

const pollStatusMessages: Record<string, string> = {
  git_token_invalid: 'O GitHub recusou o token de leitura: ele está inválido, vencido ou foi revogado. Cadastre um novo token da organização.',
  git_token_forbidden: 'O GitHub recusou o acesso: falta a aprovação do token pela organização ou a leitura deste repositório.',
  git_repository_not_found: 'Repositório não encontrado para este token. Confira se o token inclui o repositório.',
  git_credential_revoked: 'O token de leitura desta organização foi revogado no Prisma. Cadastre um novo token.',
  git_repository_mismatch: 'O nome do repositório agora aponta para outro repositório no GitHub. Confira o cadastro.',
  git_rate_limited: 'Limite de consultas do GitHub atingido. A próxima tentativa fica para depois do prazo do GitHub.',
};

/** Mensagem da última consulta do modo consulta (sem o texto do GitHub); nulo quando a última deu certo. */
export function gitPollStatusMessage(status: string | null | undefined): string | null {
  if (!status || status === 'ok') return null;
  return pollStatusMessages[status] ?? 'A última consulta ao GitHub falhou. Tente de novo mais tarde.';
}

export const formatDateTime = (value: string) => new Date(value).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
