import { randomUUID } from 'node:crypto';
import type { APIRequestContext, APIResponse, Page } from '@playwright/test';
import { cleanupFor, expect } from './test';

export type BoardStage = { id: string; name: string; category: number };
export type BoardItem = { id: string; boardId: string; stageId: string; completedAt: string | null; version: string };
export type BoardSession = { accessToken: string; organizationId: string };
export type ItemOptions = { parentId?: string; kind?: number; description?: string; priority?: number;
  startDate?: string; dueDate?: string; estimatedHours?: number; responsibleId?: string };
const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5400';
const userId = (token: string): string => {
  const claims = JSON.parse(Buffer.from(token.split('.')[1], 'base64url').toString());
  return claims.sub ?? claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'];
};

/** Dados exclusivos de uma execução; nunca consulta projeto/tarefa do seed. */
export class BoardFactory {
  private members: Array<{ id: string; role: number }> = [];

  constructor(readonly request: APIRequestContext, readonly admin: BoardSession, readonly projectId: string, readonly batch = 'L06') {}

  name(label: string) { return `${this.batch} ${label} ${randomUUID()}`; }
  get actorId() { return userId(this.admin.accessToken); }

  api(path: string, method = 'GET', data?: unknown, session = this.admin): Promise<APIResponse> {
    return this.request.fetch(`${apiUrl}${path}`, { method, data, headers: {
      Authorization: `Bearer ${session.accessToken}`, 'X-Organization-Id': session.organizationId,
    } });
  }

  async get<T>(path: string): Promise<T> {
    const response = await this.api(path);
    expect(response.status(), `GET ${path}`).toBe(200);
    return response.json();
  }

  private async create(path: string, data: unknown): Promise<string> {
    const response = await this.api(path, 'POST', data);
    expect(response.status(), `POST ${path}`).toBe(201);
    const id = await response.json();
    expect(typeof id).toBe('string');
    return id;
  }

  async board() {
    const name = this.name('quadro');
    const id = await this.create('/api/Boards', { name, projectId: this.projectId });
    return { id, name };
  }

  async stage(boardId: string, category = 3): Promise<BoardStage> {
    const name = this.name('coluna');
    const id = await this.create(`/api/Stages/board/${boardId}`, {
      projectId: this.projectId, name, category, position: 1000,
    });
    return { id, name, category };
  }

  async item(boardId: string, stageId: string, options: ItemOptions = {}) {
    const title = this.name('tarefa');
    const id = await this.create('/api/WorkItems', {
      boardId, stageId, title, responsibleId: userId(this.admin.accessToken), priority: 1, position: 100,
      ...options,
    });
    return { id, title };
  }

  async customField(type = 1) {
    const name = this.name('campo');
    const id = await this.create(`/api/projects/${this.projectId}/custom-fields`, {
      name, type, isRequired: false, optionsJson: null, position: 100,
    });
    return { id, name };
  }

  async viewer(): Promise<BoardSession> { return this.member(10, 1); }

  async member(role = 7, projectRole = 2) {
    const email = `${this.batch.toLowerCase()}-${randomUUID()}@example.test`;
    const name = this.name('pessoa');
    const invitation = await this.api('/api/organizations/current/invitations', 'POST', { email, role, expiresInDays: 1 });
    expect(invitation.status()).toBe(200);
    const password = process.env.E2E_TEST_USER_PASSWORD;
    if (!password) throw new Error('Senha local de teste não configurada.');
    const completed = await this.request.post(`${apiUrl}/api/auth/invitation/complete`, { data: {
      token: (await invitation.json()).token, password, confirmPassword: password,
      createAccount: true, fullName: name,
    } });
    expect(completed.status()).toBe(200);
    const session: BoardSession = await completed.json();
    const id = userId(session.accessToken);
    this.members.push({ id, role });
    expect((await this.api(`/api/projects/${this.projectId}/members/${id}`, 'PUT', { role: projectRole })).status()).toBe(204);
    return { ...session, id, name };
  }

  async cleanup() {
    try {
      for (const { id, role } of this.members) {
        expect((await this.api(`/api/projects/${this.projectId}/members/${id}`, 'DELETE')).status()).toBe(204);
        expect((await this.api(`/api/organizations/current/members/${id}`, 'PUT', { role, isActive: false })).status()).toBe(204);
      }
    } finally {
      expect((await this.api(`/api/projects/${this.projectId}/archive`, 'POST')).status()).toBe(204);
      const projects = await this.get<Array<{ id: string }>>('/api/projects');
      expect(projects.some(project => project.id === this.projectId)).toBe(false);
    }
  }
}

export async function withBoardFactory(page: Page, request: APIRequestContext, run: (factory: BoardFactory) => Promise<void>, batch = 'L06') {
  const cleanup = cleanupFor(request);
  const admin = await page.evaluate(() => ({
    accessToken: localStorage.getItem('prisma_workspace_token')!,
    organizationId: localStorage.getItem('prisma_workspace_organization')!,
  }));
  if (!admin.accessToken || !admin.organizationId) throw new Error('Sessão administrativa E2E ausente.');
  const response = await request.post(`${apiUrl}/api/projects`, { headers: {
    Authorization: `Bearer ${admin.accessToken}`, 'X-Organization-Id': admin.organizationId,
  }, data: { name: `${batch} projeto ${randomUUID()}`, description: 'Fixture exclusiva E2E', nature: 1, workType: 1 } });
  expect(response.status()).toBe(201);
  const fixture = new BoardFactory(request, admin, await response.json(), batch);
  cleanup(() => fixture.cleanup());
  await run(fixture);
}
