import type { PullRequestRepositoryView, PullRequestSource, TaskGitLinkView } from './pullRequestTypes';

export const CARD_LINE_PREFIX = 'Task no Prisma: ';

/**
 * RF3 (CA-1, CA-3): a branch da task pode virar PR quando já foi enviada ao GitHub (branchState "remote"), está num
 * repositório habilitado do projeto e não há PR aberto (nem rascunho) vinculado nesse repositório.
 * O modo do repositório decide o botão: "app" abre o PR pelo Prisma; "poll" leva à página de comparação do GitHub.
 */
export function findPullRequestSource(links: readonly TaskGitLinkView[] | null | undefined,
  repositories: readonly PullRequestRepositoryView[] | null | undefined): PullRequestSource | null {
  for (const link of links ?? []) {
    if (!link.branchName || link.observationId || link.pullRequestNumber != null || link.branchState !== 'remote') continue;
    const repository = (repositories ?? []).find(repo => repo.id === link.integrationId && repo.enabled);
    if (!repository) continue;
    const hasOpenPullRequest = (links ?? []).some(other => other.integrationId === link.integrationId
      && other.pullRequestNumber != null && other.pullRequestState === 'open');
    if (hasOpenPullRequest) continue;
    return { linkId: link.id, branch: link.branchName, repositoryFullName: repository.repositoryFullName,
      defaultBranch: repository.defaultBranch, mode: repository.mode ?? 'app' };
  }
  return null;
}

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
