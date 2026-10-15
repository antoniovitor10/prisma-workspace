import type { APIRequestContext, Page } from '@playwright/test';
import { expect } from './test';
import { BoardFactory, withBoardFactory, type BoardSession } from './boards';

export type TimeEntry = { id: string; workItemId: string; userId: string; startedAt: string;
  endedAt: string | null; durationSeconds: number | null; note: string | null };
export type TimeTotal = { totalSeconds: number; totalHours: number };
export type DayTime = { workItemId: string; title: string; seconds: number };
export type WeeklyTime = { weekStart: string; days: Array<{ date: string; totalSeconds: number }> };
export type Justification = { id: string; reason: string; hours: number };

/** Cada cenário tem seu próprio usuário: nenhum timer ou total é herdado do seed. */
export class TimeFactory {
  private readonly days = new Set<string>();
  private readonly tasks = new Set<string>();
  constructor(readonly boards: BoardFactory,
    readonly person: BoardSession & { id: string; name: string },
    readonly board: { id: string; name: string }, readonly stage: { id: string; name: string }) {}
  api(path: string, method = 'GET', data?: unknown) { return this.boards.api(path, method, data, this.person); }
  async get<T>(path: string): Promise<T> {
    const response = await this.api(path); expect(response.status(), `GET ${path}`).toBe(200); return response.json();
  }
  async task(category?: number) {
    const column = category === undefined ? this.stage : await this.boards.stage(this.board.id, category);
    const task = await this.boards.item(this.board.id, column.id, { responsibleId: this.person.id });
    this.tasks.add(task.id); return task;
  }
  async start(workItemId: string) {
    const response = await this.api('/api/TimeEntries/start', 'POST', { workItemId });
    expect(response.status()).toBe(200); return response.json() as Promise<TimeEntry>;
  }
  async manual(workItemId: string, startedAt: string, endedAt: string, note = this.boards.name('intervalo')) {
    const response = await this.api('/api/TimeEntries/manual', 'POST', { workItemId, startedAt, endedAt, note });
    expect(response.status()).toBe(201); return response.json() as Promise<TimeEntry>;
  }
  trackDay(date: string) { this.days.add(date); }
  async cleanup() {
    const running = await this.api('/api/TimeEntries/running');
    if (running.status() === 200) {
      const current: TimeEntry = await running.json();
      expect(this.tasks.has(current.workItemId), 'Só encerra cronômetro de tarefa criada pela fixture.').toBe(true);
      expect((await this.api('/api/TimeEntries/stop', 'POST', { workItemId: current.workItemId })).status()).toBe(200);
    } else expect(running.status()).toBe(204);
    for (const day of this.days) {
      const justifications = await this.get<Justification[]>(`/api/me/time/justifications?date=${day}`);
      for (const value of justifications)
        expect((await this.api(`/api/me/time/justifications/${value.id}`, 'DELETE')).status()).toBe(204);
    }
  }
}

export async function withTimeFactory(page: Page, request: APIRequestContext, run: (f: TimeFactory) => Promise<void>) {
  await page.goto('/projects', { waitUntil: 'domcontentloaded' });
  await withBoardFactory(page, request, async boards => {
    const person = await boards.member(); const board = await boards.board(); const stage = await boards.stage(board.id);
    const fixture = new TimeFactory(boards, person, board, stage);
    let runFailure: { error: unknown } | undefined;
    let cleanupFailure: { error: unknown } | undefined;
    try {
      expect((await fixture.api('/api/TimeEntries/running')).status()).toBe(204);
      await page.evaluate(session => {
        localStorage.setItem('prisma_workspace_token', session.accessToken);
        localStorage.setItem('prisma_workspace_organization', session.organizationId);
      }, person);
      await run(fixture);
    } catch (error) { runFailure = { error }; } finally {
      try { await fixture.cleanup(); } catch (error) { cleanupFailure = { error }; }
    }
    if (runFailure && cleanupFailure)
      throw new AggregateError([runFailure.error, cleanupFailure.error], 'Falha no cenário de horas e na limpeza.');
    if (runFailure) throw runFailure.error;
    if (cleanupFailure) throw cleanupFailure.error;
  }, 'T7');
}

