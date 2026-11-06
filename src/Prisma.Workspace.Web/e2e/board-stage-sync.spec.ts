import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { test, expect } from './fixtures/test';
import { withBoardFactory, type BoardStage } from './fixtures/boards';

/** Regressão D83: a etapa única deve atualizar quadro, gaveta e leitura persistida. */
test('concluir pela gaveta move o card no quadro e a mudança sobrevive ao recarregamento',
  async ({ page, request, authenticatedGoto }, info) => {
    await page.setViewportSize(info.project.name.includes('mobile') ? { width: 390, height: 844 } : { width: 1440, height: 900 });
    await authenticatedGoto('/projects');
    await withBoardFactory(page, request, async factory => {
      const board = await factory.board();
      const stages = await factory.get<BoardStage[]>(`/api/Stages/board/${board.id}`);
      const initial = stages.find(stage => stage.category === 1);
      const completed = stages.find(stage => stage.category === 5);
      expect(initial, 'A fixture deve ter etapa inicial.').toBeTruthy();
      expect(completed, 'A fixture deve ter etapa de conclusão.').toBeTruthy();
      expect(completed!.category).toBe(5);
      const target = await factory.item(board.id, initial!.id, { kind: 5 });
      const row = () => page.getByRole('row').filter({ hasText: target.title }).first();

      await authenticatedGoto(`/boards/${board.id}`);
      await page.getByRole('button', { name: 'Lista', exact: true }).click();
      await expect(row()).toBeVisible();
      await expect(row().getByRole('cell').nth(3)).not.toHaveText(completed!.name);
      await row().click();
      const drawer = page.getByRole('dialog');
      await expect(drawer).toBeVisible();
      await drawer.getByRole('combobox', { name: 'Status' }).selectOption({ label: completed!.name });
      await page.keyboard.press('Escape');
      await expect(drawer).toBeHidden();
      await expect(row().getByRole('cell').nth(3)).toHaveText(completed!.name);

      // A recarga precisa ler a mesma etapa persistida, não apenas o cache otimista.
      await authenticatedGoto(`/boards/${board.id}`);
      await page.getByRole('button', { name: 'Lista', exact: true }).click();
      await expect(row().getByRole('cell').nth(3)).toHaveText(completed!.name);
      await expect.poll(async () => {
        const persisted = await factory.get<{ stageId: string; completedAt: string | null }>(`/api/WorkItems/${target.id}`);
        return { stageId: persisted.stageId, completed: Boolean(persisted.completedAt) };
      }).toEqual({ stageId: completed!.id, completed: true });
      await row().getByRole('cell').nth(3).scrollIntoViewIfNeeded();
      await expect(row().getByRole('cell').nth(3)).toBeVisible();
      const directory = resolve('../../.runlogs/oficina/screenshots');
      mkdirSync(directory, { recursive: true });
      await page.screenshot({ path: `${directory}/board-sync-${info.project.name}.png`, scale: 'css' });
    }, 'Oficina-sync');
  });
