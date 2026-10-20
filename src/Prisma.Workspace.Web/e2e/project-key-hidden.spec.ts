import { mkdirSync } from 'node:fs';
import { randomUUID } from 'node:crypto';
import { resolve } from 'node:path';
import { test, expect } from './fixtures/test';

const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5400';

test('D56 chave continua interna na API e não aparece na jornada operacional', async ({ page, request, authenticatedGoto }, info) => {
  await page.setViewportSize(info.project.name.includes('mobile') ? { width: 390, height: 844 } : { width: 1440, height: 900 });
  await authenticatedGoto('/projects');
  const session = await page.evaluate(() => ({ token: localStorage.getItem('prisma_workspace_token')!, organization: localStorage.getItem('prisma_workspace_organization')! }));
  const headers = { Authorization: `Bearer ${session.token}`, 'X-Organization-Id': session.organization };
  const api = (path: string, method = 'GET', data?: unknown) => request.fetch(apiUrl + path, { method, data, headers });
  const name = `Oficina linguagem ${randomUUID()}`;
  const created = await api('/api/projects', 'POST', { key: 'CLIENTE-IGNORADO', name, nature: 1, workType: 1 });
  expect(created.status()).toBe(201);
  const projectId: string = await created.json();
  try {
    const details = await api(`/api/projects/${projectId}`);
    expect(details.status()).toBe(200);
    const project = await details.json();
    expect(project.key).not.toBe('CLIENTE-IGNORADO');
    expect(project.key).toMatch(/^[A-Z][A-Z0-9-]*$/);
    const stages = await api(`/api/Stages/board/${project.boards[0].id}`);
    expect(stages.status()).toBe(200);
    const stage = (await stages.json())[0];
    const claims = JSON.parse(Buffer.from(session.token.split('.')[1], 'base64url').toString());
    const actorId = claims.sub ?? claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'];
    const title = `Tarefa de linguagem ${randomUUID()}`;
    const item = await api('/api/WorkItems', 'POST', { boardId: project.boards[0].id, stageId: stage.id, title, responsibleId: actorId, priority: 1, position: 100 });
    expect(item.status()).toBe(201);
    const itemId: string = await item.json();
    const itemResponse = await api(`/api/WorkItems/${itemId}`);
    expect(itemResponse.status()).toBe(200);
    const stored = await itemResponse.json();
    const directory = resolve('../../.runlogs/oficina/screenshots');
    mkdirSync(directory, { recursive: true });
    for (const [label, route] of [
      ['itens', `/projects/${projectId}/items`],
      ['backlog', `/projects/${projectId}/backlog`],
      ['relatorios', `/projects/${projectId}/reports`],
      ['meu-trabalho', '/me/tasks'],
      ['inicio', '/home'],
    ]) {
      await page.goto(route);
      await expect(page.locator('main')).toBeVisible();
      if (label === 'itens') await expect(page.getByRole('button', { name: `Abrir detalhes de ${title}` })).toBeVisible();
      if (label === 'backlog') await expect(page.getByRole('heading', { name, exact: true })).toBeVisible();
      if (label === 'meu-trabalho') await expect(page.getByRole('button', { name: `Abrir tarefa ${stored.number}: ${title}`, exact: true })).toBeVisible();
      if (label === 'inicio') await expect(page.getByText(name, { exact: true })).toBeVisible();
      if (label === 'relatorios') await expect(page.getByRole('button', { name: 'Relatórios prontos', exact: true })).toBeVisible();
      await expect.poll(async () => (await page.locator('body').innerText()).includes(project.key)).toBe(false);
      await page.screenshot({ path: `${directory}/D56-${label}-${info.project.name}.png`, scale: 'css' });
    }
    await page.goto(`/projects/${projectId}/backlog?item=${itemId}`);
    const drawer = page.locator(`[role="dialog"][aria-describedby="task-description-${itemId}"]`);
    await expect(drawer).toBeVisible();
    await expect(drawer.getByText(`#${stored.number}`, { exact: true })).toBeVisible();
    await expect(drawer).not.toContainText(`${project.key}-`);
    await page.screenshot({ path: `${directory}/D56-detalhe-${info.project.name}.png`, scale: 'css' });
    const after = await api(`/api/projects/${projectId}`);
    expect(after.status()).toBe(200);
    expect((await after.json()).key).toBe(project.key);
  } finally {
    expect((await api(`/api/projects/${projectId}/archive`, 'POST')).status()).toBe(204);
  }
});
