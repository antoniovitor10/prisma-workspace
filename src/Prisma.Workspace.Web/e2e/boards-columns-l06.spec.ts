import type { Locator, Page } from '@playwright/test';
import { test, expect } from './fixtures/test';
import { withBoardFactory, type BoardFactory, type BoardItem, type BoardStage } from './fixtures/boards';

const responseTo = (page: Page, method: string, path: string) => page.waitForResponse(response =>
  response.request().method() === method && new URL(response.url()).pathname.toLowerCase() === path.toLowerCase());
const editor = (page: Page) => page.getByRole('heading', { name: 'Editar Coluna', exact: true }).locator('..');
const deleteBoardModal = (page: Page) => page.getByRole('heading', { name: 'Excluir Quadro', exact: true }).locator('..');
const stages = (f: BoardFactory, boardId: string) => f.get<BoardStage[]>(`/api/Stages/board/${boardId}`);
const item = (f: BoardFactory, id: string) => f.get<BoardItem>(`/api/WorkItems/${id}`);
async function state(f: BoardFactory, ids: string[]) {
  return Promise.all(ids.map(async id => {
    const value = await item(f, id);
    return { id, boardId: value.boardId, stageId: value.stageId, completedAt: value.completedAt, version: value.version };
  }));
}

// O diálogo nativo deve ser resolvido durante o click, antes de este terminar.
async function confirmDeletion(page: Page, button: Locator, accept: boolean) {
  const handled = new Promise<string>(resolve => page.once('dialog', async dialog => {
    const message = dialog.message();
    if (accept) await dialog.accept(); else await dialog.dismiss();
    resolve(message);
  }));
  await button.click();
  return handled;
}

test('L06 cria quadro pela UI, copia somente colunas e persiste após recarga', async ({ page, request, authenticatedGoto }) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const source = await f.board();
    const column = await f.stage(source.id);
    await f.item(source.id, column.id);
    const original = await stages(f, source.id);
    await authenticatedGoto(`/boards/${source.id}`);
    await expect(page.getByLabel('Selecionar quadro')).toHaveValue(source.id);
    await page.getByRole('button', { name: 'Novo Quadro', exact: true }).click();
    const name = f.name('quadro UI');
    await page.getByLabel('Nome do quadro', { exact: true }).fill(name);
    await page.getByLabel('Estrutura de colunas').selectOption(source.id);
    const created = responseTo(page, 'POST', '/api/Boards');
    await page.getByRole('button', { name: 'Criar', exact: true }).click();
    const response = await created;
    expect(response.status()).toBe(201);
    const id: string = await response.json();
    await page.getByRole('button', { name: 'Ir para o quadro', exact: true }).click();
    await expect(page.getByLabel('Selecionar quadro')).toHaveValue(id);
    await page.reload();
    await expect(page.getByLabel('Selecionar quadro')).toHaveValue(id);
    await expect(page.getByRole('option', { name, exact: true })).toBeAttached();
    const persisted = await f.get<{ projectId: string; name: string }>(`/api/Boards/${id}`);
    expect(persisted).toMatchObject({ name, projectId: f.projectId });
    const copied = await stages(f, id);
    expect(copied.map(stage => stage.name)).toEqual(original.map(stage => stage.name));
    expect(copied.every(stage => !original.some(existing => existing.id === stage.id))).toBe(true);
    expect(await f.get(`/api/WorkItems/board/${id}`)).toEqual([]);
  });
});

test('L06 cancelar exclusão de quadro preserva itens; confirmar exige destino e transfere sem perda', async ({ page, request, authenticatedGoto }) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const source = await f.board(); const target = await f.board();
    const from = await f.stage(source.id); const to = await f.stage(target.id);
    const tasks = [await f.item(source.id, from.id), await f.item(source.id, from.id)];
    const before = await state(f, tasks.map(task => task.id));
    await authenticatedGoto(`/boards/${source.id}`);
    await page.getByRole('button', { name: 'Excluir Quadro', exact: true }).click();
    await deleteBoardModal(page).getByRole('button', { name: 'Cancelar', exact: true }).click();
    await page.reload();
    await expect(page.getByLabel('Selecionar quadro')).toHaveValue(source.id);
    expect(await state(f, tasks.map(task => task.id))).toEqual(before);

    await page.getByRole('button', { name: 'Excluir Quadro', exact: true }).click();
    const modal = deleteBoardModal(page);
    // O seletor de quadro de destino ainda não tem label associado; role + option delimitam a escolha.
    await modal.getByRole('combobox').filter({ has: page.getByRole('option', { name: 'Escolha o quadro de destino', exact: true }) }).selectOption('');
    const refused = responseTo(page, 'DELETE', `/api/Boards/${source.id}`);
    await modal.getByRole('button', { name: 'Excluir Quadro', exact: true }).click();
    expect((await refused).status()).toBe(400);
    await expect(modal).toContainText('Escolha quadro e coluna de destino');
    expect(await state(f, tasks.map(task => task.id))).toEqual(before);
    await modal.getByRole('combobox').filter({ has: page.getByRole('option', { name: 'Escolha o quadro de destino', exact: true }) }).selectOption(target.id);
    await expect(modal.getByRole('option', { name: to.name, exact: true })).toBeAttached();
    await modal.getByLabel('Coluna de destino', { exact: true }).selectOption(to.id);
    const removed = responseTo(page, 'DELETE', `/api/Boards/${source.id}`);
    await modal.getByRole('button', { name: 'Excluir Quadro', exact: true }).click();
    expect((await removed).status()).toBe(204);
    await expect(modal).toBeHidden();
    await authenticatedGoto(`/boards/${target.id}`);
    await page.reload();
    await expect(page.getByRole('option', { name: source.name, exact: true })).toHaveCount(0);
    expect((await f.api(`/api/Boards/${source.id}`)).status()).toBe(404);
    for (const task of tasks) {
      expect(await item(f, task.id)).toMatchObject({ boardId: target.id, stageId: to.id, completedAt: null });
      await expect(page.getByRole('heading', { name: new RegExp(task.title) })).toBeVisible();
    }
  });
});

test('L06 cria, renomeia e exclui coluna vazia pela UI com persistência e cancelamento', async ({ page, request, authenticatedGoto }) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const board = await f.board(); const name = f.name('coluna UI');
    await authenticatedGoto(`/boards/${board.id}`);
    await page.getByRole('button', { name: 'Nova Coluna', exact: true }).click();
    await page.getByPlaceholder('Nome da Coluna', { exact: true }).fill(name);
    const created = responseTo(page, 'POST', `/api/Stages/board/${board.id}`);
    await page.getByRole('button', { name: 'Adicionar', exact: true }).click();
    const response = await created; expect(response.status()).toBe(201);
    const id: string = await response.json();
    await page.reload();
    await expect(page.getByRole('button', { name: `Editar coluna ${name}`, exact: true })).toBeVisible();
    expect((await stages(f, board.id)).find(stage => stage.id === id)).toMatchObject({ name, category: 3 });
    await page.getByRole('button', { name: `Editar coluna ${name}`, exact: true }).click();
    const renamed = f.name('coluna renomeada');
    await editor(page).getByPlaceholder('Nome da Coluna', { exact: true }).fill(renamed);
    const updated = responseTo(page, 'PUT', `/api/Stages/${id}`);
    await editor(page).getByRole('button', { name: 'Salvar', exact: true }).click();
    expect((await updated).status()).toBe(204);
    await page.reload();
    await expect(page.getByRole('button', { name: `Editar coluna ${renamed}`, exact: true })).toBeVisible();
    expect((await stages(f, board.id)).find(stage => stage.id === id)).toMatchObject({ name: renamed, category: 3 });
    await page.getByRole('button', { name: `Excluir coluna ${renamed}`, exact: true }).click();
    expect(await confirmDeletion(page, editor(page).getByRole('button', { name: 'Excluir coluna', exact: true }), false)).toContain('coluna vazia');
    expect((await stages(f, board.id)).some(stage => stage.id === id)).toBe(true);
    const removed = responseTo(page, 'DELETE', `/api/Stages/${id}`);
    await confirmDeletion(page, editor(page).getByRole('button', { name: 'Excluir coluna', exact: true }), true);
    expect((await removed).status()).toBe(204);
    await page.reload();
    await expect(page.getByRole('button', { name: `Editar coluna ${renamed}`, exact: true })).toHaveCount(0);
    expect((await stages(f, board.id)).some(stage => stage.id === id)).toBe(false);
  });
});

test('L06 excluir coluna ocupada exige destino, mostra impacto e cancelar preserva tarefas', async ({ page, request, authenticatedGoto }) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const board = await f.board(); const source = await f.stage(board.id); const target = await f.stage(board.id);
    const tasks = [await f.item(board.id, source.id), await f.item(board.id, source.id)];
    const ids = tasks.map(task => task.id); const before = await state(f, ids);
    await authenticatedGoto(`/boards/${board.id}`);
    for (const task of tasks) await expect(page.getByRole('heading', { name: new RegExp(task.title) })).toBeVisible();
    await page.getByRole('button', { name: `Excluir coluna ${source.name}`, exact: true }).click();
    await editor(page).getByRole('button', { name: 'Excluir coluna', exact: true }).click();
    await expect(editor(page).getByRole('alert')).toContainText('transferir 2 tarefa(s)');
    expect(await state(f, ids)).toEqual(before);
    await editor(page).getByLabel('Ao excluir, transferir tarefas para').selectOption(target.id);
    const cancelled = await confirmDeletion(page, editor(page).getByRole('button', { name: 'Excluir coluna', exact: true }), false);
    expect(cancelled).toContain('2 tarefa(s)'); expect(cancelled).toContain(target.name);
    await editor(page).getByRole('button', { name: 'Cancelar', exact: true }).click();
    await page.reload();
    await expect(page.getByRole('button', { name: `Editar coluna ${source.name}`, exact: true })).toBeVisible();
    expect(await state(f, ids)).toEqual(before);
    await page.getByRole('button', { name: `Excluir coluna ${source.name}`, exact: true }).click();
    await editor(page).getByLabel('Ao excluir, transferir tarefas para').selectOption(target.id);
    const removed = responseTo(page, 'DELETE', `/api/Stages/${source.id}`);
    expect(await confirmDeletion(page, editor(page).getByRole('button', { name: 'Excluir coluna', exact: true }), true)).toContain('2 tarefa(s)');
    expect((await removed).status()).toBe(204);
    await page.reload();
    await expect(page.getByRole('button', { name: `Editar coluna ${source.name}`, exact: true })).toHaveCount(0);
    expect((await stages(f, board.id)).some(stage => stage.id === source.id)).toBe(false);
    for (const task of tasks) {
      expect(await item(f, task.id)).toMatchObject({ boardId: board.id, stageId: target.id, completedAt: null });
      await expect(page.getByRole('heading', { name: new RegExp(task.title) })).toBeVisible();
    }
  });
});

test('L06 reclassificar coluna ocupada confirma impacto e descendência; cancelar e reabrir persistem corretamente', async ({ page, request, authenticatedGoto }) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const board = await f.board(); const column = await f.stage(board.id); const outside = await f.stage(board.id);
    const parent = await f.item(board.id, column.id, { kind: 1 });
    const child = await f.item(board.id, outside.id, { parentId: parent.id, kind: 2 });
    const grandchild = await f.item(board.id, outside.id, { parentId: child.id, kind: 3 });
    const other = await f.item(board.id, column.id);
    const ids = [parent.id, other.id, child.id, grandchild.id]; const before = await state(f, ids);
    await authenticatedGoto(`/boards/${board.id}`);
    await page.getByRole('button', { name: `Editar coluna ${column.name}`, exact: true }).click();
    await editor(page).getByLabel('Classificação da coluna').selectOption('5');
    await expect(page.getByLabel('Impacto da reclassificação')).toContainText('2 tarefa(s) na coluna; 2 terão o estado alterado; 2 descendente(s)');
    await editor(page).getByRole('button', { name: 'Salvar', exact: true }).click();
    await expect(editor(page).getByRole('alert')).toContainText('confirme explicitamente');
    await editor(page).getByRole('checkbox', { name: 'Confirmo alterar o estado das tarefas desta coluna', exact: true }).check();
    await editor(page).getByRole('button', { name: 'Salvar', exact: true }).click();
    await expect(editor(page).getByRole('alert')).toContainText('confirme explicitamente');
    await editor(page).getByRole('button', { name: 'Cancelar', exact: true }).click();
    await page.reload();
    expect(await state(f, ids)).toEqual(before);
    expect((await stages(f, board.id)).find(stage => stage.id === column.id)?.category).toBe(3);

    await page.getByRole('button', { name: `Editar coluna ${column.name}`, exact: true }).click();
    await editor(page).getByLabel('Classificação da coluna').selectOption('5');
    await expect(page.getByLabel('Impacto da reclassificação')).toContainText('2 descendente(s)');
    await editor(page).getByRole('checkbox', { name: 'Confirmo alterar o estado das tarefas desta coluna', exact: true }).check();
    await editor(page).getByRole('checkbox', { name: /Aceito concluir também todos os 2 descendentes abertos/ }).check();
    const done = responseTo(page, 'PUT', `/api/Stages/${column.id}`);
    await editor(page).getByRole('button', { name: 'Salvar', exact: true }).click();
    expect((await done).status()).toBe(204);
    await page.reload();
    for (const id of ids) {
      const current = await item(f, id);
      expect(current.completedAt).not.toBeNull();
      expect(current.stageId).toBe(before.find(row => row.id === id)!.stageId);
      const events = await f.get<Array<{ kind: string }>>(`/api/WorkItems/${id}/events`);
      expect(events.filter(event => event.kind === 'stage_reclassified')).toHaveLength(1);
    }
    const completed = await state(f, ids);
    await page.getByRole('button', { name: `Editar coluna ${column.name}`, exact: true }).click();
    await expect(editor(page).getByLabel('Classificação da coluna')).toHaveValue('5');
    await editor(page).getByLabel('Classificação da coluna').selectOption('3');
    await expect(page.getByLabel('Impacto da reclassificação')).toContainText('reabrir. 2 tarefa(s) na coluna; 2 terão o estado alterado');
    await editor(page).getByRole('button', { name: 'Salvar', exact: true }).click();
    await expect(editor(page).getByRole('alert')).toContainText('confirme explicitamente');
    await editor(page).getByRole('button', { name: 'Cancelar', exact: true }).click();
    await page.reload();
    expect(await state(f, ids)).toEqual(completed);
    expect((await stages(f, board.id)).find(stage => stage.id === column.id)?.category).toBe(5);
    await page.getByRole('button', { name: `Editar coluna ${column.name}`, exact: true }).click();
    await editor(page).getByLabel('Classificação da coluna').selectOption('3');
    await expect(page.getByLabel('Impacto da reclassificação')).toContainText('2 terão o estado alterado');
    await editor(page).getByRole('checkbox', { name: 'Confirmo alterar o estado das tarefas desta coluna', exact: true }).check();
    const reopened = responseTo(page, 'PUT', `/api/Stages/${column.id}`);
    await editor(page).getByRole('button', { name: 'Salvar', exact: true }).click();
    expect((await reopened).status()).toBe(204);
    await page.reload();
    expect((await stages(f, board.id)).find(stage => stage.id === column.id)?.category).toBe(3);
    for (const id of [parent.id, other.id]) {
      expect(await item(f, id)).toMatchObject({ completedAt: null, stageId: column.id });
      const events = await f.get<Array<{ kind: string }>>(`/api/WorkItems/${id}/events`);
      expect(events.filter(event => event.kind === 'stage_reclassified')).toHaveLength(2);
    }
    for (const id of [child.id, grandchild.id]) expect(await item(f, id)).toMatchObject({ stageId: outside.id, completedAt: completed.find(row => row.id === id)!.completedAt });
  });
});

test('L06 leitor não cria nem exclui coluna pela UI ou API', async ({ page, request, authenticatedGoto }) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const board = await f.board(); const column = await f.stage(board.id);
    const viewer = await f.viewer(); const original = await stages(f, board.id);
    const data = { projectId: f.projectId, name: f.name('negada'), position: 1000, category: 3 };
    expect((await f.api(`/api/Stages/board/${board.id}`, 'POST', data, viewer)).status()).toBe(404);
    expect((await f.api(`/api/Stages/${column.id}`, 'DELETE', undefined, viewer)).status()).toBe(404);
    await page.evaluate(session => {
      localStorage.setItem('prisma_workspace_token', session.accessToken);
      localStorage.setItem('prisma_workspace_organization', session.organizationId);
    }, viewer);
    await authenticatedGoto(`/boards/${board.id}`);
    await expect(page.getByLabel('Selecionar quadro')).toHaveValue(board.id);
    await page.getByRole('button', { name: 'Nova Coluna', exact: true }).click();
    await page.getByPlaceholder('Nome da Coluna', { exact: true }).fill(data.name);
    const refused = responseTo(page, 'POST', `/api/Stages/board/${board.id}`);
    const alert = confirmDeletion(page, page.getByRole('button', { name: 'Adicionar', exact: true }), true);
    expect((await refused).status()).toBe(404);
    expect(await alert).toContain('Erro ao criar coluna');
    await page.getByRole('button', { name: 'Cancelar', exact: true }).click();
    await page.getByRole('button', { name: `Excluir coluna ${column.name}`, exact: true }).click();
    const deniedDelete = responseTo(page, 'DELETE', `/api/Stages/${column.id}`);
    await confirmDeletion(page, editor(page).getByRole('button', { name: 'Excluir coluna', exact: true }), true);
    expect((await deniedDelete).status()).toBe(404);
    await expect(editor(page).getByRole('alert')).toBeVisible();
    await page.reload();
    await expect(page.getByRole('button', { name: `Editar coluna ${column.name}`, exact: true })).toBeVisible();
    expect(await stages(f, board.id)).toEqual(original);
  });
});
