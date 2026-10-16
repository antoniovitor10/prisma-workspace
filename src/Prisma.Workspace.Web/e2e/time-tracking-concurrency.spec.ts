import type { Page } from '@playwright/test';
import { test, expect, navigateWithApi } from './fixtures/functional';
import { withTimeFactory, type TimeFactory, type TimeEntry } from './fixtures/time';

test.use({ timezoneId: 'America/Sao_Paulo' });

const startPath = '**/api/TimeEntries/start';
const responseTo = (page: Page, method: string, path: string) => page.waitForResponse(response =>
  response.request().method() === method && new URL(response.url()).pathname.toLowerCase() === path.toLowerCase());
const today = () => new Date().toLocaleDateString('en-CA', { timeZone: 'America/Sao_Paulo' });
async function openEntries(f: TimeFactory) {
  const rows = await f.get<Array<{ entry: TimeEntry }>>(`/api/me/time/entries?date=${today()}`);
  return rows.map(row => row.entry).filter(entry => entry.endedAt === null);
}
async function board(page: Page, f: TimeFactory, reload = false) {
  await navigateWithApi(page, [`/api/WorkItems/board/${f.board.id}`], () => reload
    ? page.reload({ waitUntil: 'domcontentloaded' })
    : page.goto(`/boards/${f.board.id}`, { waitUntil: 'domcontentloaded' }));
  await expect(page.getByLabel('Selecionar quadro')).toHaveValue(f.board.id);
}

for (const surface of ['Kanban', 'Meu trabalho'] as const) {
  test(`H16: duplo clique no ${surface} envia um único início enquanto pendente`, async ({ page, request }) => {
    await withTimeFactory(page, request, async f => {
      const task = await f.task();
      if (surface === 'Kanban') await board(page, f);
      else await navigateWithApi(page, ['/api/me/work'], () => page.goto('/me/tasks', { waitUntil: 'domcontentloaded' }));
      const button = surface === 'Kanban'
        ? page.getByRole('heading', { name: new RegExp(task.title) }).locator('..').getByRole('button', { name: 'Iniciar cronômetro da tarefa' })
        : page.getByRole('button', { name: `Abrir tarefa`, exact: false }).filter({ hasText: task.title }).getByTitle('Iniciar timer');
      let release!: () => void;
      const gate = new Promise<void>(resolve => { release = resolve; });
      let reached!: () => void;
      const requested = new Promise<void>(resolve => { reached = resolve; });
      let requests = 0;
      await page.route(startPath, async route => {
        requests++; reached(); await gate; await route.continue();
      });
      const started = responseTo(page, 'POST', '/api/TimeEntries/start');
      try {
        await button.dblclick(); await requested;
        await expect(button).toBeDisabled();
        await expect(page.getByRole('dialog')).toHaveCount(0);
        expect(requests).toBe(1);
        release(); expect((await started).status()).toBe(200);
        const running = await openEntries(f);
        expect(running).toHaveLength(1);
        expect(running[0].workItemId).toBe(task.id);
        const stop = surface === 'Kanban'
          ? page.getByRole('button', { name: 'Parar cronômetro da tarefa' })
          : page.getByTitle('Parar timer');
        await expect(stop).toBeEnabled();
        const stopped = responseTo(page, 'POST', '/api/TimeEntries/stop');
        await stop.click(); expect((await stopped).status()).toBe(200);
        expect(await openEntries(f)).toEqual([]);
      } finally { release(); await page.unroute(startPath); }
    });
  });
}

for (const differentTasks of [false, true]) {
  test(`H16: inícios HTTP paralelos em ${differentTasks ? 'tarefas diferentes' : 'uma tarefa'} deixam só um aberto`, async ({ page, request }) => {
    await withTimeFactory(page, request, async f => {
      const first = await f.task(); const second = await f.task();
      for (let round = 0; round < 3; round++) {
        const replies = await Promise.all([f.start(first.id), f.start(differentTasks ? second.id : first.id)]);
        const running = await openEntries(f);
        expect(running).toHaveLength(1);
        expect(replies.map(reply => reply.id)).toContain(running[0].id);
        if (differentTasks) expect(new Set(replies.map(reply => reply.id)).size).toBe(2);
        else expect(replies[0].id).toBe(replies[1].id);
        expect((await f.api('/api/TimeEntries/stop', 'POST', { workItemId: running[0].workItemId })).status()).toBe(200);
        expect(await openEntries(f)).toEqual([]);
      }
    });
  });
}

test('H16: duas abas iniciando tarefas diferentes não deixam cronômetro oculto após recarga', async ({ page, request }) => {
  await withTimeFactory(page, request, async f => {
    const first = await f.task(); const second = await f.task();
    const other = await page.context().newPage();
    let release!: () => void;
    const gate = new Promise<void>(resolve => { release = resolve; });
    let bothReached!: () => void;
    const bothRequested = new Promise<void>(resolve => { bothReached = resolve; });
    let requests = 0;
    try {
      await board(page, f); await board(other, f);
      for (const tab of [page, other]) await tab.route(startPath, async route => {
        requests++; if (requests === 2) bothReached(); await gate; await route.continue();
      });
      const firstResponse = responseTo(page, 'POST', '/api/TimeEntries/start');
      const secondResponse = responseTo(other, 'POST', '/api/TimeEntries/start');
      await Promise.all([
        page.getByRole('heading', { name: new RegExp(first.title) }).locator('..').getByRole('button', { name: 'Iniciar cronômetro da tarefa' }).click(),
        other.getByRole('heading', { name: new RegExp(second.title) }).locator('..').getByRole('button', { name: 'Iniciar cronômetro da tarefa' }).click(),
      ]);
      await bothRequested; expect(requests).toBe(2);
      release();
      for (const response of await Promise.all([firstResponse, secondResponse])) expect(response.status()).toBe(200);
      const running = await openEntries(f);
      expect(running).toHaveLength(1);
      expect([first.id, second.id]).toContain(running[0].workItemId);
      await board(page, f, true); await board(other, f, true);
      for (const tab of [page, other]) {
        await expect(tab.getByRole('button', { name: 'Parar cronômetro da tarefa' })).toHaveCount(1);
        await expect(tab.getByRole('button', { name: 'Iniciar cronômetro da tarefa' })).toHaveCount(1);
      }
      const stopped = responseTo(page, 'POST', '/api/TimeEntries/stop');
      await page.getByRole('button', { name: 'Parar cronômetro da tarefa' }).click();
      expect((await stopped).status()).toBe(200);
      expect(await openEntries(f)).toEqual([]);
    } finally {
      release(); await page.unroute(startPath); await other.close();
    }
  });
});
