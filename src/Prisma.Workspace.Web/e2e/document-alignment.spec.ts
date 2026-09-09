import type { Page } from '@playwright/test';
import { test, expect } from './fixtures/test';

const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5400';

/** Resolve o projeto seed (INFRACOES) em vez de depender de GUID fixo do banco. */
async function resolveSeedProjectId(page: Page): Promise<string> {
  if (!/127\.0\.0\.1|localhost/.test(page.url())) {
    await page.goto('/projects');
    await page.waitForLoadState('domcontentloaded');
  }
  type Project = { id: string; key: string; name: string };
  const projects = await appApi<Project[]>(page, '/api/projects');
  const seeded = projects.find((project) => project.key === 'INFRACOES')
    ?? projects.find((project) => /infrações|infracoes/i.test(project.name))
    ?? projects[0];
  if (!seeded) throw new Error('Nenhum projeto seed disponível para os testes E2E.');
  return seeded.id;
}

async function appApi<T>(page: Page, path: string, init?: { method?: string; body?: unknown }): Promise<T> {
  return page.evaluate(async ({ apiUrl: baseUrl, path: requestPath, init: requestInit }) => {
    const token = localStorage.getItem('prisma_workspace_token');
    const organizationId = localStorage.getItem('prisma_workspace_organization');
    const response = await fetch(`${baseUrl}${requestPath}`, {
      method: requestInit?.method ?? 'GET',
      headers: {
        Authorization: `Bearer ${token}`,
        'X-Organization-Id': organizationId ?? '',
        'Content-Type': 'application/json',
      },
      body: requestInit?.body === undefined ? undefined : JSON.stringify(requestInit.body),
    });
    if (!response.ok) throw new Error(`${response.status} ${await response.text()}`);
    return response.status === 204 ? undefined : response.json();
  }, { apiUrl, path, init });
}

async function waitForBacklog(page: Page) {
  const panel = page.getByTestId('backlog-drop-panel');
  await expect(panel).toBeVisible({ timeout: 15_000 });
  await expect(page.getByText('Carregando backlog...')).toHaveCount(0, { timeout: 15_000 });
  return panel;
}

async function createProjectThroughUi(page: Page) {
  await page.goto('/projects');
  await page.waitForLoadState('domcontentloaded');
  await page.getByRole('button', { name: 'Novo projeto' }).click();
  await page.getByPlaceholder('Nome do projeto').fill(`Projeto E2E ${Date.now()}`);
  await expect(page.getByRole('combobox', { name: 'Estrutura de Trabalho' })).toHaveCount(0);
  await page.getByRole('combobox', { name: 'Natureza', exact: true }).selectOption('2');
  await page.getByRole('combobox', { name: 'Tipo de Trabalho', exact: true }).selectOption('6');
  await page.getByRole('button', { name: 'Criar projeto' }).click();
  await expect(page).toHaveURL(/\/projects\/[^/]+\/backlog$/);
  return page.url().match(/\/projects\/([^/]+)\/backlog$/)![1];
}

test('product backlog representa pai, filhos e subtarefa órfã sem criar hierarquia falsa', async ({ page, authenticatedGoto }) => {
  const projectId = await resolveSeedProjectId(page);
  await authenticatedGoto(`/projects/${projectId}/backlog`);

  const panel = await waitForBacklog(page);
  const child = panel.locator('[data-testid="backlog-item"][data-hierarchy-state="child"]').first();
  await expect(child).toBeVisible({ timeout: 15_000 });
  await expect(child).toHaveAttribute('aria-level', '2');
  await expect(child).toHaveAttribute('data-hierarchy-depth', '1');

  const parentId = await child.getAttribute('data-parent-id');
  expect(parentId).toBeTruthy();
  const parent = panel.locator(`[data-testid="backlog-item"][data-work-item-id="${parentId}"]`);
  await expect(parent).toBeVisible();
  await expect(parent).toHaveAttribute('aria-level', '1');

  const parentBeforeChild = await parent.evaluate((parentNode, childId) => {
    const childNode = document.querySelector(`[data-work-item-id="${childId}"]`);
    return Boolean(childNode && (parentNode.compareDocumentPosition(childNode) & Node.DOCUMENT_POSITION_FOLLOWING));
  }, await child.getAttribute('data-work-item-id'));
  expect(parentBeforeChild).toBeTruthy();

  await parent.getByTestId('backlog-toggle-children').click();
  await expect(child).toBeHidden();
  await parent.getByTestId('backlog-toggle-children').click();
  await expect(child).toBeVisible();

  const orphan = panel.locator('[data-testid="backlog-item"][data-hierarchy-state="orphan"]');
  if (await orphan.count()) {
    await expect(orphan.first().getByText('sem pai', { exact: true })).toBeVisible();
  }
});

test('participantes e histórico usam nome funcional e nunca e-mail como rótulo', async ({ page, authenticatedGoto }) => {
  const projectId = await resolveSeedProjectId(page);
  await authenticatedGoto(`/projects/${projectId}/backlog`);
  await waitForBacklog(page);
  const item = page.getByTestId('backlog-item').first();
  await expect(item).toBeVisible({ timeout: 15_000 });
  const workItemId = await item.getAttribute('data-work-item-id');
  expect(workItemId).toBeTruthy();

  type Member = { userId: string; role: number; isActive: boolean; email?: string };
  type Detail = { responsibleId?: string; participants: Array<{ userId: string }> };
  const members = await appApi<Member[]>(page, '/api/organizations/current/members');
  const details = await appApi<Detail>(page, `/api/WorkItems/${workItemId}`);
  const actorId = await page.evaluate(() => {
    const token = localStorage.getItem('prisma_workspace_token');
    if (!token) return '';
    const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
    return payload.sub ?? payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] ?? '';
  });
  const actor = members.find(member => member.userId === actorId)!;
  const target = members.find(member => member.userId !== actorId && member.userId !== details.responsibleId)!;
  expect(actor).toBeTruthy();
  expect(target).toBeTruthy();

  await appApi(page, `/api/organizations/current/members/${actor.userId}`, {
    method: 'PUT', body: { role: actor.role, isActive: true, displayName: 'PO E2E' },
  });
  await appApi(page, `/api/organizations/current/members/${target.userId}`, {
    method: 'PUT', body: { role: target.role, isActive: true, displayName: 'Gabriel Tavares E2E' },
  });
  if (details.participants.some(participant => participant.userId === target.userId)) {
    await appApi(page, `/api/WorkItems/${workItemId}/assignees/${target.userId}`, { method: 'DELETE' });
  }
  await appApi(page, `/api/WorkItems/${workItemId}/assignees`, {
    method: 'POST', body: { userId: target.userId },
  });

  await page.reload();
  await page.waitForLoadState('domcontentloaded');
  await page.locator(`[data-work-item-id="${workItemId}"]`).getByRole('button', { name: /^Abrir detalhes de / }).click();
  const dialog = page.getByRole('dialog');
  const participants = dialog.getByRole('heading', { name: 'Participantes' }).locator('..');
  await expect(participants).toContainText('Gabriel Tavares E2E');
  if (target.email) await expect(participants.getByText(target.email, { exact: true })).toHaveCount(0);

  await dialog.getByRole('button', { name: 'Histórico' }).click();
  await expect(dialog.getByText(/PO E2E alocou Gabriel Tavares E2E/).first()).toBeVisible();
  await expect(dialog.getByText(/@/)).toHaveCount(0);
});

test('dependência é encontrada por título, persistida e removida pela gaveta', async ({ page, authenticatedGoto }) => {
  const projectId = await resolveSeedProjectId(page);
  await authenticatedGoto(`/projects/${projectId}/backlog`);
  await waitForBacklog(page);
  const source = page.getByTestId('backlog-item').first();
  await expect(source).toBeVisible({ timeout: 15_000 });
  const workItemId = await source.getAttribute('data-work-item-id');
  expect(workItemId).toBeTruthy();

  const targetButton = page.getByTestId('backlog-item').nth(1)
    .getByRole('button', { name: /^Abrir detalhes de / });
  await expect(targetButton).toBeVisible();
  const targetLabel = await targetButton.getAttribute('aria-label');
  const targetTitle = targetLabel?.replace(/^Abrir detalhes de /, '').trim();
  expect(targetTitle).toBeTruthy();

  const existing = await appApi<{ links: Array<{ id: string; relatedTitle: string }> }>(page, `/api/WorkItems/${workItemId}`);
  for (const relation of existing.links.filter(link => link.relatedTitle === targetTitle)) {
    await appApi(page, `/api/WorkItems/${workItemId}/links/${relation.id}`, { method: 'DELETE' });
  }

  await source.getByRole('button', { name: /^Abrir detalhes de / }).click();
  let dialog = page.getByRole('dialog');
  const search = dialog.getByRole('combobox', { name: 'Buscar tarefa relacionada' });
  await search.fill(targetTitle!.slice(0, Math.min(targetTitle!.length, 24)));
  const result = dialog.getByRole('listbox', { name: 'Tarefas encontradas' })
    .getByRole('button', { name: targetTitle!, exact: false });
  await expect(result).toBeVisible();
  await result.click();
  await dialog.getByRole('button', { name: 'Vincular' }).click();
  const linkedText = new RegExp(`Depende de.*${targetTitle}`);
  await expect(dialog.getByText(linkedText)).toBeVisible();

  await page.reload();
  await page.waitForLoadState('domcontentloaded');
  dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  const relation = dialog.getByText(linkedText).locator('..').locator('..');
  await expect(relation).toBeVisible();
  await relation.getByRole('button', { name: 'Remover relacionamento' }).click();
  await expect(dialog.getByText(linkedText)).toHaveCount(0);
});

test('comentário interno não se mistura ao histórico automático', async ({ page, authenticatedGoto }) => {
  const projectId = await resolveSeedProjectId(page);
  await authenticatedGoto(`/projects/${projectId}/backlog`);
  await waitForBacklog(page);
  const firstItem = page.getByTestId('backlog-item').first();
  await expect(firstItem).toBeVisible({ timeout: 15_000 });
  await firstItem.getByRole('button', { name: /^Abrir detalhes de / }).click();
  const dialog = page.getByRole('dialog');
  const message = `Comentário interno E2E ${Date.now()}`;

  await dialog.getByRole('button', { name: 'Comentários' }).click();
  await dialog.getByPlaceholder(/Escreva um comentario/).fill(message);
  await dialog.getByRole('button', { name: 'Enviar' }).click();
  await expect(dialog.getByText(message, { exact: true })).toBeVisible();
  await expect(dialog.getByText(/atualizou os dados da tarefa/)).toHaveCount(0);

  await dialog.getByRole('button', { name: 'Descrição' }).click();
  const priority = dialog.getByRole('combobox', { name: 'Prioridade' });
  const original = await priority.inputValue();
  const changed = original === '0' ? '1' : '0';
  await priority.selectOption(changed);
  await expect(dialog.getByText('Salvo')).toBeVisible();

  await dialog.getByRole('button', { name: 'Histórico' }).click();
  await expect(dialog.getByText(/atualizou os dados da tarefa/).first()).toBeVisible();
  await expect(dialog.getByText(message, { exact: true })).toHaveCount(0);

  await dialog.getByRole('button', { name: 'Descrição' }).click();
  await priority.selectOption(original);
  await expect(dialog.getByText('Salvo')).toBeVisible();
});

test('criar projeto usa o padrão interno sem criar sprint implicitamente', async ({ page, authenticatedGoto }) => {
  await authenticatedGoto('/projects');
  const createdProjectId = await createProjectThroughUi(page);
  try {
    const project = await appApi<{ methodology: number; nature: number; workType: number }>(page, `/api/projects/${createdProjectId}`);
    const sprints = await appApi<unknown[]>(page, `/api/projects/${createdProjectId}/sprints`);
    expect(project.methodology).toBe(1);
    expect(project.nature).toBe(2);
    expect(project.workType).toBe(6);
    expect(sprints).toEqual([]);

    await page.getByRole('link', { name: 'Sprints' }).click();
    await expect(page.getByText('Nenhuma sprint planejada para este projeto.')).toBeVisible();
  } finally {
    await appApi(page, `/api/projects/${createdProjectId}/archive`, { method: 'POST' });
  }
});

test('kanban abre com cartões mais recentes no topo e restaura essa ordenação', async ({ page, authenticatedGoto }) => {
  test.setTimeout(60_000);
  await authenticatedGoto('/projects');
  const createdProjectId = await createProjectThroughUi(page);
  try {
    const project = await appApi<{ boards: Array<{ id: string }> }>(page, `/api/projects/${createdProjectId}`);
    await page.goto(`/boards/${project.boards[0].id}`);
    await page.waitForLoadState('domcontentloaded');

    const sort = page.getByRole('combobox', { name: 'Ordenar cartões' });
    await expect(sort).toBeVisible({ timeout: 15_000 });
    await expect(sort).toHaveValue('created');
    await sort.selectOption('title');
    await expect(sort).toHaveValue('title');
    await sort.selectOption('created');
    await expect(sort).toHaveValue('created');
  } finally {
    if (!page.isClosed()) {
      await appApi(page, `/api/projects/${createdProjectId}/archive`, { method: 'POST' });
    }
  }
});

test('workflow alterna entre personalizado e herdado da organização', async ({ page, authenticatedGoto }) => {
  const organizationId = '11111111-1111-4111-8111-111111111111';
  const projectId = await resolveSeedProjectId(page);
  await authenticatedGoto(`/projects/${projectId}/settings`);
  type Template = { id: string; name: string; isActive: boolean };
  let templates = await appApi<Template[]>(page, `/api/organizations/${organizationId}/workflow-templates`);
  let createdTemplateId = '';
  if (!templates.some(template => template.isActive)) {
    const created = await appApi<Template>(page, `/api/organizations/${organizationId}/workflow-templates`, {
      method: 'POST',
      body: {
        name: 'Fluxo E2E', isDefault: false,
        statuses: [
          { key: 'todo', name: 'A fazer', color: '#64748B', position: 1000, category: 1, isInitial: true, isFinal: false },
          { key: 'doing', name: 'Em andamento', color: '#1671B9', position: 2000, category: 2, isInitial: false, isFinal: false },
          { key: 'done', name: 'Concluído', color: '#16834F', position: 3000, category: 4, isInitial: false, isFinal: true },
        ],
        transitions: [{ sourceKey: 'todo', targetKey: 'doing' }, { sourceKey: 'doing', targetKey: 'done' }],
      },
    });
    createdTemplateId = created.id;
    templates = [created];
    await page.reload();
    await page.waitForLoadState('domcontentloaded');
  }

  const activeTemplate = templates.find(template => template.isActive)!;
  const modePanel = page.getByRole('heading', { name: 'Status e fluxo' }).locator('..').locator('..');
  if (await modePanel.getByText('Herdado da organização', { exact: true }).count()) {
    await modePanel.getByRole('button', { name: 'Personalizar fluxo' }).click();
    await expect(modePanel.getByText('Personalizado no projeto', { exact: true })).toBeVisible();
  }
  await modePanel.getByRole('combobox', { name: 'Template de workflow' }).selectOption(activeTemplate.id);
  await modePanel.getByRole('button', { name: 'Herdar template' }).click();
  await expect(modePanel.getByText('Herdado da organização', { exact: true })).toBeVisible();
  await expect(modePanel.getByText(/sincronizado/)).toBeVisible();

  await modePanel.getByRole('button', { name: 'Personalizar fluxo' }).click();
  await expect(modePanel.getByText('Personalizado no projeto', { exact: true })).toBeVisible();
  if (createdTemplateId) {
    await appApi(page, `/api/organizations/${organizationId}/workflow-templates/${createdTemplateId}`, { method: 'DELETE' });
  }
});

const isoHojeMais = (dias: number) => {
  const data = new Date();
  data.setUTCDate(data.getUTCDate() + dias);
  return data.toISOString().slice(0, 10);
};

test('quadro da sprint move item por drag-and-drop e persiste a etapa', async ({ page, authenticatedGoto }, testInfo) => {
  await authenticatedGoto('/projects');
  const scrumProjectId = await createProjectThroughUi(page);
  try {
    type Project = { boards: Array<{ id: string }> };
    type Stage = { id: string; name: string };
    const project = await appApi<Project>(page, `/api/projects/${scrumProjectId}`);
    const boardId = project.boards[0].id;
    const stages = await appApi<Stage[]>(page, `/api/Stages/board/${boardId}`);
    expect(stages.length).toBeGreaterThanOrEqual(2);
    const sprintId = await appApi<string>(page, `/api/projects/${scrumProjectId}/sprints`, {
      method: 'POST',
      // Datas relativas: com o estado da sprint derivado das datas (D84), um período fixo
      // no passado passa a estar encerrado, e o quadro de sprint encerrada é somente
      // leitura — o arraste ficaria desativado e o teste falharia por envelhecimento.
      body: {
        teamId: null, name: 'Sprint E2E', goal: 'Validar DnD',
        startDate: isoHojeMais(-1), endDate: isoHojeMais(13),
      },
    });
    const workItemId = await appApi<string>(page, '/api/WorkItems', {
      method: 'POST',
      body: {
        boardId, stageId: stages[0].id, parentId: null, title: 'Mover no quadro E2E', subtitle: null,
        description: null, priority: 1, estimatedHours: 4, dueDate: null, position: 1000,
        kind: 5, sprintId, remainingHours: 4, teamId: null, responsibleId: null,
        participantIds: [], origin: 1, requesterId: null, requesterName: null, requesterEmail: null,
        startDate: null, acceptanceCriteria: null,
      },
    });

    await page.goto(`/projects/${scrumProjectId}/sprints`);
    await page.waitForLoadState('domcontentloaded');
    await page.getByRole('tab', { name: 'Quadro' }).click();
    const card = page.locator(`[data-testid="sprint-card"][data-work-item-id="${workItemId}"]`);
    const destination = page.locator(`[data-testid="sprint-stage"][data-stage-id="${stages[1].id}"]`);
    await expect(card).toBeVisible();
    await expect(destination).toBeVisible();
    if (testInfo.project.name === 'chromium-mobile') {
      await page.getByRole('combobox', { name: `Mover Mover no quadro E2E para etapa` })
        .selectOption(stages[1].id);
    } else {
      await card.dragTo(destination);
    }
    await expect.poll(async () => (await appApi<{ stageId: string }>(page, `/api/WorkItems/${workItemId}`)).stageId)
      .toBe(stages[1].id);
  } finally {
    await appApi(page, `/api/projects/${scrumProjectId}/archive`, { method: 'POST' });
  }
});
