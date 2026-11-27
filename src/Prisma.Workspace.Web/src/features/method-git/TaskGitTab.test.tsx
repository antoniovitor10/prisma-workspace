import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ThemeProvider } from 'styled-components';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { api } from '../../services/api';
import { theme } from '../../styles/theme';
import type { WorkItemDetails } from '../../types/scrum';
import { TaskDetailDrawer } from '../../components/TaskDetailDrawer';
import { methodApi } from './client';
import { TaskGitTab } from './TaskGitTab';
import type { TaskGitView } from './types';

afterEach(() => { cleanup(); vi.restoreAllMocks(); });

const view = (over: Partial<TaskGitView> = {}): TaskGitView => ({
  workItemId: 'item-1', methodTaskId: 'T-0006', canLink: true,
  repositories: [
    { id: 'repo-1', repositoryFullName: 'qa/metodo', mode: 'app', lastPolledAtUtc: null, lastPollStatus: null },
    { id: 'repo-2', repositoryFullName: 'detranse/canais-mobile', mode: 'poll', lastPolledAtUtc: null, lastPollStatus: null },
  ],
  branches: [
    { linkId: 'l1', integrationId: 'repo-2', repositoryFullName: 'detranse/canais-mobile', branchName: 'feat/login social', branchState: 'local',
      lastSeenAtUtc: null, source: 'explicit', createdAtUtc: '2026-11-20T10:00:00Z', url: 'https://github.com/detranse/canais-mobile/tree/feat/login%20social' },
    { linkId: null, integrationId: 'repo-1', repositoryFullName: 'qa/metodo', branchName: 'task/T-0006-aba', branchState: 'remote',
      lastSeenAtUtc: '2026-11-20T11:00:00Z', source: 'branch', createdAtUtc: '2026-11-20T11:00:00Z', url: 'https://github.com/qa/metodo/tree/task/T-0006-aba' },
  ],
  pullRequests: [
    { linkId: 'l2', integrationId: 'repo-1', repositoryFullName: 'qa/metodo', number: 42, title: 'Aba Git na tarefa', state: 'open',
      isDraft: false, merged: false, approvalsCount: 2, changesRequested: true, url: 'https://github.com/qa/metodo/pull/42',
      headBranch: 'task/T-0006-aba', baseBranch: 'main', authorName: 'dev-e2e', source: 'branch',
      updatedAtUtc: '2026-11-20T12:00:00Z', linkedAtUtc: '2026-11-20T11:00:00Z' },
  ],
  timeline: [
    { kind: 'git_branch_linked', atUtc: '2026-11-20T10:00:00Z', actorName: 'Agente do método', channel: 'mcp', repositoryFullName: 'detranse/canais-mobile',
      branch: 'feat/login social', pullRequestNumber: null, url: null, factType: null, fromStatus: null, toStatus: null },
    { kind: 'git_branch_pushed', atUtc: '2026-11-20T10:30:00Z', actorName: 'dev-e2e', channel: 'consulta', repositoryFullName: 'detranse/canais-mobile',
      branch: 'feat/login social', pullRequestNumber: null, url: null, factType: null, fromStatus: null, toStatus: null },
    { kind: 'git_fact', atUtc: '2026-11-20T11:00:00Z', actorName: 'dev-e2e', channel: 'webhook', repositoryFullName: 'qa/metodo',
      branch: null, pullRequestNumber: 42, url: null, factType: 'pull_request_opened', fromStatus: 'em-desenvolvimento', toStatus: 'em-revisao' },
    { kind: 'git_fact', atUtc: '2026-11-20T12:00:00Z', actorName: 'revisor', channel: 'webhook', repositoryFullName: 'qa/metodo',
      branch: null, pullRequestNumber: 42, url: null, factType: 'review_changes_requested', fromStatus: 'em-revisao', toStatus: 'em-desenvolvimento' },
    { kind: 'git_pr_merged', atUtc: '2026-11-20T13:00:00Z', actorName: null, channel: null, repositoryFullName: 'qa/metodo',
      branch: 'task/T-0006-aba', pullRequestNumber: 42, url: null, factType: null, fromStatus: null, toStatus: null },
  ],
  ...over,
});

function renderTab(onLinked = vi.fn()) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  render(<QueryClientProvider client={client}><ThemeProvider theme={theme}>
    <TaskGitTab projectId="project-1" workItemId="item-1" onLinked={onLinked} />
  </ThemeProvider></QueryClientProvider>);
  return onLinked;
}

describe('aba Git da tarefa (HU-0002, T-0006)', () => {
  it('CA-5: o detalhe da tarefa do método ganha a aba Git, que abre com branches e PRs', async () => {
    const details: WorkItemDetails = {
      id: 'item-1', number: 6, reference: 'M-6', boardId: 'board', boardName: 'Método', projectId: 'project-1',
      kind: 5, origin: 1, title: 'Aba Git', priority: 1, participants: [], createdAt: '2026-11-20T10:00:00Z', updatedAt: '2026-11-20T10:00:00Z',
      realizedHours: 0, tags: [], isArchived: false, checklist: [], subtasks: [], attachmentsCount: 0, commentsCount: 0,
      followerIds: [], isFollowing: false, links: [], customFields: [], version: 'v1',
    };
    vi.spyOn(api, 'getToken').mockReturnValue('jwt');
    vi.spyOn(api, 'getWorkItemDetails').mockResolvedValue(details);
    for (const name of ['getBoardStages', 'getAssignableUsers', 'getAttachments', 'getTaskTypes', 'getTags', 'getChecklist', 'getComments', 'getTaskEvents', 'getBoards', 'getProjectSprints'] as const)
      vi.spyOn(api, name).mockResolvedValue([] as never);
    vi.spyOn(api, 'getProject').mockResolvedValue({ id: 'project-1', teams: [] } as never);
    const taskGit = vi.spyOn(methodApi, 'taskGit').mockResolvedValue(view());
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(<QueryClientProvider client={client}><ThemeProvider theme={theme}><MemoryRouter>
      <TaskDetailDrawer projectKey="M" item={{ id: 'item-1', boardId: 'board', boardName: 'Método', kind: 5, title: 'Aba Git', priority: 1, rank: 1 }} onOpenChange={vi.fn()} />
    </MemoryRouter></ThemeProvider></QueryClientProvider>);
    const tab = await screen.findByRole('button', { name: 'Git' });
    expect(taskGit).toHaveBeenCalledWith('project-1', 'item-1');
    fireEvent.click(tab);
    expect(await screen.findByRole('link', { name: 'Abrir o PR #42 no GitHub' })).toHaveAttribute('href', 'https://github.com/qa/metodo/pull/42');
  });

  it('CA-5: cada branch tem repositório, estado e link; cada PR tem número, título, estado, revisões e link', async () => {
    vi.spyOn(methodApi, 'taskGit').mockResolvedValue(view());
    renderTab();
    const branches = await screen.findByRole('list', { name: 'Branches da tarefa' });
    const local = within(branches).getByRole('link', { name: 'Abrir a branch feat/login social no GitHub' });
    expect(local).toHaveAttribute('href', 'https://github.com/detranse/canais-mobile/tree/feat/login%20social');
    expect(local).toHaveAttribute('target', '_blank');
    expect(local.closest('li')).toHaveTextContent('detranse/canais-mobile');
    expect(local.closest('li')).toHaveTextContent('não enviada');
    expect(within(branches).getByRole('link', { name: 'Abrir a branch task/T-0006-aba no GitHub' }).closest('li')).toHaveTextContent('enviada');
    const prs = screen.getByRole('list', { name: 'PRs da tarefa' });
    const pr = within(prs).getByRole('link', { name: 'Abrir o PR #42 no GitHub' });
    expect(pr).toHaveTextContent('#42 · Aba Git na tarefa');
    const row = pr.closest('li')!;
    expect(row).toHaveTextContent('mudanças pedidas');
    expect(row).toHaveTextContent('Aprovações: 2');
    expect(row).toHaveTextContent('Mudanças pedidas: sim');
    expect(row).toHaveTextContent('task/T-0006-aba → main');
  });

  it('CA-6: a linha do tempo mostra os fatos em ordem, com data, autor e canal', async () => {
    vi.spyOn(methodApi, 'taskGit').mockResolvedValue(view());
    renderTab();
    const timeline = await screen.findByRole('list', { name: 'Linha do tempo do Git' });
    const items = within(timeline).getAllByRole('listitem').map(li => li.textContent ?? '');
    expect(items).toHaveLength(5);
    expect(items[0]).toMatch(/^Branch feat\/login social vinculada.*Agente do método · canal: MCP/);
    expect(items[1]).toMatch(/^Branch feat\/login social enviada ao GitHub.*dev-e2e · canal: consulta/);
    expect(items[2]).toMatch(/^PR #42 aberto; card para Em revisão.*canal: webhook/);
    expect(items[3]).toMatch(/^Mudanças pedidas no PR #42; card para Em desenvolvimento.*revisor/);
    expect(items[4]).toMatch(/^PR #42 mergeado.*Integração Git · canal: automático/);
    expect(within(timeline).getAllByRole('listitem')[0].querySelector('time')).toHaveAttribute('dateTime', '2026-11-20T10:00:00Z');
  });

  it('CA-7: digitar uma branch e colar a URL de um PR cria os vínculos e avisa o quadro', async () => {
    const taskGit = vi.spyOn(methodApi, 'taskGit').mockResolvedValue(view());
    const linkBranch = vi.spyOn(methodApi, 'linkBranch').mockResolvedValue({});
    const linkPr = vi.spyOn(methodApi, 'linkPullRequestUrl').mockResolvedValue({});
    const onLinked = renderTab();
    const branchForm = await screen.findByRole('form', { name: 'Vincular branch' });
    fireEvent.change(within(branchForm).getByLabelText('Repositório da branch'), { target: { value: 'detranse/canais-mobile' } });
    fireEvent.change(within(branchForm).getByLabelText('Nome da branch'), { target: { value: '  feat/pagamento  ' } });
    fireEvent.click(within(branchForm).getByRole('button', { name: 'Vincular branch' }));
    await waitFor(() => expect(linkBranch).toHaveBeenCalledWith('project-1', 'T-0006', 'detranse/canais-mobile', 'feat/pagamento'));
    expect(await screen.findByRole('status')).toHaveTextContent('Branch feat/pagamento vinculada.');
    await waitFor(() => expect(onLinked).toHaveBeenCalledTimes(1));
    expect(taskGit).toHaveBeenCalledTimes(2);

    const prForm = screen.getByRole('form', { name: 'Vincular PR' });
    fireEvent.change(within(prForm).getByLabelText('URL do PR'), { target: { value: 'https://github.com/qa/metodo/pull/77' } });
    fireEvent.submit(prForm);
    await waitFor(() => expect(linkPr).toHaveBeenCalledWith('project-1', 'T-0006', 'https://github.com/qa/metodo/pull/77'));
    await waitFor(() => expect(onLinked).toHaveBeenCalledTimes(2));
  });

  it('CA-7: recusa da API (outro repositório, PR inexistente) aparece como mensagem clara', async () => {
    vi.spyOn(methodApi, 'taskGit').mockResolvedValue(view());
    vi.spyOn(methodApi, 'linkPullRequestUrl').mockRejectedValue(new Error('URL não pertence a repositório autorizado e acessível deste projeto.'));
    const onLinked = renderTab();
    const prForm = await screen.findByRole('form', { name: 'Vincular PR' });
    fireEvent.change(within(prForm).getByLabelText('URL do PR'), { target: { value: 'https://github.com/outro/repo/pull/1' } });
    fireEvent.click(within(prForm).getByRole('button', { name: 'Vincular PR' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('URL não pertence a repositório autorizado e acessível deste projeto.');
    expect(onLinked).not.toHaveBeenCalled();
    expect(within(prForm).getByLabelText('URL do PR')).toHaveValue('https://github.com/outro/repo/pull/1');
  });

  it('CA-7: quem não pode editar a tarefa vê o Git, mas não o formulário', async () => {
    vi.spyOn(methodApi, 'taskGit').mockResolvedValue(view({ canLink: false }));
    renderTab();
    expect(await screen.findByText(/só quem pode editá-la vincula/)).toBeInTheDocument();
    expect(screen.queryByRole('form', { name: 'Vincular branch' })).not.toBeInTheDocument();
    expect(screen.queryByRole('form', { name: 'Vincular PR' })).not.toBeInTheDocument();
    expect(screen.getByRole('list', { name: 'PRs da tarefa' })).toBeInTheDocument();
  });
});
