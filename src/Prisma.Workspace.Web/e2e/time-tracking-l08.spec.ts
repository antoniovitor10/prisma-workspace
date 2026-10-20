import type { Page } from '@playwright/test';
import { test, expect, navigateWithApi } from './fixtures/functional';
import { withTimeFactory, withRestrictedTime, type TimeFactory, type TimeEntry, type TimeTotal, type DayTime,
  type WeeklyTime, type Justification } from './fixtures/time';

test.use({ timezoneId: 'America/Sao_Paulo' });
const drawer = (page: Page) => page.locator('[role="dialog"][aria-describedby^="task-description-"]');
const timerStatus = (page: Page) => drawer(page).getByRole('heading', { name: 'Apontamento de horas', exact: true }).locator('..').getByRole('status');
const responseTo = (page: Page, method: string, path: string) => page.waitForResponse(response =>
  response.request().method() === method && new URL(response.url()).pathname.toLowerCase() === path.toLowerCase(), { timeout: 8_000 })
  .catch(error => { throw new Error(`Resposta ${method} ${path} não chegou.`, { cause: error }); });
async function openTask(page: Page, f: TimeFactory, id: string) {
  await navigateWithApi(page, [`/api/WorkItems/${id}`],
    () => page.goto(`/projects/${f.boards.projectId}/backlog?item=${id}`, { waitUntil: 'domcontentloaded' }));
  await expect(drawer(page)).toHaveAttribute('aria-describedby', `task-description-${id}`);
  await expect(drawer(page).getByRole('button', { name: /(?:Iniciar|Parar) cronômetro/, exact: true })).toBeEnabled();
}
async function toggle(page: Page, action: 'start' | 'stop') {
  const response = responseTo(page, 'POST', `/api/TimeEntries/${action}`);
  await drawer(page).getByRole('button', { name: action === 'start' ? 'Iniciar cronômetro' : 'Parar cronômetro', exact: true }).click();
  const value = await response; expect(value.status()).toBe(200);
  const entry: TimeEntry = await value.json();
  await expect(timerStatus(page)).toContainText(action === 'start'
    ? 'Cronômetro desta tarefa em andamento' : 'Nenhum cronômetro ativo nesta sessão.');
  return entry;
}
const entries = (f: TimeFactory, id: string) => f.get<TimeEntry[]>(`/api/TimeEntries/work-item/${id}`);
const total = (f: TimeFactory, id: string) => f.get<TimeTotal>(`/api/TimeEntries/work-item/${id}/total`);
function exactInterval(entry: TimeEntry) {
  expect(entry.endedAt).not.toBeNull();
  expect(entry.durationSeconds).toBe(Math.floor((Date.parse(entry.endedAt!) - Date.parse(entry.startedAt)) / 1000));
}
async function openDay(page: Page, f: TimeFactory, date: string) {
  await navigateWithApi(page, [`/api/WorkItems/board/${f.board.id}`],
    () => page.goto(`/boards/${f.board.id}`, { waitUntil: 'domcontentloaded' }));
  await expect(page.getByLabel('Selecionar quadro')).toHaveValue(f.board.id);
  await expect(page.getByRole('button', { name: `Editar coluna ${f.stage.name}`, exact: true })).toBeVisible();
  const weekly = responseTo(page, 'GET', '/api/TimeEntries/my/weekly');
  await page.getByRole('button', { name: 'Meu Tempo', exact: true }).click();
  const response = await weekly; expect(response.status()).toBe(200);
  const week: WeeklyTime = await response.json();
  const index = week.days.findIndex(day => day.date === date); expect(index).toBeGreaterThanOrEqual(0);
  const daily = responseTo(page, 'GET', '/api/me/time/daily-by-task');
  const justifications = responseTo(page, 'GET', '/api/me/time/justifications');
  await page.getByRole('heading', { name: 'Meu Tempo', exact: true }).locator('..').getByRole('button', { name: 'Ajustar', exact: true }).nth(index).click();
  expect((await daily).status()).toBe(200); expect((await justifications).status()).toBe(200);
  return page.getByRole('heading', { name: /^Ajustar: / }).locator('..');
}
const today = (page: Page) => page.evaluate(() => {
  const now = new Date(); return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
});

test('L08 cronômetro start/stop pela UI grava um intervalo positivo e total exato', async ({ page, request }) => {
  await withTimeFactory(page, request, async f => {
    const task = await f.task(); await openTask(page, f, task.id);
    const started = await toggle(page, 'start');
    expect(started).toMatchObject({ workItemId: task.id, userId: f.person.id, endedAt: null });
    await expect.poll(async () => (await total(f, task.id)).totalSeconds).toBeGreaterThan(0);
    const stopped = await toggle(page, 'stop');
    expect(stopped.id).toBe(started.id); exactInterval(stopped); expect(stopped.durationSeconds).toBeGreaterThan(0);
    expect(await entries(f, task.id)).toEqual([expect.objectContaining({ id: stopped.id, workItemId: task.id,
      userId: f.person.id, startedAt: stopped.startedAt, endedAt: stopped.endedAt, durationSeconds: stopped.durationSeconds,
      note: null, displayName: f.person.name })]);
    expect(await total(f, task.id)).toMatchObject({ totalSeconds: stopped.durationSeconds });
    expect((await f.api('/api/TimeEntries/running')).status()).toBe(204);
  });
});

test('L08 trocar tarefa na UI encerra o anterior e mantém somente um cronômetro ativo', async ({ page, request }) => {
  await withTimeFactory(page, request, async f => {
    const first = await f.task(); const second = await f.task(); const old = await f.start(first.id);
    await openTask(page, f, second.id);
    await expect(timerStatus(page)).toHaveText('Há um cronômetro em outra tarefa. Ao iniciar aqui, ele será encerrado automaticamente.');
    const current = await toggle(page, 'start'); expect(current.id).not.toBe(old.id);
    expect(await f.get<TimeEntry>('/api/TimeEntries/running')).toMatchObject({ id: current.id, workItemId: second.id });
    const previous = await entries(f, first.id); const next = await entries(f, second.id);
    expect(previous).toHaveLength(1); expect(previous[0].id).toBe(old.id); exactInterval(previous[0]);
    expect(Date.parse(previous[0].endedAt!)).toBeLessThanOrEqual(Date.parse(current.startedAt));
    expect([...previous, ...next].filter(entry => entry.endedAt === null).map(entry => entry.id)).toEqual([current.id]);
    const stopped = await toggle(page, 'stop'); exactInterval(stopped);
    expect(await total(f, first.id)).toMatchObject({ totalSeconds: previous[0].durationSeconds });
    expect(await total(f, second.id)).toMatchObject({ totalSeconds: stopped.durationSeconds });
  });
});

test('L08 reload restaura cronômetro ativo; reiniciar a mesma tarefa é idempotente', async ({ page, request }) => {
  await withTimeFactory(page, request, async f => {
    const task = await f.task(); const started = await f.start(task.id); await openTask(page, f, task.id);
    // A Topbar hidrata a query compartilhada active-timer por este endpoint na recarga.
    const [response] = await navigateWithApi(page, ['/api/me/active-timer'],
      () => page.reload({ waitUntil: 'domcontentloaded' }));
    expect(await response.json()).toMatchObject({ id: started.id, startedAt: started.startedAt, workItemId: task.id });
    await expect(timerStatus(page)).toContainText('Cronômetro desta tarefa em andamento');
    expect(await f.start(task.id)).toMatchObject({ id: started.id, startedAt: started.startedAt });
    expect(await entries(f, task.id)).toEqual([expect.objectContaining({ id: started.id, endedAt: null })]);
    const stopped = await toggle(page, 'stop'); exactInterval(stopped);
    expect(await total(f, task.id)).toMatchObject({ totalSeconds: stopped.durationSeconds });
  });
});

test('L08 manual na UI recusa fim igual, persiste observação e totaliza segundos sem blocos', async ({ page, request }) => {
  await withTimeFactory(page, request, async f => {
    const task = await f.task(); const note = f.boards.name('observação'); await openTask(page, f, task.id);
    await drawer(page).getByLabel('Início do apontamento', { exact: true }).fill('2026-10-12T14:00');
    await drawer(page).getByLabel('Fim do apontamento', { exact: true }).fill('2026-10-12T14:00');
    const refused = responseTo(page, 'POST', '/api/TimeEntries/manual');
    await drawer(page).getByRole('button', { name: 'Registrar manualmente', exact: true }).click();
    const invalid = await refused; expect(invalid.status()).toBe(400);
    expect(await invalid.json()).toMatchObject({ type: 'validation_error', title: 'Erro de validação', status: 400 });
    await expect(drawer(page).getByRole('alert')).toHaveText('Erro de validação');
    expect(await entries(f, task.id)).toEqual([]);
    await drawer(page).getByLabel('Fim do apontamento', { exact: true }).fill('2026-10-12T14:02');
    await drawer(page).getByPlaceholder('Observação do apontamento (opcional)').fill(note);
    const created = responseTo(page, 'POST', '/api/TimeEntries/manual');
    await drawer(page).getByRole('button', { name: 'Registrar manualmente', exact: true }).click();
    const response = await created; expect(response.status()).toBe(201);
    const entry: TimeEntry = await response.json();
    expect(entry).toMatchObject({ workItemId: task.id, userId: f.person.id, note, durationSeconds: 120 });
    expect(Date.parse(entry.startedAt)).toBe(Date.parse('2026-10-12T14:00:00-03:00')); exactInterval(entry);
    await expect(drawer(page).getByRole('alert')).toHaveCount(0);
    const precise = await f.manual(task.id, '2026-10-12T14:03:00-03:00', '2026-10-12T14:04:01-03:00');
    expect(precise.durationSeconds).toBe(61); exactInterval(precise);
    expect(await total(f, task.id)).toMatchObject({ totalSeconds: 181 });
    await openTask(page, f, task.id);
    const summary = drawer(page).getByRole('region', { name: 'Tempo geral por responsável' });
    await expect(summary.locator('article')).toHaveCount(1); await expect(summary).toContainText('0h 03min');
    const log = drawer(page).locator('details').filter({ has: page.getByText('Log de apontamentos (2)', { exact: true }) });
    await expect(log).not.toHaveAttribute('open', '');
    await log.locator('summary').click(); await expect(log).toContainText(note);
    expect((await entries(f, task.id)).map(value => value.id).sort()).toEqual([entry.id, precise.id].sort());
  });
});

test('L08 diário mostra horas próprias e justificativa da UI persiste em consulta nova', async ({ page, request }) => {
  await withTimeFactory(page, request, async f => {
    const date = await today(page); f.trackDay(date); const task = await f.task();
    await f.manual(task.id, `${date}T09:00:00-03:00`, `${date}T09:03:00-03:00`);
    const modal = await openDay(page, f, date);
    await expect(modal).toContainText(task.title); await expect(modal).toContainText('0h03');
    await modal.getByRole('button', { name: 'Justificar o dia', exact: true }).click();
    await modal.getByRole('combobox').selectOption('Outro'); await modal.getByPlaceholder('Horas', { exact: true }).fill('2');
    const created = responseTo(page, 'POST', '/api/me/time/justifications');
    await modal.getByRole('button', { name: 'Justificar', exact: true }).click();
    const response = await created; expect(response.status()).toBe(200);
    const value: Justification = await response.json(); expect(value).toMatchObject({ reason: 'Outro', hours: 2 });
    await expect(modal.getByRole('button', { name: 'Remover justificativa Outro', exact: true })).toBeVisible();
    expect(await f.get<Justification[]>(`/api/me/time/justifications?date=${date}`)).toEqual([value]);
    expect(await f.get<DayTime[]>(`/api/me/time/daily-by-task?date=${date}`)).toEqual([{ workItemId: task.id, title: task.title, seconds: 180 }]);
  });
});

test('L08 remover justificativa pela UI persiste sem alterar horas da tarefa', async ({ page, request }) => {
  await withTimeFactory(page, request, async f => {
    const date = await today(page); f.trackDay(date); const task = await f.task();
    await f.manual(task.id, `${date}T10:00:00-03:00`, `${date}T10:02:00-03:00`);
    const created = await f.api('/api/me/time/justifications', 'POST', { date, reason: 'Outro', hours: 2 });
    expect(created.status()).toBe(200); const value: Justification = await created.json();
    const modal = await openDay(page, f, date); await modal.getByRole('button', { name: 'Justificar o dia', exact: true }).click();
    const removed = responseTo(page, 'DELETE', `/api/me/time/justifications/${value.id}`);
    await modal.getByRole('button', { name: 'Remover justificativa Outro', exact: true }).click(); expect((await removed).status()).toBe(204);
    await expect(modal.getByRole('button', { name: 'Remover justificativa Outro', exact: true })).toHaveCount(0);
    expect(await f.get<Justification[]>(`/api/me/time/justifications?date=${date}`)).toEqual([]);
    expect(await total(f, task.id)).toMatchObject({ totalSeconds: 120 });
  });
});

test('L08 negação: sem vínculo não inicia timer, lança horas nem consulta apontamentos', async ({ page, request }) => {
  await withTimeFactory(page, request, async f => {
    const date = await today(page);
    await withRestrictedTime(f, date, async ({ factory, task }) => {
      for (const endpoint of ['start', 'manual']) {
        const denied = await f.api(`/api/TimeEntries/${endpoint}`, 'POST', {
          workItemId: task.id, startedAt: `${date}T13:00:00-03:00`, endedAt: `${date}T13:01:00-03:00`,
        });
        expect(denied.status(), endpoint).toBe(404);
      }
      expect((await f.api(`/api/TimeEntries/work-item/${task.id}`)).status()).toBe(404);
      expect((await f.api(`/api/TimeEntries/work-item/${task.id}/total`)).status()).toBe(404);
      expect((await f.api('/api/TimeEntries/running')).status()).toBe(204);
      expect(await factory.get<TimeEntry[]>(`/api/TimeEntries/work-item/${task.id}`)).toHaveLength(1);
      expect(await factory.get<TimeTotal>(`/api/TimeEntries/work-item/${task.id}/total`)).toMatchObject({ totalSeconds: 60 });
    });
  });
});

test('L08 relatório do membro não deve expor horas de pessoa ou projeto sem acesso', async ({ page, request }) => {
  let forbidden: { projects: string[]; otherUsers: string[] } | undefined;
  await withTimeFactory(page, request, async f => {
    const date = await today(page); const task = await f.task();
    await f.manual(task.id, `${date}T11:00:00-03:00`, `${date}T11:02:00-03:00`);
    await withRestrictedTime(f, date, async ({ task: restricted, name }) => {
      expect((await f.api(`/api/TimeEntries/work-item/${restricted.id}`)).status()).toBe(404);
      const report = await f.get<{ byResponsible: Array<{ userId: string; projects: Array<{ projectName: string }> }> }>(`/api/reports/hours?from=${date}&to=${date}`);
      const own = report.byResponsible.find(person => person.userId === f.person.id);
      expect(own).toBeDefined();
      const project = await f.boards.get<{ name: string }>(`/api/projects/${f.boards.projectId}`);
      expect(own!.projects.map(value => value.projectName)).toContain(project.name);
      forbidden = { projects: report.byResponsible.flatMap(person => person.projects.map(value => value.projectName)).filter(value => value === name),
        otherUsers: report.byResponsible.filter(person => person.userId !== f.person.id).map(person => person.userId) };
    });
  });
  // B07 / SPEC-TIME-TRACKING / SPEC-DASHBOARDS-REPORTS: agregação ignora acesso e retorna horas de terceiro.
  // Guardas de consulta, controle positivo e cleanup são verificados antes da marcação.
  test.fail(true, 'B07: /api/reports/hours expõe pessoa, projeto e horas sem acesso ao membro.');
  expect(forbidden).toEqual({ projects: [], otherUsers: [] });
});

test('L08 períodos: apontamento sobreposto deve ser recusado sem aumentar o total', async ({ page, request }) => {
  let evidence: { status: number; count: number; seconds: number } | undefined;
  await withTimeFactory(page, request, async f => {
    const task = await f.task(); await f.manual(task.id, '2026-10-12T09:00:00-03:00', '2026-10-12T09:01:01-03:00');
    const refused = await f.api('/api/TimeEntries/manual', 'POST', {
      workItemId: task.id, startedAt: '2026-10-12T09:00:30-03:00', endedAt: '2026-10-12T09:01:30-03:00',
    });
    evidence = { status: refused.status(), count: (await entries(f, task.id)).length, seconds: (await total(f, task.id)).totalSeconds };
  });
  // Gap 2 aprovado em SPEC-TIME-TRACKING: handler manual não procura períodos sobrepostos.
  test.fail(true, 'SPEC-TIME-TRACKING: sobreposição aceita e total contado duas vezes.');
  expect(evidence).toEqual({ status: 400, count: 1, seconds: 61 });
});

test('L08 Brasília: diário divide o intervalo na meia-noite local', async ({ page, request }) => {
  await withTimeFactory(page, request, async f => {
    const task = await f.task(); await f.manual(task.id, '2026-10-12T23:59:30-03:00', '2026-10-13T00:00:30-03:00');
    expect(await total(f, task.id)).toMatchObject({ totalSeconds: 60 });
    const monday = await f.get<DayTime[]>('/api/me/time/daily-by-task?date=2026-10-12');
    const tuesday = await f.get<DayTime[]>('/api/me/time/daily-by-task?date=2026-10-13');
    expect([monday, tuesday].map(day => day.filter(entry => entry.workItemId === task.id)
      .reduce((sum, entry) => sum + entry.seconds, 0))).toEqual([30, 30]);
  });
});

test('L08 Brasília: semana divide o intervalo na meia-noite local', async ({ page, request }) => {
  await withTimeFactory(page, request, async f => {
    const task = await f.task(); await f.manual(task.id, '2026-10-12T23:59:30-03:00', '2026-10-13T00:00:30-03:00');
    expect(await total(f, task.id)).toMatchObject({ totalSeconds: 60 });
    const weekly = await f.get<WeeklyTime>('/api/TimeEntries/my/weekly?weekStart=2026-10-12');
    expect(weekly.weekStart).toBe('2026-10-12');
    expect(weekly.days).toHaveLength(7);
    expect(weekly.days.slice(0, 2).map(day => day.totalSeconds)).toEqual([30, 30]);
  });
});
