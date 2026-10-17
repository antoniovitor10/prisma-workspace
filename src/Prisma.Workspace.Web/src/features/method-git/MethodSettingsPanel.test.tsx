import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { ThemeProvider } from 'styled-components';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { theme } from '../../styles/theme';
import { methodApi } from './client';
import { MethodSettingsPanel } from './MethodSettingsPanel';
import type { MethodIdentity, MethodProjectSettings, MethodTask, MethodToken, ObservedPullRequest } from './types';

vi.mock('./client', () => ({ methodApi: {
  identity: vi.fn(), settings: vi.fn(), provision: vi.fn(), updateSettings: vi.fn(), tokens: vi.fn(), issueToken: vi.fn(),
  revokeToken: vi.fn(), repositories: vi.fn(), registerRepository: vi.fn(), updateRepository: vi.fn(),
  tasks: vi.fn(), unlinked: vi.fn(), link: vi.fn(), approveResume: vi.fn(), webhookUrl: vi.fn(),
} }));

const settings: MethodProjectSettings = {
  organizationId: 'org-a', projectId: 'project-a', projectName: 'Método', boardId: 'board-a', boardName: 'Método',
  methodIdFieldId: 'field-id', huFieldId: 'field-hu', prFieldId: 'field-pr', enabled: true, gitPhase: 1,
  autonomyLevel: 0, policyVersion: 1, fields: {}, stageMappings: { backlog: '1', pronta: '2', 'em-desenvolvimento': '3',
    'em-revisao': '4', 'em-teste': '5', concluida: '6', bloqueada: '7' },
};
const identity: MethodIdentity = {
  authenticationKind: 'uiSession', role: 'humano', principalId: null, userId: 'user-a', displayName: 'Pessoa',
  organizationId: 'org-a', projectId: 'project-a', projectName: 'Método', boardId: 'board-a', boardName: 'Método',
  tokenId: null, expiresAtUtc: null, capabilities: ['View', 'Create', 'Edit', 'ChangeStatus', 'ManagePermissions'],
};
const token: MethodToken = { id: 'token-a', principalId: 'principal-a', name: 'Executor', principalDisplayName: 'Executor',
  role: 'agente', tokenPrefix: 'prefixo', issuedAtUtc: '2026-10-16T07:00:00Z', expiresAtUtc: '2099-10-16T07:00:00Z',
  revokedAtUtc: null, lastUsedAtUtc: null, permissions: [1, 2, 3, 6, 7] };
const task: MethodTask = { workItemId: 'work-item-a', id: 'T-0001', title: 'Consultar dados', status: 'bloqueada',
  hu: 'HU-0001', filePath: 'tasks/T-0001.md', covers: ['CA-1'], risk: 'normal', pr: null, dependsOn: [],
  authorized: true, canResume: false, cycleId: 'cycle-a', blockingEventId: 'block-a',
  blockingPrincipalId: 'principal-a', blockingOriginStatus: 'em-desenvolvimento' };
const observed: ObservedPullRequest = { id: 'observation-a', integrationId: 'repository-a', projectId: 'project-a',
  pullRequestNumber: 42, url: 'https://github.com/qa/metodo/pull/42', title: 'Correção com título livre',
  headBranch: 'feature/consulta', baseBranch: 'main', state: 'open', isDraft: false, workItemId: null,
  methodTaskId: null, unlinkedReason: 'Nenhuma task identificada.', updatedAtUtc: '2026-10-16T07:00:00Z' };

function show(projectId = 'project-a') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  const wrap = (id: string) => <ThemeProvider theme={theme}><QueryClientProvider client={client}>
    <MethodSettingsPanel projectId={id} boards={[{ id: 'existing-board', name: 'Quadro existente' }]} />
  </QueryClientProvider></ThemeProvider>;
  const view = render(wrap(projectId));
  return { client, ...view, project: (id: string) => view.rerender(wrap(id)) };
}

beforeEach(() => {
  vi.clearAllMocks(); localStorage.clear();
  vi.mocked(methodApi.identity).mockImplementation(async projectId => ({ ...identity, projectId }));
  vi.mocked(methodApi.settings).mockImplementation(async projectId => ({ ...settings, projectId }));
  vi.mocked(methodApi.tokens).mockResolvedValue([]);
  vi.mocked(methodApi.tasks).mockResolvedValue([]);
  vi.mocked(methodApi.repositories).mockResolvedValue([]);
  vi.mocked(methodApi.unlinked).mockResolvedValue({ items: [], page: 1, pageSize: 25, total: 0 });
  vi.mocked(methodApi.revokeToken).mockResolvedValue(undefined);
  vi.mocked(methodApi.approveResume).mockResolvedValue({ eventId: 'approval-a' });
});
afterEach(() => { cleanup(); vi.restoreAllMocks(); });

describe('Configuração do método', () => {
  it('apenas lê ao abrir e só provisiona após ação explícita do administrador', async () => {
    let configured = false;
    vi.mocked(methodApi.settings).mockImplementation(async () => configured ? settings : null);
    vi.mocked(methodApi.provision).mockImplementation(async () => { configured = true; return settings; });
    show();
    await screen.findByRole('heading', { name: 'Método ainda não configurado' });
    expect(methodApi.provision).not.toHaveBeenCalled();
    fireEvent.change(screen.getByLabelText('Quadro do método'), { target: { value: 'existing-board' } });
    fireEvent.click(screen.getByRole('button', { name: 'Configurar método neste projeto' }));
    await waitFor(() => expect(methodApi.provision).toHaveBeenCalledWith('project-a', 'existing-board'));
    await screen.findByRole('heading', { name: 'Tokens do projeto' });
  });

  it('membro sem ManagePermissions não consulta tokens nem mostra emissão', async () => {
    vi.mocked(methodApi.identity).mockResolvedValue({ ...identity, capabilities: ['View', 'Edit', 'ChangeStatus'] });
    show();
    await screen.findByRole('heading', { name: 'PRs não vinculados' });
    expect(screen.queryByRole('button', { name: 'Emitir token' })).not.toBeInTheDocument();
    expect(methodApi.tokens).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Retomadas' })).toBeVisible();
  });

  it('papel humano com credencial do método não ganha ações de sessão UI', async () => {
    vi.mocked(methodApi.identity).mockResolvedValue({ ...identity, authenticationKind: 'methodToken', role: 'humano' });
    show();
    await screen.findByRole('heading', { name: 'PRs não vinculados' });
    expect(screen.queryByRole('button', { name: 'Tokens' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Retomadas' })).not.toBeInTheDocument();
  });

  it('exibe segredo uma vez, fora do cache de consulta, e o descarta ao mudar de projeto', async () => {
    const secret = `pmt_${'a'.repeat(43)}`;
    vi.mocked(methodApi.issueToken).mockResolvedValue({ metadata: token, token: secret });
    const view = show();
    await screen.findByRole('button', { name: 'Emitir token' });
    fireEvent.change(screen.getByLabelText('Nome do token'), { target: { value: 'Executor' } });
    fireEvent.click(screen.getByRole('button', { name: 'Emitir token' }));
    expect(await screen.findByLabelText('Token emitido')).toHaveValue(secret);
    expect(methodApi.issueToken).toHaveBeenCalledWith('project-a', { name: 'Executor', role: 'agente', validDays: 30 });
    expect(JSON.stringify(localStorage)).not.toContain(secret);
    expect(JSON.stringify(view.client.getQueryCache().getAll().map(query => query.state.data))).not.toContain(secret);
    view.project('project-b');
    await screen.findByRole('button', { name: 'Emitir token' });
    expect(screen.queryByLabelText('Token emitido')).not.toBeInTheDocument();
  });

  it('revogação pede confirmação e reflete os metadados atualizados', async () => {
    let revoked = false;
    vi.mocked(methodApi.tokens).mockImplementation(async () => [{ ...token, revokedAtUtc: revoked ? '2026-10-16T08:00:00Z' : null }]);
    vi.mocked(methodApi.revokeToken).mockImplementation(async () => { revoked = true; });
    show();
    fireEvent.click(await screen.findByRole('button', { name: 'Revogar Executor' }));
    expect(methodApi.revokeToken).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Confirmar revogação' }));
    await waitFor(() => expect(methodApi.revokeToken).toHaveBeenCalledWith('project-a', 'token-a'));
    await screen.findByText('Token revogado.');
    expect(screen.queryByRole('button', { name: 'Revogar Executor' })).not.toBeInTheDocument();
  });

  it('emite token humano pessoal sem escolher outra pessoa nem um principal existente', async () => {
    vi.mocked(methodApi.issueToken).mockResolvedValue({ metadata: { ...token, role: 'humano' }, token: `pmt_${'h'.repeat(43)}` });
    show();
    await screen.findByRole('button', { name: 'Emitir token' });
    fireEvent.change(screen.getByLabelText('Nome do token'), { target: { value: 'Meu acesso' } });
    fireEvent.change(screen.getByLabelText('Papel'), { target: { value: 'humano' } });
    fireEvent.click(screen.getByRole('button', { name: 'Emitir token' }));
    await screen.findByLabelText('Token emitido');
    expect(methodApi.issueToken).toHaveBeenCalledWith('project-a', { name: 'Meu acesso', role: 'humano', validDays: 30 });
  });

  it('conecta o repositório com branch explícita e referências de segredos do servidor', async () => {
    vi.mocked(methodApi.tokens).mockResolvedValue([{ ...token, role: 'sistema', principalDisplayName: 'GitHub' }]);
    vi.mocked(methodApi.registerRepository).mockResolvedValue({ id: 'repository-a', projectId: 'project-a',
      repositoryFullName: 'qa/metodo', repositoryId: 900001, defaultBranch: 'trunk', systemPrincipalId: 'principal-a',
      appId: 424242, installationId: 1, host: 'github.com', enabled: true, operationalError: null });
    show();
    fireEvent.click(await screen.findByRole('button', { name: 'Repositórios' }));
    await screen.findByRole('option', { name: 'GitHub' });
    for (const [label, value] of [
      ['Repositório', 'qa/metodo'], ['ID do repositório', '900001'], ['Branch principal', 'trunk'],
      ['Sistema de integração', 'principal-a'], ['GitHub App ID', '424242'], ['Installation ID', '1'],
      ['Referência da chave privada', 'test-app-key'], ['Referência do segredo do webhook', 'test-webhook-secret'],
    ]) fireEvent.change(screen.getByLabelText(label, { exact: label !== 'Referência da chave privada' }), { target: { value } });
    fireEvent.click(screen.getByRole('button', { name: 'Conectar repositório' }));
    await waitFor(() => expect(methodApi.registerRepository).toHaveBeenCalledWith('project-a', {
      repositoryFullName: 'qa/metodo', repositoryId: 900001, defaultBranch: 'trunk', systemPrincipalId: 'principal-a',
      appId: 424242, installationId: 1, host: 'github.com', appPrivateKeySecretRef: 'test-app-key',
      webhookSecretRef: 'test-webhook-secret', enabled: true,
    }));
    await screen.findByText('Repositório conectado. Configure o webhook no GitHub com a URL abaixo.');
  });

  it('vincula PR de título livre usando o GUID da task selecionada no projeto', async () => {
    vi.mocked(methodApi.unlinked).mockResolvedValue({ items: [observed], page: 1, pageSize: 25, total: 1 });
    vi.mocked(methodApi.tasks).mockResolvedValue([task]);
    vi.mocked(methodApi.link).mockResolvedValue({ id: 'link-a' });
    show();
    fireEvent.click(await screen.findByRole('button', { name: 'PRs não vinculados' }));
    await screen.findByRole('link', { name: '#42 · Correção com título livre' });
    fireEvent.change(screen.getByLabelText('Task do PR #42'), { target: { value: task.workItemId } });
    fireEvent.change(screen.getByLabelText('Motivo do vínculo do PR #42'), { target: { value: 'Conferido com a task.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Vincular PR #42' }));
    await waitFor(() => expect(methodApi.link).toHaveBeenCalledWith('project-a', observed.id, task.workItemId, 'Conferido com a task.'));
  });

  it('liberação usa o bloqueio vigente e mantém a ação separada de mover o card', async () => {
    vi.mocked(methodApi.tasks).mockResolvedValue([task]);
    show();
    fireEvent.click(await screen.findByRole('button', { name: 'Retomadas' }));
    await screen.findByLabelText('Motivo da liberação de T-0001');
    fireEvent.change(screen.getByLabelText('Motivo da liberação de T-0001'), { target: { value: 'Impedimento resolvido.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Liberar retomada de T-0001' }));
    await waitFor(() => expect(methodApi.approveResume).toHaveBeenCalledWith('project-a', task, 'Impedimento resolvido.'));
    await screen.findByText('Retomada liberada para este bloqueio.');
  });

  it('mostra erro de leitura com uma ação para tentar novamente', async () => {
    vi.mocked(methodApi.settings).mockRejectedValue(new Error('Serviço indisponível.'));
    show();
    await screen.findByRole('alert');
    expect(screen.getByText('Serviço indisponível.')).toBeVisible();
    const before = vi.mocked(methodApi.settings).mock.calls.length;
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }));
    await waitFor(() => expect(methodApi.settings).toHaveBeenCalledTimes(before + 1));
  });
});
