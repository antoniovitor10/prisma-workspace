import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import type { Page, TestInfo } from '@playwright/test';
import { test, expect } from './fixtures/test';
import { withBoardFactory, type BoardFactory } from './fixtures/boards';

async function qaCapture(page: Page, info: TestInfo, label: string) {
  const directory = resolve('../../.runlogs/oficina/screenshots');
  mkdirSync(directory, { recursive: true });
  await page.screenshot({ path: `${directory}/C1-${label}-${info.project.name}-after.png`, scale: 'css' });
}
async function qaViewport(page: Page, info: TestInfo) {
  await page.setViewportSize(info.project.name.includes('mobile') ? { width: 390, height: 844 } : { width: 1440, height: 900 });
}

type Task = { id: string; title: string; description: string | null; kind: number; parentId: string | null;
  boardId: string; stageId: string; responsibleId: string | null; priority: number; startDate: string | null;
  dueDate: string | null; estimatedHours: number | null; isArchived: boolean; version: string;
  links: Array<{ relatedWorkItemId: string }>;
  customFields: Array<{ fieldId: string; value: string | null }> };
type Checklist = { id: string; text: string; done: boolean };
const dialog = (page: Page) => page.locator('[role="dialog"][aria-describedby^="task-description-"]');
const responseTo = (page: Page, method: string, path: string) => page.waitForResponse(response =>
  response.request().method() === method && new URL(response.url()).pathname.toLowerCase() === path.toLowerCase());
const task = (f: BoardFactory, id: string) => f.get<Task>(`/api/WorkItems/${id}`);
async function openTask(page: Page, projectId: string, id: string) {
  const loaded = responseTo(page, 'GET', `/api/WorkItems/${id}`);
  await page.goto(`/projects/${projectId}/backlog?item=${id}`, { waitUntil: 'domcontentloaded' });
  expect((await loaded).status()).toBe(200);
  await expect(dialog(page)).toHaveAttribute('aria-describedby', `task-description-${id}`);
  await expect(dialog(page).getByLabel('Título', { exact: true })).toBeVisible();
}
async function closedTask(page: Page) {
  await expect(dialog(page)).toBeHidden();
  // O backlog pode esconder o modal antes de a mutation terminar de remover o deep link.
  await expect.poll(() => new URL(page.url()).searchParams.has('item')).toBe(false);
}
async function saveTask(page: Page, id: string, action: () => Promise<unknown>) {
  const saved = responseTo(page, 'PUT', `/api/WorkItems/${id}`);
  // Aguarda também a hidratação posterior: a mutation invalida o detalhe ao salvar.
  const refreshed = responseTo(page, 'GET', `/api/WorkItems/${id}`);
  await action();
  expect((await saved).status()).toBe(204);
  const details = await refreshed;
  expect(details.status()).toBe(200);
  await details.finished();
}
const payload = (value: Task) => ({ title: value.title, description: value.description, kind: value.kind,
  stageId: value.stageId, responsibleId: value.responsibleId, priority: value.priority });

test('L07 criar tarefa pela UI persiste ID, quadro, coluna e campos informados', async ({ page, request, authenticatedGoto }) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const board = await f.board(); const column = await f.stage(board.id);
    const boardsLoaded = responseTo(page, 'GET', '/api/Boards');
    const columnsLoaded = responseTo(page, 'GET', `/api/Stages/board/${board.id}`);
    const itemsLoaded = responseTo(page, 'GET', `/api/WorkItems/board/${board.id}`);
    await authenticatedGoto(`/boards/${board.id}`);
    const boardsResponse = await boardsLoaded; expect(boardsResponse.status()).toBe(200);
    expect(await boardsResponse.json()).toEqual(expect.arrayContaining([expect.objectContaining({ id: board.id })]));
    const columnsResponse = await columnsLoaded; expect(columnsResponse.status()).toBe(200);
    expect(await columnsResponse.json()).toEqual(expect.arrayContaining([expect.objectContaining({ id: column.id })]));
    expect((await itemsLoaded).status()).toBe(200);
    await expect(page.getByRole('button', { name: `Editar coluna ${column.name}`, exact: true })).toBeVisible();
    await page.locator(`[data-stage-id="${column.id}"]`).getByRole('button', { name: 'Novo Card', exact: true }).click();
    const title = f.name('criação UI'); const description = f.name('descrição inicial');
    await page.getByPlaceholder('Título do Card', { exact: true }).fill(title);
    await page.getByPlaceholder('Descrição em Markdown (Opcional)', { exact: true }).fill(description);
    await page.getByPlaceholder('Horas', { exact: true }).fill('2.5');
    await page.getByRole('combobox').filter({ has: page.getByRole('option', { name: 'Alta', exact: true }) }).selectOption('2');
    await page.getByLabel('Responsável principal da nova tarefa', { exact: true }).selectOption(f.actorId);
    const created = responseTo(page, 'POST', '/api/WorkItems');
    await page.getByRole('button', { name: 'Criar Card', exact: true }).click();
    const response = await created; expect(response.status()).toBe(201);
    const id: string = await response.json();
    const boardsReloaded = responseTo(page, 'GET', '/api/Boards');
    const columnsReloaded = responseTo(page, 'GET', `/api/Stages/board/${board.id}`);
    const itemsReloaded = responseTo(page, 'GET', `/api/WorkItems/board/${board.id}`);
    await page.reload({ waitUntil: 'domcontentloaded' });
    expect((await boardsReloaded).status()).toBe(200);
    expect((await columnsReloaded).status()).toBe(200);
    const persistedItems = await itemsReloaded; expect(persistedItems.status()).toBe(200);
    expect(await persistedItems.json()).toEqual(expect.arrayContaining([expect.objectContaining({ id, title })]));
    await expect(page.getByRole('heading', { name: new RegExp(title) })).toBeVisible();
    expect(await task(f, id)).toMatchObject({ id, title, description, boardId: board.id, stageId: column.id,
      priority: 2, estimatedHours: 2.5, responsibleId: f.actorId, isArchived: false });
    await openTask(page, f.projectId, id);
    await expect(dialog(page).getByLabel('Título', { exact: true })).toHaveValue(title);
  }, 'L07');
});

test('L07 editar título, descrição rica, prioridade, datas e responsável persiste após reload', async ({ page, request, authenticatedGoto }) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const board = await f.board(); const column = await f.stage(board.id); const original = await f.item(board.id, column.id);
    const person = await f.member();
    await openTask(page, f.projectId, original.id);
    const title = f.name('editada'); const description = f.name('descrição editada');
    await saveTask(page, original.id, async () => {
      await dialog(page).getByLabel('Título', { exact: true }).fill(title);
      await dialog(page).getByLabel('Título', { exact: true }).press('Tab');
    });
    const editor = dialog(page).getByRole('region', { name: 'Editor da descrição', exact: true });
    await saveTask(page, original.id, async () => {
      await editor.getByRole('textbox', { name: 'Descrição da tarefa', exact: true }).fill(description);
      await editor.getByRole('textbox', { name: 'Descrição da tarefa', exact: true }).press('Tab');
    });
    await expect(editor.getByRole('status')).toHaveText('Salvo');
    await saveTask(page, original.id, () => dialog(page).getByRole('combobox', { name: 'Prioridade', exact: true }).selectOption('3'));
    await saveTask(page, original.id, async () => {
      await dialog(page).getByLabel('Data de início', { exact: true }).fill('2026-11-01');
      await dialog(page).getByRole('heading', { name: 'Planejamento', exact: true }).click();
    });
    await saveTask(page, original.id, async () => {
      await dialog(page).getByLabel('Prazo', { exact: true }).fill('2026-11-15');
      await dialog(page).getByRole('heading', { name: 'Planejamento', exact: true }).click();
    });
    await saveTask(page, original.id, () => dialog(page).getByRole('combobox', { name: 'Responsável principal', exact: true }).selectOption(person.id));
    await page.reload({ waitUntil: 'domcontentloaded' });
    await expect(dialog(page).getByLabel('Título', { exact: true })).toHaveValue(title);
    await expect(dialog(page).getByRole('textbox', { name: 'Descrição da tarefa', exact: true })).toContainText(description);
    await expect(dialog(page).getByRole('combobox', { name: 'Prioridade', exact: true })).toHaveValue('3');
    await expect(dialog(page).getByLabel('Data de início', { exact: true })).toHaveValue('2026-11-01');
    await expect(dialog(page).getByLabel('Prazo', { exact: true })).toHaveValue('2026-11-15');
    await expect(dialog(page).getByRole('combobox', { name: 'Responsável principal', exact: true })).toHaveValue(person.id);
    const persisted = await task(f, original.id);
    expect(persisted).toMatchObject({ title, priority: 3, startDate: '2026-11-01', dueDate: '2026-11-15', responsibleId: person.id });
    expect(persisted.description).toContain(description);
  }, 'L07');
});

test('L07 duplicar, arquivar e reativar tarefa preserva conteúdo e muda listas de forma persistente', async ({ page, request, authenticatedGoto }) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const board = await f.board(); const column = await f.stage(board.id);
    const original = await f.item(board.id, column.id, { description: 'Conteúdo da cópia', priority: 2, startDate: '2026-11-01', dueDate: '2026-11-15' });
    await openTask(page, f.projectId, original.id);
    const duplicated = responseTo(page, 'POST', `/api/WorkItems/${original.id}/duplicate`);
    await dialog(page).getByRole('button', { name: 'Duplicar', exact: true }).click();
    const copied = await duplicated; expect(copied.status()).toBe(201); const id: string = await copied.json();
    expect(id).not.toBe(original.id);
    const title = `${original.title} (copia)`;
    // Duplicar preserva o detalhe de origem aberto; fecha pela UI antes de conferir a lista.
    await dialog(page).getByRole('button', { name: 'Fechar modal', exact: true }).click();
    await closedTask(page);
    await page.reload({ waitUntil: 'domcontentloaded' });
    await expect(page.getByRole('button', { name: `Abrir detalhes de ${title}`, exact: true })).toBeVisible();
    expect(await task(f, id)).toMatchObject({ title, description: 'Conteúdo da cópia', priority: 2,
      responsibleId: f.actorId, stageId: column.id, startDate: '2026-11-01', dueDate: '2026-11-15', isArchived: false });
    const openedCopy = responseTo(page, 'GET', `/api/WorkItems/${id}`);
    await page.getByRole('button', { name: `Abrir detalhes de ${title}`, exact: true }).click();
    expect((await openedCopy).status()).toBe(200);
    await expect(dialog(page)).toHaveAttribute('aria-describedby', `task-description-${id}`);
    const archived = responseTo(page, 'DELETE', `/api/WorkItems/${id}`);
    await dialog(page).getByRole('button', { name: 'Arquivar', exact: true }).click();
    expect((await archived).status()).toBe(204);
    await closedTask(page);
    await page.reload({ waitUntil: 'domcontentloaded' });
    await expect(page.getByRole('button', { name: `Abrir detalhes de ${title}`, exact: true })).toHaveCount(0);
    expect((await task(f, id)).isArchived).toBe(true);
    await page.getByRole('button', { name: 'Filtros', exact: true }).click();
    await page.getByLabel('Mostrar tarefas arquivadas').selectOption('yes');
    await page.getByRole('button', { name: `Abrir detalhes de ${title}`, exact: true }).click();
    await expect(dialog(page).getByRole('button', { name: 'Reativar', exact: true })).toBeVisible();
    const reactivated = responseTo(page, 'POST', `/api/WorkItems/${id}/reactivate`);
    await dialog(page).getByRole('button', { name: 'Reativar', exact: true }).click();
    expect((await reactivated).status()).toBe(204);
    await closedTask(page);
    await page.reload({ waitUntil: 'domcontentloaded' });
    await expect(page.getByRole('button', { name: `Abrir detalhes de ${title}`, exact: true })).toBeVisible();
    expect((await task(f, id)).isArchived).toBe(false);
    expect((await task(f, original.id)).isArchived).toBe(false);
  }, 'L07');
});

test('L07 checklist e campos próprios da tarefa adicionam, marcam e removem valores pela UI', async ({ page, request, authenticatedGoto }) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const board = await f.board(); const column = await f.stage(board.id); const original = await f.item(board.id, column.id);
    const field = await f.customField(); const flag = await f.customField(4);
    await openTask(page, f.projectId, original.id);
    await dialog(page).getByRole('button', { name: 'Subtarefas', exact: true }).click();
    const text = f.name('checklist');
    await dialog(page).getByPlaceholder('Novo item...', { exact: true }).fill(text);
    const added = responseTo(page, 'POST', `/api/WorkItems/${original.id}/checklist`);
    await dialog(page).getByRole('button', { name: '+ Add', exact: true }).click();
    const response = await added; expect(response.status()).toBe(200); const entry: Checklist = await response.json();
    const row = dialog(page).getByText(text, { exact: true }).locator('..');
    const marked = responseTo(page, 'PUT', `/api/WorkItems/${original.id}/checklist/${entry.id}`);
    await row.getByRole('checkbox').click();
    expect((await marked).status()).toBe(204);
    await expect(row.getByRole('checkbox')).toBeChecked();
    await page.reload({ waitUntil: 'domcontentloaded' });
    await dialog(page).getByRole('button', { name: 'Subtarefas', exact: true }).click();
    await expect(row.getByRole('checkbox')).toBeChecked();
    expect(await f.get(`/api/WorkItems/${original.id}/checklist`)).toEqual([expect.objectContaining({ id: entry.id, text, done: true })]);
    const removed = responseTo(page, 'DELETE', `/api/WorkItems/${original.id}/checklist/${entry.id}`);
    await row.getByRole('button').click();
    expect((await removed).status()).toBe(204);
    await expect(dialog(page).getByText(text, { exact: true })).toHaveCount(0);
    expect(await f.get(`/api/WorkItems/${original.id}/checklist`)).toEqual([]);
    await dialog(page).getByRole('button', { name: 'Descrição', exact: true }).click();
    await dialog(page).getByLabel(field.name, { exact: true }).fill('Valor próprio L07');
    await dialog(page).getByRole('combobox', { name: flag.name, exact: true }).selectOption('true');
    const stored = responseTo(page, 'PUT', `/api/WorkItems/${original.id}/custom-fields`);
    await dialog(page).getByRole('button', { name: 'Salvar campos', exact: true }).click();
    expect((await stored).status()).toBe(204);
    await page.reload({ waitUntil: 'domcontentloaded' });
    await expect(dialog(page).getByLabel(field.name, { exact: true })).toHaveValue('Valor próprio L07');
    await expect(dialog(page).getByRole('combobox', { name: flag.name, exact: true })).toHaveValue('true');
    expect((await task(f, original.id)).customFields).toEqual(expect.arrayContaining([
      expect.objectContaining({ fieldId: field.id, value: 'Valor próprio L07' }), expect.objectContaining({ fieldId: flag.id, value: 'true' }),
    ]));
    await dialog(page).getByLabel(field.name, { exact: true }).fill('');
    await dialog(page).getByRole('combobox', { name: flag.name, exact: true }).selectOption('');
    const cleared = responseTo(page, 'PUT', `/api/WorkItems/${original.id}/custom-fields`);
    await dialog(page).getByRole('button', { name: 'Salvar campos', exact: true }).click();
    expect((await cleared).status()).toBe(204);
    await page.reload({ waitUntil: 'domcontentloaded' });
    await expect(dialog(page).getByLabel(field.name, { exact: true })).toHaveValue('');
    await expect(dialog(page).getByRole('combobox', { name: flag.name, exact: true })).toHaveValue('');
    expect((await task(f, original.id)).customFields).toEqual(expect.arrayContaining([
      expect.objectContaining({ fieldId: field.id, value: null }), expect.objectContaining({ fieldId: flag.id, value: null }),
    ]));
  }, 'L07');
});

test('L07 subtarefa válida abre detalhe completo; tipos inválidos e filhos de subtarefa são recusados', async ({ page, request, authenticatedGoto }) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const board = await f.board(); const column = await f.stage(board.id); const parent = await f.item(board.id, column.id);
    await openTask(page, f.projectId, parent.id);
    await dialog(page).getByRole('button', { name: 'Subtarefas', exact: true }).click();
    const title = f.name('subtarefa UI');
    await dialog(page).getByPlaceholder('Nova subtarefa (somente título)', { exact: true }).fill(title);
    await dialog(page).getByLabel('Responsável da subtarefa', { exact: true }).selectOption(f.actorId);
    const created = responseTo(page, 'POST', '/api/WorkItems');
    await dialog(page).getByRole('button', { name: 'Adicionar', exact: true }).click();
    const response = await created; expect(response.status()).toBe(201); const id: string = await response.json();
    await expect(dialog(page)).toHaveAttribute('aria-describedby', `task-description-${id}`);
    await expect(dialog(page).getByRole('button', { name: 'Anexos', exact: true })).toBeVisible();
    await expect(dialog(page).getByRole('heading', { name: 'Apontamento de horas', exact: true })).toBeVisible();
    await page.reload({ waitUntil: 'domcontentloaded' });
    expect(await task(f, id)).toMatchObject({ id, title, parentId: parent.id, boardId: board.id, kind: 6 });
    const before = await f.get(`/api/WorkItems/${parent.id}/subitems`);
    for (const invalid of [{ parentId: id, kind: 6 }, { parentId: parent.id, kind: 1 }]) {
      const refused = await f.api('/api/WorkItems', 'POST', { boardId: board.id, stageId: column.id, title: f.name('hierarquia negada'),
        responsibleId: f.actorId, priority: 1, position: 100, ...invalid });
      expect(refused.status()).toBe(400);
      expect((await refused.json()).detail).toContain('hierarquia');
    }
    expect(await f.get(`/api/WorkItems/${parent.id}/subitems`)).toEqual(before);
    expect(await f.get(`/api/WorkItems/${id}/subitems`)).toEqual([]);
  }, 'L07');
});

test('L07 leitor não edita nem arquiva tarefa pela UI ou API', async ({ page, request, authenticatedGoto }) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const board = await f.board(); const column = await f.stage(board.id); const original = await f.item(board.id, column.id);
    const viewer = await f.viewer(); const before = await task(f, original.id);
    expect((await f.api(`/api/WorkItems/${original.id}`, 'PUT', { ...payload(before), title: f.name('edição negada') }, viewer)).status()).toBe(404);
    expect((await f.api(`/api/WorkItems/${original.id}`, 'DELETE', undefined, viewer)).status()).toBe(404);
    await page.evaluate(session => {
      localStorage.setItem('prisma_workspace_token', session.accessToken);
      localStorage.setItem('prisma_workspace_organization', session.organizationId);
    }, viewer);
    await openTask(page, f.projectId, original.id);
    const denied = responseTo(page, 'PUT', `/api/WorkItems/${original.id}`);
    await dialog(page).getByLabel('Título', { exact: true }).fill(f.name('UI negada'));
    await dialog(page).getByLabel('Título', { exact: true }).press('Tab');
    expect((await denied).status()).toBe(404);
    await expect(dialog(page)).toContainText('Falha ao salvar');
    const archived = responseTo(page, 'DELETE', `/api/WorkItems/${original.id}`);
    await dialog(page).getByRole('button', { name: 'Arquivar', exact: true }).click();
    expect((await archived).status()).toBe(404);
    await page.reload({ waitUntil: 'domcontentloaded' });
    await expect(dialog(page).getByLabel('Título', { exact: true })).toHaveValue(before.title);
    expect(await task(f, original.id)).toMatchObject({ title: before.title, isArchived: false, version: before.version });
  }, 'L07');
});

test('L07 D99: dependências oferecem autocomplete e vínculo persistido', async ({ page, request, authenticatedGoto }, info) => {
  await qaViewport(page, info);
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const board = await f.board(); const column = await f.stage(board.id);
    const original = await f.item(board.id, column.id);
    const target = await f.item(board.id, column.id);
    await openTask(page, f.projectId, original.id);
    await expect(dialog(page).getByRole('heading', { name: 'Dependências e bloqueios', exact: true })).toBeVisible();
    await dialog(page).getByRole('combobox', { name: 'Buscar tarefa relacionada' }).fill(target.title);
    await expect(dialog(page).getByRole('listbox', { name: 'Tarefas encontradas' })).toBeVisible();
    await dialog(page).getByRole('listbox', { name: 'Tarefas encontradas' }).scrollIntoViewIfNeeded();
    await qaCapture(page, info, 'autocomplete');
    await dialog(page).getByRole('listbox', { name: 'Tarefas encontradas' }).getByRole('button', { name: target.title, exact: false }).click();
    const linked = responseTo(page, 'POST', `/api/WorkItems/${original.id}/links`);
    await dialog(page).getByRole('button', { name: 'Vincular', exact: true }).click();
    expect((await linked).ok()).toBe(true);
    expect((await task(f, original.id)).links).toEqual(expect.arrayContaining([
      expect.objectContaining({ relatedWorkItemId: target.id }),
    ]));
  }, 'L07');
});

test('L07 contrato: criar subtarefa pela UI não deixa responsável principal ausente', async ({ page, request, authenticatedGoto }, info) => {
  await qaViewport(page, info);
  let responsibleId: string | null = null;
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const board = await f.board(); const column = await f.stage(board.id); const parent = await f.item(board.id, column.id);
    await openTask(page, f.projectId, parent.id);
    await dialog(page).getByRole('button', { name: 'Subtarefas', exact: true }).click();
    await dialog(page).getByPlaceholder('Nova subtarefa (somente título)', { exact: true }).fill(f.name('responsável obrigatório'));
    await dialog(page).getByLabel('Responsável da subtarefa', { exact: true }).selectOption(f.actorId);
    const box = await dialog(page).getByLabel('Responsável da subtarefa', { exact: true }).boundingBox();
    expect(box).not.toBeNull();
    expect(box!.x).toBeGreaterThanOrEqual(0);
    expect(box!.x + box!.width).toBeLessThanOrEqual(page.viewportSize()!.width);
    await dialog(page).getByLabel('Responsável da subtarefa', { exact: true }).scrollIntoViewIfNeeded();
    await qaCapture(page, info, 'responsavel');
    const created = responseTo(page, 'POST', '/api/WorkItems');
    await dialog(page).getByRole('button', { name: 'Adicionar', exact: true }).click();
    const response = await created; expect(response.status()).toBe(201); const id: string = await response.json();
    const child = await task(f, id);
    expect(child.parentId).toBe(parent.id);
    expect(child.responsibleId).toBe(f.actorId);
    responsibleId = child.responsibleId;
  }, 'L07');
  // Regressão BUG-L07-02: toda subtarefa precisa de responsável.
  expect(responsibleId).toEqual(expect.any(String));
});

test('L07 contrato: pai de outro projeto é recusado como erro de validação', async ({ page, request, authenticatedGoto }) => {
  let status = 0; let detail = '';
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const board = await f.board(); const column = await f.stage(board.id);
    const before = await f.get(`/api/WorkItems/project/${f.projectId}`);
    await withBoardFactory(page, request, async other => {
      const foreignBoard = await other.board(); const foreignColumn = await other.stage(foreignBoard.id);
      const foreignParent = await other.item(foreignBoard.id, foreignColumn.id);
      const refused = await f.api('/api/WorkItems', 'POST', { boardId: board.id, stageId: column.id, parentId: foreignParent.id,
        kind: 6, title: f.name('pai externo negado'), responsibleId: f.actorId, priority: 1, position: 100 });
      expect(await f.get(`/api/WorkItems/project/${f.projectId}`)).toEqual(before);
      expect(await other.get(`/api/WorkItems/${foreignParent.id}/subitems`)).toEqual([]);
      status = refused.status();
      detail = (await refused.json()).detail;
    }, 'L07');
  }, 'L07');
  // Regressão BUG-L07-03: argumento inválido usa Problem Details 400.
  expect(status).toBe(400);
  expect(detail).toContain('pai');
});
