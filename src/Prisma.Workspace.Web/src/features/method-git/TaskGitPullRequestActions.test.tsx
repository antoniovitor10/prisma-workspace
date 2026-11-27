import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { ThemeProvider } from 'styled-components';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { theme } from '../../styles/theme';
import { CardOpenPullRequest } from './CardOpenPullRequest';
import { sourceFromCardGit, sourceFromTaskGit, taskTitleWithoutId } from './pullRequestEligibility';
import { gitTimelineText } from './taskGit';
import { TaskGitPullRequestActions } from './TaskGitPullRequestActions';
import type { WorkItemGitSummary } from './gitLabels';
import type { TaskGitPullRequest, TaskGitView } from './types';

const view = (extra: Partial<TaskGitView> = {}): TaskGitView => ({
  workItemId: 'item-1', methodTaskId: 'T-0028', canLink: true, canOpenPullRequest: true,
  repositories: [{ id: 'repo-1', repositoryFullName: 'qa/metodo', mode: 'app', lastPolledAtUtc: null, lastPollStatus: null, defaultBranch: 'main' }],
  branches: [{ linkId: 'link-1', integrationId: 'repo-1', repositoryFullName: 'qa/metodo', branchName: 'task/T-0028-x', branchState: 'remote',
    lastSeenAtUtc: null, source: 'explicit', createdAtUtc: '2026-11-27T10:00:00Z', url: 'https://github.com/qa/metodo/tree/task/T-0028-x' }],
  pullRequests: [], timeline: [], pullRequestDraft: null, ...extra,
});
const pr = (state: string): TaskGitPullRequest => ({ linkId: 'pr', integrationId: 'repo-1', repositoryFullName: 'qa/metodo', number: 7, title: 'PR', state,
  isDraft: false, merged: false, approvalsCount: 0, changesRequested: false, url: 'u', headBranch: 'task/T-0028-x', baseBranch: 'main',
  authorName: null, source: 'explicit', updatedAtUtc: 'x', linkedAtUtc: 'x' });
const git: WorkItemGitSummary = { repoName: 'metodo', repoFullName: 'qa/metodo', branch: 'task/T-0028-x', branchState: 'remote',
  prNumber: null, prState: null, prUrl: null, linkId: 'link-1', integrationMode: 'app', defaultBranch: 'main', methodTaskId: 'T-0028' };
const wrap = (node: ReactNode) => render(<ThemeProvider theme={theme}><QueryClientProvider client={new QueryClient()}>{node}</QueryClientProvider></ThemeProvider>);
afterEach(() => cleanup());

describe('aba Git: rascunho e botão (CA-1, CA-2, CA-8)', () => {
  it('mostra "Rascunho do PR: gravado em … por …" e o botão para quem pode', () => {
    wrap(<TaskGitPullRequestActions projectId="p" view={view({ pullRequestDraft: { updatedAtUtc: '2026-11-27T12:00:00Z', actorName: 'Agente SQL' } })} taskTitle="T-0028 · Botão" />);
    expect(screen.getByText(/Rascunho do PR: gravado em/)).toHaveTextContent('por Agente SQL');
    expect(screen.getByRole('button', { name: 'Abrir PR da task T-0028' })).toBeInTheDocument();
  });

  it('sem rascunho e sem permissão de abrir: nada aparece', () => {
    wrap(<TaskGitPullRequestActions projectId="p" view={view({ canOpenPullRequest: false })} />);
    expect(screen.queryByRole('button', { name: /Abrir PR/ })).toBeNull();
    expect(screen.queryByText(/Rascunho do PR/)).toBeNull();
  });

  it('quem só lê vê o rascunho mas não o botão', () => {
    wrap(<TaskGitPullRequestActions projectId="p" view={view({ canLink: false, canOpenPullRequest: false,
      pullRequestDraft: { updatedAtUtc: '2026-11-27T12:00:00Z', actorName: 'Agente' } })} />);
    expect(screen.getByText(/Rascunho do PR: gravado em/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Abrir PR/ })).toBeNull();
  });

  it('modo consulta: "Abrir no GitHub" para quem pode editar', () => {
    wrap(<TaskGitPullRequestActions projectId="p" view={view({ canOpenPullRequest: false,
      repositories: [{ id: 'repo-1', repositoryFullName: 'qa/metodo', mode: 'poll', lastPolledAtUtc: null, lastPollStatus: null, defaultBranch: 'prod' }] })} />);
    expect(screen.getByRole('button', { name: 'Abrir PR no GitHub da task T-0028' })).toBeInTheDocument();
  });

  it('linha do tempo: rascunho sem o corpo e PR aberto pelo Prisma', () => {
    const base = { atUtc: 'x', channel: 'mcp', repositoryFullName: null, branch: null, pullRequestNumber: null, url: null, factType: null, fromStatus: null, toStatus: null };
    expect(gitTimelineText({ ...base, kind: 'git_pr_draft_saved', actorName: 'Agente', title: 'Corrige X' })).toBe('Rascunho do PR gravado: Corrige X (corpo atualizado)');
    expect(gitTimelineText({ ...base, kind: 'git_pr_opened', actorName: 'Ana', pullRequestNumber: 7, draft: false })).toBe('PR #7 aberto pelo Prisma por Ana');
    expect(gitTimelineText({ ...base, kind: 'git_pr_opened', actorName: 'Ana', pullRequestNumber: 7, draft: true })).toBe('PR #7 aberto pelo Prisma por Ana como rascunho');
  });
});

describe('card do quadro (CA-1, CA-3, CA-8)', () => {
  it('com branch enviada e permissão o card tem o botão; o clique pede o modal ao quadro (fora do card)', () => {
    const onOpenDialog = vi.fn();
    wrap(<CardOpenPullRequest projectId="p" workItemId="item-1" title="T-0028 · Botão" git={git} canEdit onOpenDialog={onOpenDialog} />);
    fireEvent.click(screen.getByRole('button', { name: 'Abrir PR da task T-0028' }));
    expect(onOpenDialog).toHaveBeenCalledWith({ workItemId: 'item-1', methodTaskId: 'T-0028', title: 'Botão' },
      { linkId: 'link-1', branch: 'task/T-0028-x', repositoryFullName: 'qa/metodo', defaultBranch: 'main', mode: 'app' });
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('sem permissão, com branch local ou com PR aberto não tem', () => {
    wrap(<CardOpenPullRequest projectId="p" workItemId="item-1" title="t" git={git} canEdit={false} onOpenDialog={vi.fn()} />);
    expect(screen.queryByRole('button', { name: /Abrir PR/ })).toBeNull();
    cleanup();
    wrap(<CardOpenPullRequest projectId="p" workItemId="item-1" title="t" git={{ ...git, branchState: 'local' }} canEdit onOpenDialog={vi.fn()} />);
    expect(screen.queryByRole('button', { name: /Abrir PR/ })).toBeNull();
    cleanup();
    wrap(<CardOpenPullRequest projectId="p" workItemId="item-1" title="t" git={{ ...git, prNumber: 3, prState: 'draft' }} canEdit onOpenDialog={vi.fn()} />);
    expect(screen.queryByRole('button', { name: /Abrir PR/ })).toBeNull();
  });
});

describe('regras de exibição', () => {
  it('card: branch enviada sem PR é elegível com a base cadastrada; PR aberto, rascunho ou em revisão esconde', () => {
    expect(sourceFromCardGit(git)).toEqual({ linkId: 'link-1', branch: 'task/T-0028-x', repositoryFullName: 'qa/metodo', defaultBranch: 'main', mode: 'app' });
    expect(sourceFromCardGit({ ...git, integrationMode: 'poll', defaultBranch: 'prod' })?.mode).toBe('poll');
    for (const prState of ['open', 'draft', 'approved', 'changes_requested'] as const)
      expect(sourceFromCardGit({ ...git, prNumber: 7, prState })).toBeNull();
    expect(sourceFromCardGit({ ...git, prNumber: 7, prState: 'closed' })?.linkId).toBe('link-1');
    expect(sourceFromCardGit({ ...git, linkId: null })).toBeNull();
    expect(sourceFromCardGit({ ...git, defaultBranch: null })).toBeNull();
    expect(sourceFromCardGit(null)).toBeNull();
  });

  it('aba Git: PR aberto da mesma branch esconde; branch local e repositório fora da lista também', () => {
    expect(sourceFromTaskGit(view())?.linkId).toBe('link-1');
    expect(sourceFromTaskGit(view({ pullRequests: [pr('open')] }))).toBeNull();
    expect(sourceFromTaskGit(view({ pullRequests: [pr('closed')] }))?.linkId).toBe('link-1');
    expect(sourceFromTaskGit(view({ branches: [{ ...view().branches[0], branchState: 'local' }] }))).toBeNull();
    expect(sourceFromTaskGit(view({ repositories: [] }))).toBeNull();
  });

  it('título do card sem o ID', () => {
    expect(taskTitleWithoutId('T-0028 · Botão', 'T-0028')).toBe('Botão');
    expect(taskTitleWithoutId('Botão', 'T-0028')).toBe('Botão');
  });
});
