import { api } from '../../services/api';
import type {
  GitRepository, IssuedToken, IssueTokenInput, MethodIdentity, MethodProjectSettings,
  MethodTask, MethodToken, PullRequestPage, RegisterRepositoryInput,
} from './types';

const base = (projectId: string) => `/api/projects/${encodeURIComponent(projectId)}/method`;

function safeMessage(error: unknown): string {
  const message = error instanceof Error ? error.message : 'Não foi possível concluir a operação. Tente novamente.';
  return message.replace(/pmt_[A-Za-z0-9_-]{43}/g, '[removido]')
    .replace(/Bearer\s+\S+/gi, 'Bearer [removido]')
    .replace(/eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+/g, '[removido]');
}

async function request<T>(path: string, method = 'GET', data?: unknown): Promise<T> {
  try {
    if (!api.getToken()) throw new Error('Sua sessão terminou. Entre novamente para continuar.');
    // Usa somente a sessão humana da SPA e sua renovação normal. Não guarda credenciais do método.
    return await api.request(path, { method, ...(data === undefined ? {} : { body: JSON.stringify(data) }) }) as T;
  } catch (error) { throw new Error(safeMessage(error)); }
}

export const methodApi = {
  identity: (projectId: string) => request<MethodIdentity>(`${base(projectId)}/me`),
  settings: (projectId: string) => request<MethodProjectSettings | null>(`${base(projectId)}/settings`),
  provision: (projectId: string, boardId?: string) => request<MethodProjectSettings>(`${base(projectId)}/provision`, 'POST',
    { ...(boardId ? { boardId } : {}), autonomyLevel: 0 }),
  updateSettings: (projectId: string, enabled: boolean, autonomyLevel: number) => request<MethodProjectSettings>(
    `${base(projectId)}/settings`, 'PUT', { enabled, autonomyLevel }),
  tokens: (projectId: string) => request<MethodToken[]>(`${base(projectId)}/tokens`),
  issueToken: (projectId: string, data: IssueTokenInput) => request<IssuedToken>(`${base(projectId)}/tokens`, 'POST', data),
  revokeToken: (projectId: string, tokenId: string) => request<void>(`${base(projectId)}/tokens/${encodeURIComponent(tokenId)}/revoke`, 'POST'),
  repositories: (projectId: string) => request<GitRepository[]>(`${base(projectId)}/repositories`),
  registerRepository: (projectId: string, data: RegisterRepositoryInput) => request<GitRepository>(`${base(projectId)}/repositories`, 'POST', data),
  updateRepository: (projectId: string, repositoryId: string, enabled: boolean) => request<GitRepository>(
    `${base(projectId)}/repositories/${encodeURIComponent(repositoryId)}`, 'PUT', { enabled }),
  tasks: (projectId: string) => request<MethodTask[]>(`${base(projectId)}/tasks`),
  unlinked: (projectId: string, page = 1) => request<PullRequestPage>(`${base(projectId)}/pull-requests?linked=false&page=${page}&pageSize=25`),
  link: (projectId: string, observationId: string, workItemId: string, reason: string) => request<unknown>(
    `${base(projectId)}/pull-requests/${encodeURIComponent(observationId)}/link`, 'POST', { workItemId, reason }),
  approveResume: (projectId: string, task: MethodTask, reason: string) => request<unknown>(
    `${base(projectId)}/tasks/${encodeURIComponent(task.workItemId)}/resume-approval`, 'POST',
    { blockingEventId: task.blockingEventId, cycleId: task.cycleId, reason }),
  webhookUrl: (integrationId: string) => `${api.baseUrl || window.location.origin}/api/integrations/git/github/${encodeURIComponent(integrationId)}/webhook`,
};
