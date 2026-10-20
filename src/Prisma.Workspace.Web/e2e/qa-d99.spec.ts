import { randomUUID } from 'node:crypto';
import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import type { Page, TestInfo } from '@playwright/test';
import { test, expect } from './fixtures/test';
import { withBoardFactory } from './fixtures/boards';
const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5400';
async function capture(page: Page, info: TestInfo, label: string) {
  const directory = resolve('../../.runlogs/oficina/screenshots');
  mkdirSync(directory, { recursive: true });
  await page.screenshot({ path: `${directory}/${label}-${info.project.name}-after.png`, scale: 'css' });
}
test.beforeEach(async ({ page }, info) => {
  await page.setViewportSize(info.project.name.includes('mobile') ? { width: 390, height: 844 } : { width: 1440, height: 900 });
});

test('D99 cadastro exige nome completo e preserva o nome ao criar a primeira organização', async ({ page, request }, info) => {
  const password = process.env.E2E_TEST_USER_PASSWORD;
  if (!password) throw new Error('Configure a senha de teste local.');
  const email = `oficina-name-${randomUUID()}@example.test`;
  const bad = await request.post(`${apiUrl}/api/auth/register`, { data: { email, password, fullName: 'Ana' } });
  expect(bad.status()).toBe(400);
  expect(JSON.stringify(await bad.json())).toContain('nome e sobrenome');
  await page.goto('/login');
  await page.evaluate(() => localStorage.clear());
  await page.context().clearCookies();
  await page.goto('/login');
  await page.getByRole('button', { name: 'registre-se', exact: true }).click();
  const name = page.getByRole('textbox', { name: 'Nome completo', exact: true });
  await name.fill('Ana'); await name.blur();
  await expect(name).toHaveAttribute('aria-invalid', 'true');
  await expect(page.getByRole('alert')).toContainText('Informe seu nome e sobrenome.');
  await name.fill('Ana Alves Oficina');
  await page.getByPlaceholder('seu.nome@empresa.com').fill(email);
  await page.getByPlaceholder('Digite sua senha', { exact: true }).fill(password);
  await capture(page, info, 'C5-nome-completo');
  const registered = page.waitForResponse(response => response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/auth/register');
  await page.getByRole('button', { name: 'Criar conta', exact: true }).click();
  expect((await registered).status()).toBe(202);
  await expect(page.getByRole('heading', { name: 'Crie sua organização' })).toBeVisible();
  await page.getByLabel('Nome da organização', { exact: true }).fill(`Oficina nome ${randomUUID()}`);
  const created = page.waitForResponse(response => response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/organizations');
  await page.getByRole('button', { name: 'Criar ambiente', exact: true }).click();
  expect((await created).status()).toBe(201);
  await expect(page.getByRole('heading', { name: 'Crie sua organização' })).toBeHidden();
  const session = await page.evaluate(() => ({ token: localStorage.getItem('prisma_workspace_token')!, org: localStorage.getItem('prisma_workspace_organization')! }));
  expect(session.org).toEqual(expect.any(String));
  const members = await request.get(`${apiUrl}/api/organizations/current/members`, { headers: { Authorization: `Bearer ${session.token}`, 'X-Organization-Id': session.org } });
  expect(members.status()).toBe(200);
  expect(await members.json()).toEqual(expect.arrayContaining([expect.objectContaining({ displayName: 'Ana Alves Oficina' })]));
  await capture(page, info, 'C5-nome-no-vinculo');
});

test('D99 linha do tempo separa movimentos, mantém revisitas e oferece grafo complementar', async ({ page, request, authenticatedGoto }, info) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const board = await f.board(); const initial = await f.stage(board.id); const second = await f.stage(board.id);
    const item = await f.item(board.id, initial.id);
    for (const destinationStageId of [second.id, initial.id]) {
      const moved = await f.api('/api/WorkItems/move', 'POST', { workItemId: item.id, destinationStageId, position: 100 });
      expect(moved.status()).toBe(204);
    }
    const timeline = await f.get<{ items: Array<{ kind: string; fromColumnName: string | null; toColumnName: string; actorName: string | null }> }>(`/api/WorkItems/${item.id}/timeline`);
    expect(timeline.items).toHaveLength(3);
    expect(timeline.items.map(entry => entry.kind)).toEqual(['column_moved', 'column_moved', 'created_in_column']);
    expect(timeline.items.map(entry => entry.toColumnName)).toEqual([initial.name, second.name, initial.name]);
    expect(timeline.items[2].actorName).toEqual(expect.any(String));
    await page.goto(`/boards/${board.id}`);
    await expect(page.getByRole('button', { name: 'Lead Time', exact: true })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Automações', exact: true })).toHaveCount(0);
    await capture(page, info, 'C5-funcoes-ocultas');
    await page.goto(`/projects/${f.projectId}/backlog?item=${item.id}`);
    const drawer = page.locator(`[role="dialog"][aria-describedby="task-description-${item.id}"]`);
    await drawer.getByRole('button', { name: 'Linha do tempo', exact: true }).click();
    const list = drawer.getByRole('list', { name: 'Movimentações entre colunas', exact: true });
    await expect(list.getByRole('listitem')).toHaveCount(3);
    await expect(list.getByRole('listitem').nth(0)).toContainText(`moveu de ${second.name} para ${initial.name}`);
    await expect(list.getByRole('listitem').nth(2)).toContainText(`Tarefa criada na coluna ${initial.name}`);
    await capture(page, info, 'C5-linha-tempo');
    await drawer.getByRole('button', { name: 'Ver grafo de estados', exact: true }).click();
    await expect(drawer.getByRole('heading', { name: 'Grafo de estados', exact: true })).toBeVisible();
    await expect(drawer.getByLabel('Grafo de estados da tarefa', { exact: true })).toBeVisible();
    await expect(drawer.getByText('Carregando caminho da tarefa...', { exact: true })).toHaveCount(0);
    await expect(drawer.locator('[title*="Permanência"]')).toHaveCount(0);
    await capture(page, info, 'C5-grafo-complementar');
    await drawer.getByRole('button', { name: 'Histórico', exact: true }).click();
    await expect(drawer.getByText(/moveu de/)).toHaveCount(0);
    // A separação é de apresentação: os eventos de auditoria continuam persistidos.
    const events = await f.get<Array<{ kind: string }>>(`/api/WorkItems/${item.id}/events`);
    expect(events.filter(event => event.kind === 'stage_changed')).toHaveLength(2);
  }, 'Oficina D99');
});
