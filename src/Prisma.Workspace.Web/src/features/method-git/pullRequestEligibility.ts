import type { WorkItemGitSummary } from './gitLabels';
import type { GitIntegrationMode, PullRequestSource } from './pullRequestTypes';
import type { TaskGitView } from './types';

export const CARD_LINE_PREFIX = 'Task no Prisma: ';

/** Link profundo do card, no mesmo formato que a API usa no corpo do PR. */
export function cardLink(projectId: string, workItemId: string, origin = window.location.origin): string {
  return `${origin}/projects/${encodeURIComponent(projectId)}/backlog?item=${encodeURIComponent(workItemId)}`;
}

export function hasTaskPrefix(title: string, methodTaskId: string): boolean {
  return title.trim().startsWith(`[${methodTaskId}]`);
}

/** Corpo completo como vai ao GitHub: o texto e, no fim, a linha do card (sem repetir se já termina nela). */
export function bodyWithCardLine(body: string, link: string): string {
  const line = `${CARD_LINE_PREFIX}${link}`;
  const trimmed = body.trimEnd();
  if (trimmed.endsWith(line)) return trimmed;
  return trimmed ? `${trimmed}\n\n${line}` : line;
}

const OPEN_PR_STATES = new Set(['open', 'draft', 'approved', 'changes_requested']);
const mode = (value: string | null | undefined): GitIntegrationMode => (value === 'poll' ? 'poll' : 'app');

/** Card do quadro (projeção da T-0005): branch enviada, vínculo de branch conhecido e sem PR aberto (nem rascunho). */
export function sourceFromCardGit(git: WorkItemGitSummary | null | undefined): PullRequestSource | null {
  if (!git?.linkId || !git.branch || git.branchState !== 'remote' || !git.defaultBranch) return null;
  if (git.prNumber != null && git.prState != null && OPEN_PR_STATES.has(git.prState)) return null;
  return { linkId: git.linkId, branch: git.branch, repositoryFullName: git.repoFullName, defaultBranch: git.defaultBranch, mode: mode(git.integrationMode) };
}

/** Aba Git (T-0006): primeira branch enviada de repositório habilitado sem PR aberto para essa branch. */
export function sourceFromTaskGit(view: TaskGitView | null | undefined): PullRequestSource | null {
  for (const branch of view?.branches ?? []) {
    if (!branch.linkId || branch.branchState !== 'remote') continue;
    const repository = view!.repositories.find(repo => repo.id === branch.integrationId);
    if (!repository?.defaultBranch) continue;
    const open = view!.pullRequests.some(pr => pr.integrationId === branch.integrationId && pr.headBranch === branch.branchName && pr.state === 'open');
    if (open) continue;
    return { linkId: branch.linkId, branch: branch.branchName, repositoryFullName: branch.repositoryFullName,
      defaultBranch: repository.defaultBranch, mode: mode(repository.mode) };
  }
  return null;
}

/** "T-0001 · Título" vira "Título" (o card do método leva o ID no título). */
export function taskTitleWithoutId(title: string, methodTaskId: string): string {
  const prefix = `${methodTaskId} · `;
  return title.startsWith(prefix) ? title.slice(prefix.length) : title;
}
