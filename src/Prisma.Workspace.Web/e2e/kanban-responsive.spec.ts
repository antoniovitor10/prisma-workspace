import { test, expect } from './fixtures/test';

/**
 * Regressão: no Pixel 5 a barra de ações do quadro estourava (~803px em 393px),
 * a página rolava na horizontal e o botão de visão "Gantt" interceptava o clique
 * em "Nova Coluna".
 */
test.describe('Kanban responsivo e acessível', () => {
  test('barra de ações: Nova Coluna clicável sem overflow horizontal da página',
    async ({ page, authenticatedGoto, resolveSeedProject }, testInfo) => {
      test.skip(testInfo.project.name !== 'chromium-mobile', 'Viewport Pixel 5 (mobile)');

      const project = await resolveSeedProject();
      const board = project.boards[0];

      await authenticatedGoto(`/boards/${board.id}`);
      await expect(page.getByRole('button', { name: 'Nova Coluna' })).toBeVisible();
      await expect(page.locator('[data-stage-id]').first()).toBeVisible({ timeout: 15000 });

      const overflow = await page.evaluate(() => ({
        scrollWidth: document.documentElement.scrollWidth,
        clientWidth: document.documentElement.clientWidth,
      }));
      expect(
        overflow.scrollWidth,
        `Página não deve rolar na horizontal (scrollWidth=${overflow.scrollWidth}, clientWidth=${overflow.clientWidth})`,
      ).toBeLessThanOrEqual(overflow.clientWidth);

      await page.getByRole('button', { name: 'Nova Coluna' }).click();
      await expect(page.getByPlaceholder('Nome da Coluna')).toBeVisible();
      await expect(page.getByRole('heading', { name: 'Criar Nova Coluna' })).toBeVisible();
    });

  test('botões só de ícone do quadro têm nome acessível',
    async ({ page, authenticatedGoto, resolveSeedProject }) => {
      const project = await resolveSeedProject();
      const board = project.boards[0];

      await authenticatedGoto(`/boards/${board.id}`);
      await expect(page.getByRole('button', { name: 'Nova Coluna' })).toBeVisible();
      await expect(page.locator('[data-stage-id]').first()).toBeVisible({ timeout: 15000 });

      // Colunas: setas de reordenar (antes só tinham title, sem accessible name).
      const moveLeft = page.getByRole('button', { name: /Mover coluna .+ para a esquerda/ });
      const moveRight = page.getByRole('button', { name: /Mover coluna .+ para a direita/ });
      await expect(moveLeft.first()).toBeVisible();
      await expect(moveRight.first()).toBeVisible();

      // Cartões: mover e cronômetro (antes ActionIcon sem aria-label).
      const cardMovePrev = page.getByRole('button', { name: 'Mover tarefa para a coluna anterior' });
      if (await cardMovePrev.count() > 0) {
        await expect(cardMovePrev.first()).toBeAttached();
        await expect(page.getByRole('button', { name: 'Mover tarefa para a próxima coluna' }).first()).toBeAttached();
        await expect(page.getByRole('button', { name: /^(Iniciar|Parar) cronômetro da tarefa/ }).first()).toBeAttached();
      }

      // Nenhum botão só-ícone sem nome dentro do quadro (main do AppShell).
      const unnamed = await page.evaluate(() => {
        const main = document.querySelector('main');
        if (!main) return -1;
        return [...main.querySelectorAll('button')].filter((btn) => {
          const name = (btn.getAttribute('aria-label') || btn.textContent || '').replace(/\s+/g, ' ').trim();
          return name.length === 0;
        }).length;
      });
      expect(unnamed, 'Botões sem nome acessível no quadro').toBe(0);
    });
});
