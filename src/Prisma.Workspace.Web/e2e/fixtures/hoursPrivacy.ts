import type { APIRequestContext, APIResponse, Page } from '@playwright/test';
import { cleanupFor, expect } from './test';
import { withBoardFactory, type BoardFactory } from './boards';
import { test as base } from './functional';
export { expect } from './functional';

export const privacyRoles = [{ name: 'Membro', role: 7 }, { name: 'Visualizador', role: 10 },
  { name: 'Gestor', role: 2 }, { name: 'Administrador', role: 1 }] as const;
export async function withHoursPrivacy(page: Page, request: APIRequestContext, role: number,
  run: (f: HoursPrivacy) => Promise<void>) {
  await page.goto('/projects', { waitUntil: 'domcontentloaded' });
  await withBoardFactory(page, request, async boards => {
    const teams: string[] = [];
    cleanupFor(request)(async () => {
      for (const id of teams.reverse()) expect((await boards.api(`/api/teams/${id}/active`, 'PUT', { isActive: false })).status()).toBe(204);
    });
    const actor = await boards.member(role === 10 ? 7 : role);
    const colleague = await boards.member(); const outsider = await boards.member();
    async function team(people: string[], leaderId: string) {
      const name = boards.name('equipe');
      const response = await boards.api('/api/teams', 'POST', { name, defaultWeeklyCapacityHours: 40 });
      expect(response.status()).toBe(200); const id: string = (await response.json()).id; teams.push(id);
      for (const userId of people) expect((await boards.api(`/api/teams/${id}/members`, 'POST', { userId })).status()).toBe(204);
      expect((await boards.api(`/api/teams/${id}`, 'PUT', { name, leaderId, defaultWeeklyCapacityHours: 40 })).status()).toBe(204);
      expect((await boards.api(`/api/projects/${boards.projectId}/teams/${id}`, 'POST')).status()).toBe(204);
      return id;
    }
    const managed = await team([actor.id, colleague.id], actor.id);
    const otherTeam = await team([outsider.id], outsider.id);
    const board = await boards.board(); const stage = await boards.stage(board.id);
    const parent = await boards.item(board.id, stage.id, { teamId: managed });
    const task = await boards.item(board.id, stage.id, { teamId: managed, responsibleId: actor.id });
    const child = await boards.item(board.id, stage.id, { parentId: parent.id, teamId: managed, responsibleId: actor.id });
    const other = await boards.item(board.id, stage.id, { teamId: otherTeam, responsibleId: outsider.id });
    const personal = await boards.item(board.id, stage.id, { responsibleId: actor.id });
    const today = new Date().toLocaleDateString('en-CA', { timeZone: 'America/Sao_Paulo' });
    const yesterday = new Date(Date.now() - 86_400_000).toLocaleDateString('en-CA', { timeZone: 'America/Sao_Paulo' });
    const monday = new Date(`${today}T12:00:00Z`).getUTCDay() === 1;
    // As seis entradas precisam terminar no passado, inclusive na madrugada de segunda.
    const elapsed = Date.now() - new Date(`${today}T00:00:00-03:00`).getTime();
    const date = monday && elapsed >= 3 * 3_600_000 + 60_000 ? today : yesterday;
    const inCurrentWeek = !monday || date === today;
    const ownNote = boards.name('nota própria'); const otherNote = boards.name('nota de colega');
    const time = (hour: number) => `${date}T${String(Math.floor(hour)).padStart(2, '0')}:${hour % 1 ? '30' : '00'}:00-03:00`;
    for (const [item, person, start, end, note] of [
      [task, actor, 0, 0.5, ownNote], [child, actor, 0.5, 1, ownNote], [personal, actor, 1, 1.5, ownNote],
      [task, colleague, 0, 1.5, otherNote], [child, colleague, 1.5, 2, otherNote], [other, outsider, 0, 3, otherNote],
    ] as const) expect((await boards.api('/api/TimeEntries/manual', 'POST', { workItemId: item.id,
      startedAt: time(start), endedAt: time(end), note }, person)).status()).toBe(201);
    if (role === 10) expect((await boards.api(`/api/organizations/current/members/${actor.id}`, 'PUT',
      { role, isActive: true })).status()).toBe(204);
    const fixture = { boards, actor, colleague, outsider, managed, otherTeam, board, stage, parent, task, child,
      other, personal, date, inCurrentWeek, role, ownNote, otherNote,
      api: (path: string, method = 'GET', data?: unknown) => boards.api(path, method, data, actor),
      async get<T>(path: string): Promise<T> {
        const response = await this.api(path); expect(response.status(), `GET ${path}`).toBe(200); return response.json();
      },
    };
    await run(fixture);
  }, 'H03');
}
type Person = Awaited<ReturnType<BoardFactory['member']>>;
type Item = Awaited<ReturnType<BoardFactory['item']>>;
export type HoursPrivacy = { boards: BoardFactory; actor: Person; colleague: Person; outsider: Person;
  managed: string; otherTeam: string; board: Awaited<ReturnType<BoardFactory['board']>>;
  stage: Awaited<ReturnType<BoardFactory['stage']>>; parent: Item; task: Item; child: Item; other: Item; personal: Item;
  date: string; inCurrentWeek: boolean; role: number; ownNote: string; otherNote: string;
  api(path: string, method?: string, data?: unknown): Promise<APIResponse>; get<T>(path: string): Promise<T> };

// Três pessoas, duas equipes e seis intervalos pertencem ao arranjo, não ao
// prazo das asserções. O corpo mantém os 30s padrão; a limpeza tem seus 20s
// na fixture request. Medir o arranjo permite identificar consultas frias.
export const test = base.extend<{ hours: HoursPrivacy; hoursRole: number }>({
  hoursRole: [7, { option: true }],
  hours: [async ({ page, request, hoursRole }, use, info) => {
    const started = Date.now();
    await withHoursPrivacy(page, request, hoursRole, async fixture => {
      info.annotations.push({ type: 'hours-setup-ms', description: String(Date.now() - started) });
      const assertionsStarted = Date.now();
      try { await use(fixture); } finally {
        info.annotations.push({ type: 'hours-body-ms', description: String(Date.now() - assertionsStarted) });
      }
    });
  }, { timeout: 30_000 }],
});

