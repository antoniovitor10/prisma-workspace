import { randomUUID } from 'node:crypto';
import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { test, expect } from './fixtures/test';
const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5400';

test('nova tarefa entra antes de zero sem reordenar cartões de outras pessoas ou colunas', async ({ page, request, authenticatedGoto }, info) => {
  await page.setViewportSize(info.project.name.includes('mobile') ? { width: 390, height: 844 } : { width: 1440, height: 900 });
  await authenticatedGoto('/projects');
  const session = await page.evaluate(() => ({ token: localStorage.getItem('prisma_workspace_token')!, organizationId: localStorage.getItem('prisma_workspace_organization')! }));
  const headers = { Authorization: `Bearer ${session.token}`, 'X-Organization-Id': session.organizationId };
  const claims = JSON.parse(Buffer.from(session.token.split('.')[1], 'base64url').toString());
  const actor: string = claims.sub ?? claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'];
  const name = `Oficina ordem ${randomUUID()}`;
  const project = await request.post(`${apiUrl}/api/projects`, { headers, data: { name, description: 'Fixture própria TASK210.', methodology: 1, nature: 1, workType: 1 } });
  expect(project.status()).toBe(201); const projectId: string = await project.json();
  try {
    const board = await request.post(`${apiUrl}/api/Boards`, { headers, data: { name, projectId } });
    expect(board.status()).toBe(201); const boardId: string = await board.json();
    const columns = await request.get(`${apiUrl}/api/Stages/board/${boardId}`, { headers });
    expect(columns.status()).toBe(200); const stages: Array<{ id: string; name: string }> = await columns.json();
    expect(stages.length).toBeGreaterThanOrEqual(2);
    const original: Array<{ id: string; position: number }> = [];
    for (const [index, position] of [0, 200, -500].entries()) {
      const created = await request.post(`${apiUrl}/api/WorkItems`, { headers, data: { boardId, stageId: stages[index === 2 ? 1 : 0].id,
        title: `${name} original ${index}`, responsibleId: actor, priority: 1, position } });
      expect(created.status()).toBe(201); original.push({ id: await created.json(), position });
    }
    await page.goto(`/boards/${boardId}`);
    const column = page.locator(`[data-stage-id="${stages[0].id}"]`);
    await expect(column.getByRole('combobox', { name: `Ordenar cartões da coluna ${stages[0].name}`, exact: true })).toHaveValue('position');
    const directory = resolve('../../.runlogs/oficina/screenshots'); mkdirSync(directory, { recursive: true });
    for (const index of [1, 2]) {
      const title = `${name} nova ${index}`;
      await column.getByRole('button', { name: 'Novo Card', exact: true }).click();
      await page.getByPlaceholder('Título do Card', { exact: true }).fill(title);
      await page.getByRole('combobox', { name: 'Responsável principal da nova tarefa', exact: true }).selectOption(actor);
      const saving = page.waitForResponse(response => response.request().method() === 'POST' && new URL(response.url()).pathname.toLowerCase() === '/api/workitems');
      await page.getByRole('button', { name: 'Criar Card', exact: true }).click();
      const saved = await saving; expect(saved.status()).toBe(201);
      expect(saved.request().postDataJSON().position).toBe(-100 * index);
      await expect(column.getByRole('checkbox').first()).toHaveAttribute('aria-label', new RegExp(title));
      await page.reload({ waitUntil: 'domcontentloaded' });
      await expect(column.getByRole('checkbox').first()).toHaveAttribute('aria-label', new RegExp(title));
    }
    const persisted = await request.get(`${apiUrl}/api/WorkItems/board/${boardId}`, { headers });
    expect(persisted.status()).toBe(200); const items: Array<{ id: string; position: number }> = await persisted.json();
    for (const item of original) expect(items.find(candidate => candidate.id === item.id)?.position).toBe(item.position);
    await page.screenshot({ path: `${directory}/TASK210-${info.project.name}-after.png`, scale: 'css' });
  } finally {
    expect((await request.post(`${apiUrl}/api/projects/${projectId}/archive`, { headers })).status()).toBe(204);
  }
});
