import type { APIRequestContext, Page } from '@playwright/test';
import { test, expect, navigateWithApi } from './fixtures/functional';
import { withBoardFactory, type BoardFactory, type BoardStage } from './fixtures/boards';

async function withOwnProject(page: Page, request: APIRequestContext, run: (factory: BoardFactory) => Promise<void>) {
  await page.goto('/projects', { waitUntil: 'domcontentloaded' });
  await withBoardFactory(page, request, run, 'D89');
}
async function openBoard(page: Page, boardId: string) {
  await navigateWithApi(page, [`/api/WorkItems/board/${boardId}`],
    () => page.goto(`/boards/${boardId}`, { waitUntil: 'domcontentloaded' }));
}

// Os três contratos antes compartilhavam um prazo de 30s, incluindo toda a
// preparação e a limpeza. Cada caso agora tem projeto próprio e mantém seu efeito.
test('colunas independentes: cópia, ordem e exclusão de coluna preservam a tarefa', async ({ page, request }) => {
  await withOwnProject(page, request, async factory => {
    const a = await factory.board();
    const copied = await factory.api('/api/Boards', 'POST', { name: factory.name('cópia'),
      projectId: factory.projectId, copyStagesFromBoardId: a.id });
    expect(copied.status()).toBe(201);
    const b: string = await copied.json();
    const aStages = await factory.get<BoardStage[]>(`/api/Stages/board/${a.id}`);
    const bStages = await factory.get<BoardStage[]>(`/api/Stages/board/${b}`);
    expect(aStages).toHaveLength(3);
    expect(bStages).toHaveLength(3);
    expect(aStages.map(stage => stage.id)).not.toEqual(bStages.map(stage => stage.id));
    expect((await factory.api(`/api/Stages/${aStages[0].id}`, 'PUT', { name: factory.name('coluna') })).status()).toBe(204);
    expect((await factory.get<BoardStage[]>(`/api/Stages/board/${b}`))[0].name).toBe(bStages[0].name);
    expect((await factory.api(`/api/Stages/board/${a.id}/order`, 'PUT', aStages.map(stage => stage.id).reverse())).status()).toBe(204);
    expect((await factory.api(`/api/Stages/board/${a.id}/order`, 'PUT', bStages.map(stage => stage.id))).status()).toBe(400);
    expect((await factory.get<BoardStage[]>(`/api/Stages/board/${b}`)).map(stage => stage.id)).toEqual(bStages.map(stage => stage.id));

    const task = await factory.item(a.id, aStages[0].id);
    expect((await factory.api('/api/WorkItems/move', 'POST', {
      workItemId: task.id, destinationStageId: bStages[0].id, position: 100,
    })).status()).toBe(400);
    expect((await factory.api(`/api/Boards/${a.id}`, 'DELETE')).status()).toBe(400);
    expect((await factory.api(`/api/Stages/${aStages[0].id}`, 'DELETE')).status()).toBe(400);
    expect((await factory.api(`/api/Stages/${aStages[0].id}?destinationStageId=${aStages[1].id}`, 'DELETE')).status()).toBe(204);
    expect(await factory.get(`/api/WorkItems/${task.id}`)).toMatchObject({ stageId: aStages[1].id });
    await openBoard(page, a.id);
    await expect(page.getByRole('heading', { name: task.title, exact: true })).toBeVisible();
  });
});

test('colunas independentes: transferência pela gaveta persiste no quadro de destino', async ({ page, request }) => {
  await withOwnProject(page, request, async factory => {
    const a = await factory.board();
    const b = await factory.board();
    const source = await factory.stage(a.id);
    const destination = await factory.stage(b.id);
    const task = await factory.item(a.id, source.id);
    await navigateWithApi(page, [`/api/projects/${factory.projectId}/backlog`],
      () => page.goto(`/projects/${factory.projectId}/backlog`, { waitUntil: 'domcontentloaded' }));
    await page.getByRole('button', { name: `Abrir detalhes de ${task.title}`, exact: true }).click();
    const dialog = page.locator('[role="dialog"][aria-describedby^="task-description-"]');
    await expect(dialog).toBeVisible();
    await dialog.locator('summary').getByText('Transferir tarefa', { exact: true }).click();
    await dialog.getByLabel('Quadro de destino', { exact: true }).selectOption(b.id);
    await dialog.getByLabel('Coluna de destino', { exact: true }).selectOption(destination.id);
    const transferred = page.waitForResponse(response => response.request().method() === 'POST'
      && new URL(response.url()).pathname === `/api/workitems/${task.id}/transfer`);
    await dialog.getByRole('button', { name: 'Transferir tarefa', exact: true }).click();
    expect((await transferred).status()).toBe(204);
    expect(await factory.get(`/api/WorkItems/${task.id}`)).toMatchObject({ boardId: b.id, stageId: destination.id });
    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();
    await openBoard(page, b.id);
    await expect(page.getByRole('heading', { name: task.title, exact: true })).toBeVisible();
  });
});

test('colunas independentes: excluir quadro ocupado preserva destino e histórico de três movimentos', async ({ page, request }) => {
  await withOwnProject(page, request, async factory => {
    const a = await factory.board();
    const b = await factory.board();
    const removed = await factory.stage(a.id);
    const destination = await factory.stage(a.id);
    const intermediate = await factory.stage(b.id);
    const task = await factory.item(a.id, removed.id);
    // Mesmo encadeamento de histórico do caso original: excluir coluna,
    // transferir entre quadros e excluir o quadro ocupado com destino explícito.
    expect((await factory.api(`/api/Stages/${removed.id}?destinationStageId=${destination.id}`, 'DELETE')).status()).toBe(204);
    expect((await factory.api(`/api/workitems/${task.id}/transfer`, 'POST', {
      destinationBoardId: b.id, destinationStageId: intermediate.id,
    })).status()).toBe(204);
    expect(await factory.get(`/api/WorkItems/${task.id}`)).toMatchObject({ boardId: b.id, stageId: intermediate.id });
    expect((await factory.api(`/api/Boards/${b.id}?destinationBoardId=${a.id}&destinationStageId=${destination.id}`, 'DELETE')).status()).toBe(204);
    expect(await factory.get(`/api/WorkItems/${task.id}`)).toMatchObject({ boardId: a.id, stageId: destination.id });
    const events = await factory.get<unknown[]>(`/api/WorkItems/${task.id}/events`);
    expect(events.length).toBeGreaterThanOrEqual(3);
    await openBoard(page, a.id);
    await expect(page.getByRole('heading', { name: task.title, exact: true })).toBeVisible();
  });
});
