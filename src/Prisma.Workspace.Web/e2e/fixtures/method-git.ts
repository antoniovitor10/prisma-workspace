import { createHmac, randomUUID } from 'node:crypto';
import { expect, request as playwrightRequest, type APIRequestContext, type APIResponse } from '@playwright/test';

/**
 * Fixtures da integração Git do método (fase 2a): projeto próprio, tokens de método,
 * repositório cadastrado no GitHub falso (e2e/fixtures/github-fake.mjs) e webhook assinado.
 *
 * Só roda com E2E_METHOD_GIT=1 e o ambiente da frente: app com MethodGit__GitHubApiBaseUrl
 * apontando para o falso, segredos de teste MethodGit__Secrets__* e o mesmo segredo de webhook
 * em E2E_METHOD_WEBHOOK_SECRET. Nada aqui imprime token, segredo ou corpo assinado.
 */
export const methodGitEnabled = process.env.E2E_METHOD_GIT === '1';
const apiUrl = (process.env.E2E_API_URL ?? 'http://127.0.0.1:5400').replace(/\/+$/, '');
const fakeUrl = (process.env.E2E_GITHUB_FAKE_URL ?? 'http://metodo-github:3134').replace(/\/+$/, '');
const webhookSecret = () => {
  const s = process.env.E2E_METHOD_WEBHOOK_SECRET;
  if (!s) throw new Error('E2E_METHOD_WEBHOOK_SECRET não definido.');
  return s;
};
export const APP_ID = Number(process.env.E2E_METHOD_APP_ID ?? 424242);
export const INSTALLATION_ID = Number(process.env.E2E_METHOD_INSTALLATION_ID ?? 1);

export const STATUS = ['backlog', 'pronta', 'em-desenvolvimento', 'em-revisao', 'em-teste', 'concluida', 'bloqueada'] as const;
export type Status = typeof STATUS[number];

export interface MethodTask {
  workItemId: string; id: string; title: string; status: Status; hu: string | null;
  filePath: string | null; covers: string[]; risk: string | null; pr: string | null;
  dependsOn: string[]; authorized: boolean; canResume: boolean;
  cycleId?: string | null; blockingEventId?: string | null; blockingPrincipalId?: string | null; blockingOriginStatus?: string | null;
}

/** Cliente HTTP com uma credencial: sessão humana (JWT de login + organização) ou token de método. */
export class Api {
  constructor(readonly ctx: APIRequestContext, private readonly headers: Record<string, string>) {}
  get(path: string) { return this.ctx.get(`${apiUrl}${path}`, { headers: this.headers }); }
  post(path: string, data?: unknown) { return this.ctx.post(`${apiUrl}${path}`, { headers: this.headers, data }); }
  put(path: string, data?: unknown) { return this.ctx.put(`${apiUrl}${path}`, { headers: this.headers, data }); }
  async json<T>(r: Promise<APIResponse> | APIResponse, status?: number): Promise<T> {
    const res = await r;
    if (status !== undefined) expect(res.status(), `${res.url()} → ${await res.text()}`).toBe(status);
    else expect(res.ok(), `${res.url()} → ${res.status()} ${await res.text()}`).toBeTruthy();
    const t = await res.text();
    return (t ? JSON.parse(t) : undefined) as T;
  }
}

export async function humanSession(): Promise<{ api: Api; token: string; organizationId: string }> {
  const ctx = await playwrightRequest.newContext();
  const login = await ctx.post(`${apiUrl}/api/auth/login`, {
    data: { email: process.env.E2E_TEST_USER_EMAIL, password: process.env.E2E_TEST_USER_PASSWORD },
  });
  expect(login.ok(), `login humano ${login.status()}`).toBeTruthy();
  const token: string = (await login.json()).accessToken;
  const orgs = await (await ctx.get(`${apiUrl}/api/organizations`, { headers: { Authorization: `Bearer ${token}` } })).json();
  const organizationId: string = (orgs as { id: string; isActive?: boolean }[]).filter(o => o.isActive !== false)[0].id;
  return { api: new Api(ctx, { Authorization: `Bearer ${token}`, 'X-Organization-Id': organizationId }), token, organizationId };
}

/** Token de método sem cabeçalho de organização: o escopo vem do registro do token no servidor. */
export async function tokenSession(token: string): Promise<Api> {
  return new Api(await playwrightRequest.newContext(), { Authorization: `Bearer ${token}` });
}

export interface MethodProject {
  projectId: string; boardId: string; name: string;
  stages: Record<Status, string>;
  agentToken: string; agentPrincipalId: string; systemPrincipalId: string;
  integrationId: string; repositoryId: number; repositoryFullName: string;
}

const unique = () => `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;

/** Cria projeto próprio, provisiona o método, emite tokens agente/sistema e cadastra o repositório. */
export async function createMethodProject(human: Api, label: string): Promise<MethodProject> {
  const name = `metodo-git-e2e-${label}-${unique()}`;
  const projectId = await human.json<string | { id: string }>(human.post('/api/projects', {
    key: null, name, description: 'Projeto próprio do E2E da integração Git do método.', nature: 1, workType: 1,
  })).then(r => (typeof r === 'string' ? r : r.id));
  const snapshot = await human.json<{ boardId: string; stageMappings: Record<string, string> }>(
    human.post(`/api/projects/${projectId}/method/provision`, { autonomyLevel: 0 }));
  await human.json(human.put(`/api/projects/${projectId}/method/settings`, { enabled: true, autonomyLevel: 0 }));
  for (const s of STATUS) expect(snapshot.stageMappings[s], `coluna mapeada para ${s}`).toBeTruthy();

  const agent = await human.json<{ metadata: { principalId: string }; token: string }>(
    human.post(`/api/projects/${projectId}/method/tokens`, { name: 'agente e2e', role: 'agente', validDays: 1 }));
  const system = await human.json<{ metadata: { principalId: string }; token: string }>(
    human.post(`/api/projects/${projectId}/method/tokens`, { name: 'sistema e2e', role: 'sistema', validDays: 1 }));

  // Cada execução usa um Repository ID novo: Provider/Host/RepositoryId é único entre projetos.
  const repositoryId = 900000 + Math.floor(Math.random() * 90000);
  const repositoryFullName = `qa/metodo-${unique()}`;
  await fake('PUT', '/__fixture/repository', { id: repositoryId, full_name: repositoryFullName, default_branch: 'main', installation_id: INSTALLATION_ID });
  const repo = await human.json<{ id: string }>(human.post(`/api/projects/${projectId}/method/repositories`, {
    host: 'github.com', repositoryId, repositoryFullName, defaultBranch: 'main', appId: APP_ID, installationId: INSTALLATION_ID,
    appPrivateKeySecretRef: 'test-app-key', webhookSecretRef: 'test-webhook-secret',
    systemPrincipalId: system.metadata.principalId, enabled: true,
  }));
  return {
    projectId, boardId: snapshot.boardId, name, stages: snapshot.stageMappings as Record<Status, string>,
    agentToken: agent.token, agentPrincipalId: agent.metadata.principalId, systemPrincipalId: system.metadata.principalId,
    integrationId: repo.id, repositoryId, repositoryFullName,
  };
}

export async function createTask(api: Api, p: MethodProject, id: string, status: Status = 'backlog', extra: Partial<{ title: string; hu: string }> = {}) {
  return api.json<MethodTask>(api.post(`/api/projects/${p.projectId}/method/tasks`, {
    id, title: extra.title ?? `Task ${id} do E2E`, hu: extra.hu ?? 'HU-9001',
    filePath: `docs/trabalho/HU-9001-e2e/tasks/${id}.md`, covers: ['CA-1'], risk: 'normal', status,
  }));
}

export const getTask = (api: Api, p: MethodProject, id: string) =>
  api.json<MethodTask>(api.get(`/api/projects/${p.projectId}/method/tasks/${id}`));

export const moveTask = (api: Api, p: MethodProject, id: string, newStatus: Status, reason = 'E2E') =>
  api.post(`/api/projects/${p.projectId}/method/tasks/${id}/move`, { newStatus, reason });

/** O webhook é processado fora da requisição: espera o status por consulta, sem sleep fixo. */
export async function expectStatus(api: Api, p: MethodProject, id: string, status: Status, timeout = 20_000) {
  await expect.poll(async () => (await getTask(api, p, id)).status, { timeout, message: `${id} deveria chegar em ${status}` }).toBe(status);
}

/** Garante que o status NÃO muda: dá tempo ao worker e confere ao final. */
export async function expectStays(api: Api, p: MethodProject, id: string, status: Status, ms = 4_000) {
  const t0 = Date.now();
  while (Date.now() - t0 < ms) {
    expect((await getTask(api, p, id)).status, `${id} não deveria sair de ${status}`).toBe(status);
    await new Promise(r => setTimeout(r, 500));
  }
}

// ---- GitHub falso ----
export async function fake(method: 'GET' | 'PUT' | 'POST', path: string, data?: unknown) {
  const ctx = await playwrightRequest.newContext();
  const r = await ctx.fetch(`${fakeUrl}${path}`, { method, data });
  expect(r.status(), `fixture ${method} ${path}`).toBeLessThan(300);
  const t = await r.text();
  await ctx.dispose();
  return t ? JSON.parse(t) : undefined;
}

export interface PrSpec {
  number: number; branch: string; title?: string; draft?: boolean; state?: 'open' | 'closed';
  merged?: boolean; base?: string; headSha?: string;
}

/** Atualiza o PR no GitHub falso (a verdade que o provedor consulta) e devolve o payload do webhook. */
export async function setPullRequest(p: MethodProject, pr: PrSpec) {
  const merged = !!pr.merged;
  const now = new Date().toISOString();
  await fake('PUT', `/__fixture/pulls/${pr.number}`, {
    title: pr.title ?? `PR ${pr.number}`, state: pr.state ?? 'open', draft: !!pr.draft, merged,
    merged_at: merged ? now : null, merge_commit_sha: merged ? 'a'.repeat(40) : null, closed_at: pr.state === 'closed' ? now : null,
    head: { ref: pr.branch, sha: pr.headSha ?? 'b'.repeat(40) }, base: { ref: pr.base ?? 'main' },
  });
  const repository = { id: p.repositoryId, full_name: p.repositoryFullName, name: p.repositoryFullName.split('/')[1], default_branch: 'main', html_url: `https://github.com/${p.repositoryFullName}` };
  return {
    number: pr.number, repository, installation: { id: INSTALLATION_ID }, sender: { login: 'dev-e2e', id: 4242, type: 'User' },
    pull_request: {
      id: 7_000_000 + pr.number, number: pr.number, html_url: `https://github.com/${p.repositoryFullName}/pull/${pr.number}`,
      title: pr.title ?? `PR ${pr.number}`, state: pr.state ?? 'open', draft: !!pr.draft, merged,
      merged_at: merged ? now : null, merge_commit_sha: merged ? 'a'.repeat(40) : null, updated_at: now, created_at: now,
      head: { ref: pr.branch, sha: pr.headSha ?? 'b'.repeat(40), repo: repository }, base: { ref: pr.base ?? 'main', sha: 'c'.repeat(40), repo: repository },
    },
  };
}

export interface WebhookOptions { delivery?: string; signature?: 'valid' | 'wrong-secret' | 'none' | 'sha1-only' | 'malformed'; tamper?: boolean; raw?: string }

export async function sendWebhook(p: MethodProject, event: string, payload: unknown, o: WebhookOptions = {}) {
  const raw = o.raw ?? JSON.stringify(payload);
  const delivery = o.delivery ?? randomUUID();
  const headers: Record<string, string> = {
    'Content-Type': 'application/json', 'User-Agent': 'GitHub-Hookshot/e2e',
    'X-GitHub-Event': event, 'X-GitHub-Delivery': delivery, 'X-GitHub-Hook-ID': '1',
  };
  const hmac = (secret: string, alg = 'sha256') => createHmac(alg, secret).update(raw).digest('hex');
  switch (o.signature ?? 'valid') {
    case 'valid': headers['X-Hub-Signature-256'] = `sha256=${hmac(webhookSecret())}`; break;
    case 'wrong-secret': headers['X-Hub-Signature-256'] = `sha256=${hmac(webhookSecret() + 'x')}`; break;
    case 'sha1-only': headers['X-Hub-Signature'] = `sha1=${hmac(webhookSecret(), 'sha1')}`; break;
    case 'malformed': headers['X-Hub-Signature-256'] = `sha256=${'z'.repeat(64)}`; break;
    case 'none': break;
  }
  const body = o.tamper ? raw.replace('"title":"', '"title":"X') : raw;
  const ctx = await playwrightRequest.newContext();
  const t0 = Date.now();
  const res = await ctx.post(`${apiUrl}/api/integrations/git/github/${p.integrationId}/webhook`, { headers, data: Buffer.from(body, 'utf8') });
  const result = { status: res.status(), ms: Date.now() - t0, delivery, text: await res.text() };
  await ctx.dispose();
  return result;
}

export async function pullRequestEvent(p: MethodProject, action: string, pr: PrSpec, o: WebhookOptions = {}) {
  const payload = await setPullRequest(p, pr);
  return sendWebhook(p, 'pull_request', { action, ...payload }, o);
}

export async function reviewEvent(p: MethodProject, pr: PrSpec, state: 'changes_requested' | 'approved' | 'commented', reviewId: number, commitId?: string) {
  const payload = await setPullRequest(p, pr);
  const review = { id: reviewId, state: state.toUpperCase(), commit_id: commitId ?? pr.headSha ?? 'b'.repeat(40), submitted_at: new Date().toISOString(), user: { id: 4343, login: 'revisor' } };
  await fake('PUT', `/__fixture/pulls/${pr.number}/reviews`, [review]);
  return sendWebhook(p, 'pull_request_review', { action: 'submitted', ...payload, review: { ...review, state } });
}
