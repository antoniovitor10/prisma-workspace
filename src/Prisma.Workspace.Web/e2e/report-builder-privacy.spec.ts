import { test, expect } from './fixtures/functional';
import { cleanupFor } from './fixtures/test';
import { withTimeFactory, type TimeFactory } from './fixtures/time';

type Execution = { sourceRowCount: number; rows: Array<{ userId: string; hours?: number; realizedHours?: number }> };
// Os três apontamentos precisam ficar no período padrão de 30 dias da UI,
// inclusive quando o CI executar em outro mês; ontem também evita horários futuros.
const date = new Date(Date.now() - 86_400_000).toLocaleDateString('en-CA', { timeZone: 'America/Sao_Paulo' });
const definition = (source: number) => ({ columns: source === 7 ? ['userId', 'hours', 'note'] : ['userId', 'realizedHours'],
  metrics: [], filters: [], descending: false, from: date, to: date });
async function arrange(f: TimeFactory) {
  const task = await f.task();
  const other = await f.boards.member();
  const ownNote = f.boards.name('nota própria'); const otherNote = f.boards.name('nota de terceiro');
  await f.manual(task.id, `${date}T09:00:00-03:00`, `${date}T10:00:00-03:00`, ownNote);
  expect((await f.boards.api('/api/TimeEntries/manual', 'POST', { workItemId: task.id,
    startedAt: `${date}T10:00:00-03:00`, endedAt: `${date}T12:00:00-03:00`, note: otherNote }, other)).status()).toBe(201);
  // Criar os apontamentos com permissão de edição e depois provar um papel de leitura
  // que realmente tem ViewReport; Developer/TeamMember não exercitam esta brecha.
  expect((await f.boards.api(`/api/organizations/current/members/${f.person.id}`, 'PUT',
    { role: 10, isActive: true })).status()).toBe(204);
  return { other, ownNote, otherNote };
}

for (const [source, label] of [[7, 'Apontamentos'], [5, 'Usuários']] as const) {
  test(`B07: construtor ${label} restringe leitor às próprias horas sem filtro de projeto`, async ({ page, request }) => {
    await withTimeFactory(page, request, async f => {
      const { other, ownNote, otherNote } = await arrange(f);
      await page.goto('/reports', { waitUntil: 'domcontentloaded' });
      const catalog = page.waitForResponse(r => new URL(r.url()).pathname === '/api/reports/catalog');
      await page.getByRole('button', { name: 'Construtor', exact: true }).click();
      expect((await catalog).status()).toBe(200);
      await page.getByRole('combobox', { name: 'Fonte', exact: true }).selectOption(String(source));
      if (source === 7) await page.getByRole('checkbox', { name: 'Observação', exact: true }).check();
      const preview = page.waitForResponse(r => r.request().method() === 'POST'
        && new URL(r.url()).pathname === '/api/reports/preview');
      await page.getByRole('button', { name: 'Pré-visualizar', exact: true }).click();
      const response = await preview; expect(response.status()).toBe(200);
      expect(response.request().postDataJSON().projectId ?? null).toBeNull();
      const result: Execution = await response.json();
      expect(result.rows.map(r => r.userId)).toEqual([f.person.id]);
      expect(result.sourceRowCount).toBe(1);
      if (source === 5) expect(result.rows[0].realizedHours).toBe(1);
      const table = page.getByRole('table');
      await expect(table.getByRole('row')).toHaveCount(2);
      await expect(table.getByRole('cell', { name: f.person.id, exact: true })).toBeVisible();
      await expect(table).not.toContainText(other.id);
      if (source === 7) {
        await expect(table).toContainText(ownNote);
        await expect(table).not.toContainText(otherNote);
      } else await expect(table.getByRole('row').nth(1).getByRole('cell').last()).toHaveText('1');
    });
  });
}

test('B07: relatório compartilhado e CSV usam o escopo de quem executa, sem herdar o admin', async ({ page, request }) => {
  await withTimeFactory(page, request, async f => {
    const { other } = await arrange(f);
    const response = await f.boards.api('/api/reports/definitions', 'POST', {
      name: f.boards.name('relatório compartilhado'), source: 5, visualization: 1,
      definition: definition(5), isShared: true,
    });
    expect(response.status()).toBe(201);
    const report: { id: string } = await response.json();
    cleanupFor(request)(async () => {
      expect((await f.boards.api(`/api/reports/definitions/${report.id}`, 'DELETE')).status()).toBe(204);
    });
    const path = `/api/reports/definitions/${report.id}`;
    const mine = await f.api(`${path}/run`, 'POST'); expect(mine.status()).toBe(200);
    expect((await mine.json() as Execution).rows).toEqual([{ userId: f.person.id, realizedHours: 1 }]);
    const csv = await f.api(`${path}/export`); expect(csv.status()).toBe(200);
    const text = await csv.text(); expect(text).toContain(f.person.id); expect(text).not.toContain(other.id);
    const admin = await f.boards.api(`${path}/run`, 'POST'); expect(admin.status()).toBe(200);
    const rows = (await admin.json() as Execution).rows;
    expect(rows).toEqual(expect.arrayContaining([{ userId: f.person.id, realizedHours: 1 }, { userId: other.id, realizedHours: 2 }]));
  });
});
