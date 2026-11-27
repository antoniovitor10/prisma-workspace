import { spawn, type ChildProcess } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { expect, request as playwrightRequest } from '@playwright/test';
import { STATUS, type Api, type Status } from './method-git';

/**
 * Modo consulta do método Git (HU-0002): o GitHub falso (github-fake.mjs) roda dentro do próprio runner do Playwright,
 * e a API alcança o runner pela rede do ambiente (MethodGit__GitHubApiBaseUrl, em scripts/ci-e2e.sh).
 * Só roda com E2E_GITHUB_FAKE_PORT definido pelo ambiente; o token é falso e nunca é impresso.
 */
export const pollFakePort = process.env.E2E_GITHUB_FAKE_PORT ?? '';
export const pollEnabled = /^\d{2,5}$/.test(pollFakePort);
const fakeUrl = `http://127.0.0.1:${pollFakePort}`;
const fakeScript = path.join(path.dirname(fileURLToPath(import.meta.url)), 'github-fake.mjs');

export async function startGitHubFake(): Promise<ChildProcess> {
  const child = spawn(process.execPath, [fakeScript], { env: { ...process.env, GITHUB_FAKE_PORT: pollFakePort }, stdio: 'ignore' });
  await expect.poll(async () => {
    try {
      const ctx = await playwrightRequest.newContext();
      const response = await ctx.get(`${fakeUrl}/__fixture/calls`);
      await ctx.dispose();
      return response.status();
    } catch { return 0; }
  }, { timeout: 15_000, message: 'GitHub falso do modo consulta não subiu' }).toBe(200);
  return child;
}

export async function fakePoll(method: 'PUT' | 'POST' | 'GET', fixturePath: string, data?: unknown) {
  const ctx = await playwrightRequest.newContext();
  const response = await ctx.fetch(`${fakeUrl}${fixturePath}`, { method, data });
  expect(response.status(), `fixture ${method} ${fixturePath}`).toBeLessThan(300);
  await ctx.dispose();
}

export interface PollProject {
  projectId: string; boardId: string; name: string; stages: Record<Status, string>;
  owner: string; repositoryFullName: string; repositoryId: number; token: string;
  systemPrincipalId: string; systemName: string; integrationId: string | null;
}

const unique = () => `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;

/** Projeto próprio com o método, token de sistema e um repositório no GitHub falso; opcionalmente já conectado no modo consulta. */
export async function createPollProject(human: Api, label: string, register: boolean): Promise<PollProject> {
  const name = `metodo-consulta-e2e-${label}-${unique()}`;
  const projectId = await human.json<string | { id: string }>(human.post('/api/projects', {
    key: null, name, description: 'Projeto próprio do E2E do modo consulta.', nature: 1, workType: 1,
  })).then(r => (typeof r === 'string' ? r : r.id));
  const snapshot = await human.json<{ boardId: string; stageMappings: Record<string, string> }>(
    human.post(`/api/projects/${projectId}/method/provision`, { autonomyLevel: 0 }));
  await human.json(human.put(`/api/projects/${projectId}/method/settings`, { enabled: true, autonomyLevel: 0 }));
  for (const s of STATUS) expect(snapshot.stageMappings[s], `coluna mapeada para ${s}`).toBeTruthy();
  const systemName = `sistema consulta ${unique()}`;
  const system = await human.json<{ metadata: { principalId: string } }>(
    human.post(`/api/projects/${projectId}/method/tokens`, { name: systemName, role: 'sistema', validDays: 1 }));
  const owner = `qapoll${unique()}`.slice(0, 30);
  const repositoryFullName = `${owner}/canais-mobile`;
  const repositoryId = 700000 + Math.floor(Math.random() * 99000);
  const token = `github_pat_E2E${randomBytes(18).toString('hex')}`;
  await fakePoll('PUT', '/__fixture/repository', { id: repositoryId, full_name: repositoryFullName, default_branch: 'main', installation_id: 77 });
  await fakePoll('PUT', '/__fixture/read-token', { token, repos: [repositoryFullName] });
  let integrationId: string | null = null;
  if (register) {
    await human.json(human.post(`/api/projects/${projectId}/method/git-credentials`, { gitHubOwner: owner, token }));
    integrationId = (await human.json<{ id: string }>(human.post(`/api/projects/${projectId}/method/repositories/poll`, {
      repositoryFullName, defaultBranch: 'main', systemPrincipalId: system.metadata.principalId, enabled: true,
    }))).id;
  }
  return { projectId, boardId: snapshot.boardId, name, stages: snapshot.stageMappings as Record<Status, string>, owner, repositoryFullName,
    repositoryId, token, systemPrincipalId: system.metadata.principalId, systemName, integrationId };
}

/** PR no GitHub falso, no repositório do projeto. */
export async function setPollPullRequest(p: PollProject, pr: { number: number; branch: string; title?: string; state?: 'open' | 'closed'; merged?: boolean }) {
  const now = new Date().toISOString();
  const merged = !!pr.merged;
  await fakePoll('PUT', `/__fixture/pulls/${pr.number}?repo=${encodeURIComponent(p.repositoryFullName)}`, {
    title: pr.title ?? `PR ${pr.number}`, state: pr.state ?? 'open', draft: false, merged,
    merged_at: merged ? now : null, merge_commit_sha: merged ? 'a'.repeat(40) : null, closed_at: pr.state === 'closed' ? now : null,
    head: { ref: pr.branch, sha: 'b'.repeat(40) }, base: { ref: 'main' }, user: { id: 4242, login: 'dev-e2e' },
  });
}
