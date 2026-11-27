import { describe, expect, it } from 'vitest';
import { bodyWithCardLine, findPullRequestSource, hasTaskPrefix } from './pullRequestEligibility';
import type { PullRequestRepositoryView, TaskGitLinkView } from './pullRequestTypes';

const repo: PullRequestRepositoryView = { id: 'repo-1', repositoryFullName: 'qa/metodo', defaultBranch: 'main', enabled: true, mode: 'app' };
const branch = (extra: Partial<TaskGitLinkView> = {}): TaskGitLinkView =>
  ({ id: 'link-1', integrationId: 'repo-1', branchName: 'task/T-0028-x', branchState: 'remote', observationId: null, pullRequestNumber: null, ...extra });

describe('findPullRequestSource (RF3, CA-3)', () => {
  it('branch enviada sem PR num repositório habilitado é elegível, com a base cadastrada e o modo', () => {
    expect(findPullRequestSource([branch()], [repo])).toEqual({ linkId: 'link-1', branch: 'task/T-0028-x', repositoryFullName: 'qa/metodo', defaultBranch: 'main', mode: 'app' });
    expect(findPullRequestSource([branch()], [{ ...repo, mode: 'poll', defaultBranch: 'prod' }])?.mode).toBe('poll');
    expect(findPullRequestSource([branch()], [{ ...repo, mode: undefined }])?.mode).toBe('app');
  });

  it('branch local, repositório desabilitado ou de fora não são elegíveis', () => {
    expect(findPullRequestSource([branch({ branchState: 'local' })], [repo])).toBeNull();
    expect(findPullRequestSource([branch()], [{ ...repo, enabled: false }])).toBeNull();
    expect(findPullRequestSource([branch({ integrationId: 'outro' })], [repo])).toBeNull();
    expect(findPullRequestSource([], [repo])).toBeNull();
    expect(findPullRequestSource(null, null)).toBeNull();
  });

  it('PR aberto (inclusive rascunho, que é "open") vinculado no mesmo repositório esconde o botão; PR fechado não', () => {
    const pr = (state: string): TaskGitLinkView => ({ id: 'link-pr', integrationId: 'repo-1', branchName: null, observationId: 'obs', pullRequestNumber: 7, pullRequestState: state });
    expect(findPullRequestSource([branch(), pr('open')], [repo])).toBeNull();
    expect(findPullRequestSource([branch(), pr('closed')], [repo])?.linkId).toBe('link-1');
  });
});

describe('texto do PR', () => {
  it('prefixo e linha do card', () => {
    expect(hasTaskPrefix('[T-0028] Título', 'T-0028')).toBe(true);
    expect(hasTaskPrefix('Título', 'T-0028')).toBe(false);
    expect(bodyWithCardLine('Corpo\n', 'https://x/c')).toBe('Corpo\n\nTask no Prisma: https://x/c');
    expect(bodyWithCardLine('Corpo\n\nTask no Prisma: https://x/c', 'https://x/c')).toBe('Corpo\n\nTask no Prisma: https://x/c');
    expect(bodyWithCardLine('', 'https://x/c')).toBe('Task no Prisma: https://x/c');
  });
});
