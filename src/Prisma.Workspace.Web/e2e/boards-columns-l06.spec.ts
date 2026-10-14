import type { Locator, Page, Request } from '@playwright/test';
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


// A recarga só pode ser conferida depois que o quadro e suas projeções próprias chegarem.
async function loadedBoard(page: Page, boardId: string, navigate: () => Promise<unknown>) {
  // Observa requisições novas, não respostas tardias do refresh anterior à recarga.
  const loaded = ['/api/Boards', `/api/Stages/board/${boardId}`, `/api/WorkItems/board/${boardId}`]
    .map(path => page.waitForRequest(request => request.method() === 'GET'
      && new URL(request.url()).pathname.toLowerCase() === path.toLowerCase()));
  await navigate();
  for (const request of await Promise.all(loaded)) {
    const response = await request.response();
    expect(response).not.toBeNull(); expect(response!.status()).toBe(200);
    expect(await response!.finished()).toBeNull();
  }
}
function writesTo(page: Page, method: string, path: string) {
  let count = 0;
  const listener = (request: Request) => {
    if (request.method() === method && new URL(request.url()).pathname.toLowerCase() === path.toLowerCase()) count++;
  };
  page.on('request', listener);
  return { count: () => count, dispose: () => page.off('request', listener) };
}
async function stageFamily(f: BoardFactory, category = 3) {
  const board = await f.board(); const column = await f.stage(board.id, category); const outside = await f.stage(board.id, category);
  const parent = await f.item(board.id, column.id, { kind: 1 });
  const child = await f.item(board.id, outside.id, { parentId: parent.id, kind: 2 });
  const grandchild = await f.item(board.id, outside.id, { parentId: child.id, kind: 3 });
  const other = await f.item(board.id, column.id);
  return { board, column, outside, parent, child, grandchild, other, ids: [parent.id, other.id, child.id, grandchild.id] };
}
async function selectCategory(page: Page, column: BoardStage, category: string) {
  await page.getByRole('button', { name: `Editar coluna ${column.name}`, exact: true }).click();
  await expect(editor(page).getByRole('alert')).toHaveCount(0);
  await editor(page).getByLabel('Classificação da coluna').selectOption(category);
  await expect(page.getByLabel('Impacto da reclassificação')).toContainText('2 tarefa(s) na coluna; 2 terão o estado alterado');
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

test('L06 excluir coluna ocupada: API exige destino; UI informa impacto e cancelar preserva tarefas', async ({ page, request, authenticatedGoto }) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const board = await f.board(); const source = await f.stage(board.id); const target = await f.stage(board.id);
    const tasks = [await f.item(board.id, source.id), await f.item(board.id, source.id)];
    const ids = tasks.map(task => task.id); const before = await state(f, ids);
    const refused = await f.api(`/api/Stages/${source.id}`, 'DELETE');
    expect(refused.status()).toBe(400);
    expect((await refused.json()).detail).toContain('Escolha a coluna de destino');
    expect(await state(f, ids)).toEqual(before);
    await loadedBoard(page, board.id, () => page.goto(`/boards/${board.id}`, { waitUntil: 'domcontentloaded' }));
    await page.getByRole('button', { name: `Excluir coluna ${source.name}`, exact: true }).click();
    await editor(page).getByRole('button', { name: 'Excluir coluna', exact: true }).click();
    await expect(editor(page).getByRole('alert')).toContainText('transferir 2 tarefa(s)');
    await editor(page).getByLabel('Ao excluir, transferir tarefas para').selectOption(target.id);
    const cancelled = await confirmDeletion(page, editor(page).getByRole('button', { name: 'Excluir coluna', exact: true }), false);
    expect(cancelled).toContain('2 tarefa(s)'); expect(cancelled).toContain(target.name);
    await editor(page).getByRole('button', { name: 'Cancelar', exact: true }).click();
    expect(await state(f, ids)).toEqual(before);
    expect(await stages(f, board.id)).toEqual(expect.arrayContaining([expect.objectContaining({ id: source.id })]));
    await expect(page.getByRole('button', { name: `Editar coluna ${source.name}`, exact: true })).toBeVisible();
  });
});

test('L06 confirmar exclusão ocupada transfere os mesmos IDs e persiste após recarga', async ({ page, request, authenticatedGoto }) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const board = await f.board(); const source = await f.stage(board.id); const target = await f.stage(board.id);
    const tasks = [await f.item(board.id, source.id), await f.item(board.id, source.id)];
    await loadedBoard(page, board.id, () => page.goto(`/boards/${board.id}`, { waitUntil: 'domcontentloaded' }));
    await page.getByRole('button', { name: `Excluir coluna ${source.name}`, exact: true }).click();
    await editor(page).getByLabel('Ao excluir, transferir tarefas para').selectOption(target.id);
    const removed = responseTo(page, 'DELETE', `/api/Stages/${source.id}`);
    expect(await confirmDeletion(page, editor(page).getByRole('button', { name: 'Excluir coluna', exact: true }), true)).toContain('2 tarefa(s)');
    expect((await removed).status()).toBe(204);
    await expect(editor(page)).toBeHidden();
    await loadedBoard(page, board.id, () => page.reload({ waitUntil: 'domcontentloaded' }));
    await expect(page.getByRole('button', { name: `Editar coluna ${source.name}`, exact: true })).toHaveCount(0);
    expect((await stages(f, board.id)).some(stage => stage.id === source.id)).toBe(false);
    const persisted = await Promise.all(tasks.map(task => item(f, task.id)));
    for (const current of persisted) expect(current).toMatchObject({ boardId: board.id, stageId: target.id, completedAt: null });
    for (const task of tasks) await expect(page.getByRole('heading', { name: new RegExp(task.title) })).toBeVisible();
  });
});

test('L06 reclassificação: API e UI exigem ambos os consentimentos; cancelamento não altera estado', async ({ page, request, authenticatedGoto }) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const { board, column, ids } = await stageFamily(f); const before = await state(f, ids);
    const impact = await f.get<{ snapshotToken: string; openDescendants: number }>(`/api/Stages/${column.id}/impact?category=5`);
    expect(impact.openDescendants).toBe(2);
    const refusedCategory = await f.api(`/api/Stages/${column.id}`, 'PUT', { name: column.name, category: 5, impactToken: impact.snapshotToken });
    expect(refusedCategory.status()).toBe(400);
    expect((await refusedCategory.json()).detail).toContain('Confirme a mudança de classificação');
    const refusedDescendants = await f.api(`/api/Stages/${column.id}`, 'PUT', {
      name: column.name, category: 5, confirmCategoryChange: true, impactToken: impact.snapshotToken,
    });
    expect(refusedDescendants.status()).toBe(400);
    expect((await refusedDescendants.json()).detail).toContain('Confirme separadamente a conclusão de 2 descendente(s)');
    expect(await state(f, ids)).toEqual(before);
    await loadedBoard(page, board.id, () => page.goto(`/boards/${board.id}`, { waitUntil: 'domcontentloaded' }));
    const observed = writesTo(page, 'PUT', `/api/Stages/${column.id}`);
    try {
      await selectCategory(page, column, '5');
      await editor(page).getByRole('button', { name: 'Salvar', exact: true }).click();
      await expect(editor(page).getByRole('alert')).toHaveText('Revise o impacto e confirme explicitamente as alterações.');
      expect(observed.count()).toBe(0);
      await editor(page).getByRole('button', { name: 'Cancelar', exact: true }).click();
      // Nova abertura elimina o alerta anterior: a segunda recusa precisa produzir um novo erro.
      await selectCategory(page, column, '5');
      await editor(page).getByRole('checkbox', { name: 'Confirmo alterar o estado das tarefas desta coluna', exact: true }).check();
      await expect(editor(page).getByRole('alert')).toHaveCount(0);
      await editor(page).getByRole('button', { name: 'Salvar', exact: true }).click();
      await expect(editor(page).getByRole('alert')).toHaveText('Revise o impacto e confirme explicitamente as alterações.');
      expect(observed.count()).toBe(0);
      await editor(page).getByRole('button', { name: 'Cancelar', exact: true }).click();
      expect(await state(f, ids)).toEqual(before);
      expect(await stages(f, board.id)).toEqual(expect.arrayContaining([expect.objectContaining({ id: column.id, category: 3 })]));
      expect(observed.count()).toBe(0);
    } finally { observed.dispose(); }
  });
});

test('L06 concluir coluna ocupada confirma descendência recursiva e persiste histórico individual', async ({ page, request, authenticatedGoto }) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const { board, column, ids } = await stageFamily(f); const before = await state(f, ids);
    await loadedBoard(page, board.id, () => page.goto(`/boards/${board.id}`, { waitUntil: 'domcontentloaded' }));
    await selectCategory(page, column, '5');
    await editor(page).getByRole('checkbox', { name: 'Confirmo alterar o estado das tarefas desta coluna', exact: true }).check();
    await editor(page).getByRole('checkbox', { name: /Aceito concluir também todos os 2 descendentes abertos/ }).check();
    const done = responseTo(page, 'PUT', `/api/Stages/${column.id}`);
    await editor(page).getByRole('button', { name: 'Salvar', exact: true }).click();
    expect((await done).status()).toBe(204);
    await expect(editor(page)).toBeHidden();
    await loadedBoard(page, board.id, () => page.reload({ waitUntil: 'domcontentloaded' }));
    expect(await stages(f, board.id)).toEqual(expect.arrayContaining([expect.objectContaining({ id: column.id, category: 5 })]));
    const persisted = await state(f, ids);
    for (const current of persisted) {
      expect(current.completedAt).not.toBeNull();
      expect(current.stageId).toBe(before.find(row => row.id === current.id)!.stageId);
    }
    const events = await Promise.all(ids.map(id => f.get<Array<{ kind: string }>>(`/api/WorkItems/${id}/events`)));
    for (const feed of events) expect(feed.filter(event => event.kind === 'stage_reclassified')).toHaveLength(1);
  });
});

test('L06 reabrir: API e UI exigem confirmação; cancelar preserva tarefas concluídas', async ({ page, request, authenticatedGoto }) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const { board, column, ids } = await stageFamily(f, 5); const before = await state(f, ids);
    const impact = await f.get<{ snapshotToken: string }>(`/api/Stages/${column.id}/impact?category=3`);
    const refused = await f.api(`/api/Stages/${column.id}`, 'PUT', { name: column.name, category: 3, impactToken: impact.snapshotToken });
    expect(refused.status()).toBe(400); expect((await refused.json()).detail).toContain('Confirme a mudança de classificação');
    await loadedBoard(page, board.id, () => page.goto(`/boards/${board.id}`, { waitUntil: 'domcontentloaded' }));
    const observed = writesTo(page, 'PUT', `/api/Stages/${column.id}`);
    try {
      await selectCategory(page, column, '3');
      await editor(page).getByRole('button', { name: 'Salvar', exact: true }).click();
      await expect(editor(page).getByRole('alert')).toHaveText('Revise o impacto e confirme explicitamente as alterações.');
      expect(observed.count()).toBe(0);
      await editor(page).getByRole('button', { name: 'Cancelar', exact: true }).click();
      expect(await state(f, ids)).toEqual(before);
      expect(await stages(f, board.id)).toEqual(expect.arrayContaining([expect.objectContaining({ id: column.id, category: 5 })]));
      expect(observed.count()).toBe(0);
    } finally { observed.dispose(); }
  });
});

test('L06 reabrir coluna ocupada pela UI persiste estado e histórico das tarefas nela', async ({ page, request, authenticatedGoto }) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const board = await f.board(); const column = await f.stage(board.id, 5);
    const ids = [await f.item(board.id, column.id), await f.item(board.id, column.id)].map(value => value.id);
    await loadedBoard(page, board.id, () => page.goto(`/boards/${board.id}`, { waitUntil: 'domcontentloaded' }));
    await selectCategory(page, column, '3');
    await editor(page).getByRole('checkbox', { name: 'Confirmo alterar o estado das tarefas desta coluna', exact: true }).check();
    const reopened = responseTo(page, 'PUT', `/api/Stages/${column.id}`);
    await editor(page).getByRole('button', { name: 'Salvar', exact: true }).click();
    expect((await reopened).status()).toBe(204);
    await expect(editor(page)).toBeHidden();
    await loadedBoard(page, board.id, () => page.reload({ waitUntil: 'domcontentloaded' }));
    expect(await stages(f, board.id)).toEqual(expect.arrayContaining([expect.objectContaining({ id: column.id, category: 3 })]));
    for (const current of await state(f, ids)) expect(current).toMatchObject({ completedAt: null, stageId: column.id });
    const events = await Promise.all(ids.map(id => f.get<Array<{ kind: string }>>(`/api/WorkItems/${id}/events`)));
    for (const feed of events) expect(feed.filter(event => event.kind === 'stage_reclassified')).toHaveLength(1);
  });
});

test('L06 reabertura via API preserva descendentes concluídos fora da coluna', async ({ page, request, authenticatedGoto }) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const { board, column, outside, parent, other, child, grandchild, ids } = await stageFamily(f, 5);
    const before = await state(f, ids);
    const impact = await f.get<{ snapshotToken: string }>(`/api/Stages/${column.id}/impact?category=3`);
    const reopened = await f.api(`/api/Stages/${column.id}`, 'PUT', {
      name: column.name, category: 3, confirmCategoryChange: true, impactToken: impact.snapshotToken,
    });
    expect(reopened.status()).toBe(204);
    expect(await stages(f, board.id)).toEqual(expect.arrayContaining([expect.objectContaining({ id: column.id, category: 3 })]));
    const persisted = await state(f, ids);
    for (const id of [parent.id, other.id]) expect(persisted.find(row => row.id === id)).toMatchObject({ completedAt: null, stageId: column.id });
    for (const id of [child.id, grandchild.id]) expect(persisted.find(row => row.id === id)).toMatchObject({ stageId: outside.id, completedAt: before.find(row => row.id === id)!.completedAt });
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
