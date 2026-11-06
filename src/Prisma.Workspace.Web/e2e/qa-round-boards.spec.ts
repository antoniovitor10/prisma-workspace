import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import type { Page, TestInfo } from '@playwright/test';
import { test, expect } from './fixtures/test';
import { withBoardFactory } from './fixtures/boards';

test.beforeEach(async ({ page }, info) => {
  await page.setViewportSize(info.project.name.includes('mobile') ? { width: 390, height: 844 } : { width: 1440, height: 900 });
});
async function capture(page: Page, info: TestInfo, phase: string) {
  const directory = resolve('../../.runlogs/oficina/screenshots');
  mkdirSync(directory, { recursive: true });
  await page.screenshot({ path: `${directory}/C6-${info.project.name}-${phase}.png`, scale: 'css' });
}

test('C6 seletor não lista quadros de projeto arquivado e restaura os mesmos IDs', async ({ page, request, authenticatedGoto }, info) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const board = await f.board();
    const stages = await f.get<Array<{ id: string }>>(`/api/Stages/board/${board.id}`);
    const item = await f.item(board.id, stages[0].id);
    const listed = async () => f.get<Array<{ id: string }>>('/api/Boards');
    expect((await listed()).some(candidate => candidate.id === board.id)).toBe(true);
    await page.goto(`/boards/${board.id}`);
    await expect(page.getByRole('combobox', { name: 'Selecionar quadro', exact: true }).locator(`option[value="${board.id}"]`)).toHaveCount(1);
    await capture(page, info, 'ativo');
    await withBoardFactory(page, request, async anchor => {
    const anchorBoard = await anchor.board();
    expect((await f.api(`/api/projects/${f.projectId}/archive`, 'POST')).status()).toBe(204);
    expect((await listed()).some(candidate => candidate.id === board.id)).toBe(false);
    await page.goto(`/boards/${anchorBoard.id}`);
    const picker = page.getByRole('combobox', { name: 'Selecionar quadro', exact: true });
    await expect(picker).toHaveValue(anchorBoard.id);
    await expect(picker.locator(`option[value="${board.id}"]`)).toHaveCount(0);
    await capture(page, info, 'arquivado');
    expect((await f.api(`/api/projects/${f.projectId}/reactivate`, 'POST')).status()).toBe(204);
    expect((await listed()).some(candidate => candidate.id === board.id)).toBe(true);
    }, 'Oficina C6 âncora');
    const items = await f.get<Array<{ id: string; number: number }>>(`/api/WorkItems/board/${board.id}`);
    const persisted = items.find(candidate => candidate.id === item.id);
    expect(persisted).toBeDefined();
    expect(persisted!.number).toBeGreaterThan(0);
    await page.goto(`/boards/${board.id}`);
    await expect(page.getByRole('button', { name: 'Nova Coluna', exact: true })).toBeVisible();
    await expect(page.getByText(`#${persisted!.number} · ${item.title}`, { exact: true })).toBeVisible();
    await expect(page.getByRole('combobox', { name: 'Selecionar quadro', exact: true })).toHaveValue(board.id);
    await capture(page, info, 'reativado');
  }, 'Oficina C6');
});
