import type { Locator, Page } from '@playwright/test';
import { test, expect, navigateWithApi } from './fixtures/functional';
import { withTimeFactory, type TimeFactory, type TimeEntry, type TimeTotal, type WeeklyTime } from './fixtures/time';

test.use({ timezoneId: 'America/Sao_Paulo' });
const today = () => new Date().toLocaleDateString('en-CA', { timeZone: 'America/Sao_Paulo' });
const responseTo = (page: Page, method: string, path: string) => page.waitForResponse(r =>
  r.request().method() === method && new URL(r.url()).pathname.toLowerCase() === path.toLowerCase());
const total = (f: TimeFactory, id: string) => f.get<TimeTotal>(`/api/TimeEntries/work-item/${id}/total`);
async function openBoard(page: Page, f: TimeFactory, reload = false) {
  await navigateWithApi(page, [`/api/WorkItems/board/${f.board.id}`], () => reload
    ? page.reload({ waitUntil: 'domcontentloaded' }) : page.goto(`/boards/${f.board.id}`, { waitUntil: 'domcontentloaded' }));
}
async function openWeek(page: Page, f: TimeFactory) {
  await openBoard(page, f);
  const response = responseTo(page, 'GET', '/api/TimeEntries/my/weekly');
  await page.getByRole('button', { name: 'Meu Tempo', exact: true }).click();
  const week: WeeklyTime = await (await response).json();
  return { week, modal: page.getByRole('heading', { name: 'Meu Tempo', exact: true }).locator('..') };
}
async function openDay(page: Page, f: TimeFactory) {
  const { week, modal } = await openWeek(page, f);
  const index = week.days.findIndex(d => d.date === today());
  expect(index).toBeGreaterThanOrEqual(0);
  const loaded = responseTo(page, 'GET', '/api/me/time/entries');
  await modal.getByRole('button', { name: 'Ajustar', exact: true }).nth(index).click();
  expect((await loaded).status()).toBe(200);
  return page.getByRole('heading', { name: /^Ajustar: / }).locator('..');
}
async function compareLiveWithApi(counter: Locator, f: TimeFactory, taskId: string) {
  await expect(counter).toBeVisible();
  const before = await total(f, taskId);
  const [h, m, s] = (await counter.innerText()).split(':').map(Number);
  const after = await total(f, taskId);
  const seconds = h * 3600 + m * 60 + s;
  expect(seconds).toBeGreaterThanOrEqual(before.totalSeconds - 2);
  expect(seconds, 'Contador ao vivo não pode superar o total consultado depois dele.').toBeLessThanOrEqual(after.totalSeconds);
}

for (const surface of ['card', 'Meu trabalho'] as const) {
  test(`timer ativo após reload no ${surface} não fica acima de /total`, async ({ page, request }) => {
    await withTimeFactory(page, request, async f => {
      const task = await f.task();
      await f.manual(task.id, '2026-10-12T00:00:00-03:00', '2026-10-12T00:02:00-03:00');
      await f.start(task.id);
      await expect.poll(async () => (await total(f, task.id)).totalSeconds).toBeGreaterThanOrEqual(124);
      if (surface === 'card') {
        await openBoard(page, f);
        await openBoard(page, f, true);
        const card = page.getByRole('heading', { name: new RegExp(task.title) }).locator('..');
        await compareLiveWithApi(card.getByText(/^\d{2}:\d{2}:\d{2}$/, { exact: true }), f, task.id);
      } else {
        await page.goto('/me/tasks', { waitUntil: 'domcontentloaded' });
        await navigateWithApi(page, ['/api/me/work'], () => page.reload({ waitUntil: 'domcontentloaded' }));
        const card = page.getByRole('button', { name: new RegExp(`^Abrir tarefa \\d+: ${task.title}$`) });
        await compareLiveWithApi(card.getByText(/^\d{2}:\d{2}:\d{2}$/, { exact: true }), f, task.id);
      }
    });
  });
}

test('B07: relatório do membro contém só horas próprias; admin mantém o recorte autorizado', async ({ page, request }) => {
  await withTimeFactory(page, request, async f => {
    const task = await f.task();
    const other = await f.boards.member();
    const otherTask = await f.boards.item(f.board.id, f.stage.id, { responsibleId: other.id });
    const date = '2026-10-12';
    await f.manual(task.id, `${date}T09:00:00-03:00`, `${date}T09:02:00-03:00`);
    const created = await f.boards.api('/api/TimeEntries/manual', 'POST', { workItemId: otherTask.id,
      startedAt: `${date}T09:00:00-03:00`, endedAt: `${date}T09:03:00-03:00` }, other);
    expect(created.status()).toBe(201);
    type Report = { byResponsible: Array<{ userId: string; projects: Array<{ projectName: string }> }> };
    const report = await f.get<Report>(`/api/reports/hours?from=${date}&to=${date}`);
    expect(report.byResponsible.map(row => row.userId)).toEqual([f.person.id]);
    expect(report.byResponsible.filter(row => row.userId !== f.person.id)).toEqual([]); // Oráculo L08/B07.
    const filtered = await f.get<Report>(`/api/reports/hours?from=${date}&to=${date}&userId=${other.id}`);
    expect(filtered.byResponsible).toEqual([]);
    const admin = await f.boards.get<Report>(`/api/reports/hours?from=${date}&to=${date}`);
    expect(admin.byResponsible.map(row => row.userId)).toEqual(expect.arrayContaining([f.person.id, other.id]));
  });
});

test('Brasília: período 22h–02h contribui 2h em cada dia no diário, semana e relatório', async ({ page, request }) => {
  await withTimeFactory(page, request, async f => {
    const task = await f.task();
    const entry = await f.manual(task.id, '2026-10-12T22:00:00-03:00', '2026-10-13T02:00:00-03:00');
    for (const date of ['2026-10-12', '2026-10-13']) {
      const daily = await f.get<Array<{ workItemId: string; seconds: number }>>(`/api/me/time/daily-by-task?date=${date}`);
      expect(daily).toEqual([expect.objectContaining({ workItemId: task.id, seconds: 7200 })]);
      expect(await f.get<{ totalHours: number }>(`/api/reports/hours?from=${date}&to=${date}`)).toMatchObject({ totalHours: 2 });
      const rows = await f.get<Array<{ entry: TimeEntry }>>(`/api/me/time/entries?date=${date}`);
      expect(rows.map(row => row.entry.id)).toEqual([entry.id]);
    }
    const week = await f.get<WeeklyTime>('/api/TimeEntries/my/weekly?weekStart=2026-10-12');
    expect(week.days.map(day => day.totalSeconds)).toEqual([7200, 7200, 0, 0, 0, 0, 0]);
    expect(await total(f, task.id)).toMatchObject({ totalSeconds: 14400 });
  });
});

test('criação manual recusa futuro e sobreposição sem alterar o total anterior', async ({ page, request }) => {
  await withTimeFactory(page, request, async f => {
    const task = await f.task();
    await f.manual(task.id, '2026-10-12T09:00:00-03:00', '2026-10-12T09:01:00-03:00');
    const future = new Date(Date.now() + 24 * 3600_000);
    const periods = [
      { startedAt: '2026-10-12T09:00:30-03:00', endedAt: '2026-10-12T09:01:30-03:00' },
      { startedAt: future.toISOString(), endedAt: new Date(future.getTime() + 60_000).toISOString() },
    ];
    for (const period of periods) {
      const response = await f.api('/api/TimeEntries/manual', 'POST', { workItemId: task.id, ...period });
      expect(response.status()).toBe(400);
    }
    expect(await f.get<TimeEntry[]>(`/api/TimeEntries/work-item/${task.id}`)).toHaveLength(1);
    expect(await total(f, task.id)).toMatchObject({ totalSeconds: 60 });
  });
});

test('dois ajustes pela UI escolhem períodos livres e permitem editar observação', async ({ page, request }) => {
  await withTimeFactory(page, request, async f => {
    const task = await f.task();
    for (let index = 0; index < 2; index++) {
      const modal = await openDay(page, f);
      await modal.getByRole('combobox', { name: 'Buscar tarefa por título ou número' }).fill(task.title);
      await modal.getByRole('option').filter({ hasText: task.title }).click();
      await modal.getByPlaceholder('Minutos a adicionar').fill('1');
      const created = responseTo(page, 'POST', '/api/TimeEntries/manual');
      await modal.getByRole('button', { name: 'Adicionar', exact: true }).click();
      expect((await created).status()).toBe(201);
      await expect(modal).toBeHidden();
    }
    const entries = await f.get<TimeEntry[]>(`/api/TimeEntries/work-item/${task.id}`);
    expect(entries).toHaveLength(2);
    const ordered = entries.sort((a, b) => Date.parse(a.startedAt) - Date.parse(b.startedAt));
    expect(Date.parse(ordered[0].endedAt!)).toBeLessThanOrEqual(Date.parse(ordered[1].startedAt));
    const modal = await openDay(page, f);
    const row = modal.getByRole('article', { name: `Lançamento ${task.title}`, exact: true }).first();
    await row.getByRole('button', { name: 'Editar lançamento', exact: true }).click();
    await row.getByRole('textbox', { name: 'Observação', exact: true }).fill('Observação corrigível');
    const saved = page.waitForResponse(r => r.request().method() === 'PUT' && /\/api\/timeentries\//i.test(r.url()));
    await row.getByRole('button', { name: 'Salvar lançamento', exact: true }).click();
    expect((await saved).status()).toBe(200);
    await expect(row).toContainText('Observação corrigível');
    expect(await total(f, task.id)).toMatchObject({ totalSeconds: 120 });
  });
});

test('Meu Tempo permite alcançar e ajustar os sete dias por toque', async ({ page, request }, testInfo) => {
  await withTimeFactory(page, request, async f => {
    const { week, modal } = await openWeek(page, f);
    expect(week.days).toHaveLength(7);
    const grid = modal.getByRole('region', { name: 'Horas da semana' });
    for (let index = 0; index < 7; index++) {
      const button = grid.getByRole('button', { name: 'Ajustar', exact: true }).nth(index);
      await button.scrollIntoViewIfNeeded();
      const loaded = responseTo(page, 'GET', '/api/me/time/entries');
      await button.click();
      expect((await loaded).status()).toBe(200);
      const adjust = page.getByRole('heading', { name: /^Ajustar: / }).locator('..');
      await expect(adjust).toBeVisible();
      await adjust.getByRole('button', { name: 'Fechar', exact: true }).click();
    }
    await grid.evaluate(element => { element.scrollLeft = 0; });
    await expect(grid.getByRole('button', { name: 'Ajustar', exact: true }).first()).toBeInViewport();
    await page.screenshot({ path: testInfo.outputPath('sete-dias-acessiveis.png') });
  });
});
