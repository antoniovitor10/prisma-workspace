import { randomUUID } from 'node:crypto';
import type { Page } from '@playwright/test';
import { test, expect } from './fixtures/test';

/**
 * T-0019: editar uma Tarefa filha de História pela gaveta não a transforma em Subtarefa (B-1),
 * e escolher "Tarefa" no seletor de uma Subtarefa sob História devolve o tipo Tarefa (B-5).
 * Dados próprios por execução (projeto novo), sem depender do seed.
 */
const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5400';

async function api(page: Page, path: string, method = 'GET', body?: unknown) {
  return page.evaluate(async ({ base, path, method, body }) => {
    const response = await fetch(`${base}${path}`, {
      method,
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${localStorage.getItem('prisma_workspace_token')}`,
        'X-Organization-Id': localStorage.getItem('prisma_workspace_organization') ?? '',
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    const text = await response.text();
    return { status: response.status, body: text ? JSON.parse(text) : null };
  }, { base: apiUrl, path, method, body });
}

async function storyWithTask(page: Page) {
  const created = await api(page, '/api/projects', 'POST', {
    name: `Tipo na edição E2E ${randomUUID()}`, description: 'Fixture da T-0019', nature: 1, workType: 1,
  });
  expect(created.status, JSON.stringify(created.body)).toBe(201);
  const projectId: string = created.body;
  const project = (await api(page, `/api/projects/${projectId}`)).body;
  const boardId: string = project.boards[0].id;
  const actorId = await page.evaluate(() => {
    const token = localStorage.getItem('prisma_workspace_token')!;
    const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
    return payload.sub ?? payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'];
  });
  const story = await api(page, '/api/WorkItems', 'POST', {
    projectId, boardId, title: `História ${randomUUID().slice(0, 8)}`, kind: 3, responsibleId: actorId, priority: 1, position: 100,
  });
  expect(story.status, JSON.stringify(story.body)).toBe(201);
  const task = await api(page, '/api/WorkItems', 'POST', {
    projectId, boardId, parentId: story.body, title: `Tarefa filha ${randomUUID().slice(0, 8)}`, kind: 5,
    responsibleId: actorId, priority: 1, position: 200,
  });
  expect(task.status, JSON.stringify(task.body)).toBe(201);
  const detail = (await api(page, `/api/WorkItems/${task.body}`)).body;
  expect(detail.kind).toBe(5);
  return { projectId, storyId: story.body as string, taskId: task.body as string };
}

test('B-1: editar o título de uma Tarefa filha de História mantém o tipo Tarefa', async ({ page, authenticatedGoto }) => {
  await authenticatedGoto('/projects');
  const { projectId, taskId } = await storyWithTask(page);

  await authenticatedGoto(`/projects/${projectId}/backlog?item=${taskId}`);
  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await expect(dialog.getByText('Tarefa', { exact: true })).toBeVisible();

  const title = dialog.getByRole('textbox', { name: 'Título', exact: true });
  const saved = page.waitForResponse(r => r.url().includes(`/api/WorkItems/${taskId}`) && r.request().method() === 'PUT');
  await title.fill('Tarefa filha editada pela gaveta');
  await title.blur();
  expect((await saved).ok()).toBeTruthy();

  await page.reload();
  const reopened = page.getByRole('dialog');
  await expect(reopened.getByRole('textbox', { name: 'Título', exact: true })).toHaveValue('Tarefa filha editada pela gaveta');
  await expect(reopened.getByText('Tarefa', { exact: true })).toBeVisible();
  await expect(reopened.getByText('Subtarefa', { exact: true })).toHaveCount(0);
  expect((await api(page, `/api/WorkItems/${taskId}`)).body.kind).toBe(5);
});

test('B-5: escolher Tarefa no seletor de uma Subtarefa sob História grava Tarefa', async ({ page, authenticatedGoto }) => {
  await authenticatedGoto('/projects');
  const { projectId, taskId } = await storyWithTask(page);

  // O par Subtarefa sob História só existe em dados antigos (a API não o cria mais). A primeira leitura
  // da gaveta é servida como o item afetado pelo bug; a gravação e a releitura vão ao servidor real.
  let served = false;
  await page.route(`**/api/WorkItems/${taskId}`, async route => {
    if (served || route.request().method() !== 'GET') return route.fallback();
    served = true;
    const response = await route.fetch();
    const body = await response.json();
    await route.fulfill({ response, json: { ...body, kind: 6 } });
  });

  await authenticatedGoto(`/projects/${projectId}/backlog?item=${taskId}`);
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByText('Subtarefa', { exact: true })).toBeVisible();
  const kind = dialog.getByRole('combobox', { name: 'Tipo', exact: true });
  await expect(kind).toHaveValue('6');

  const saved = page.waitForResponse(r => r.url().includes(`/api/WorkItems/${taskId}`) && r.request().method() === 'PUT');
  await kind.selectOption('5');
  const response = await saved;
  expect(response.ok()).toBeTruthy();
  expect(response.request().postDataJSON().kind).toBe(5);

  await page.unroute(`**/api/WorkItems/${taskId}`);
  await page.reload();
  const reopened = page.getByRole('dialog');
  await expect(reopened.getByRole('combobox', { name: 'Tipo', exact: true })).toHaveValue('5');
  await expect(reopened.getByText('Tarefa', { exact: true })).toBeVisible();
  expect((await api(page, `/api/WorkItems/${taskId}`)).body.kind).toBe(5);
});
