import { test, expect, navigateWithApi } from './fixtures/functional';
import { withBoardFactory } from './fixtures/boards';

/**
 * Regressão: concluir a tarefa pela gaveta de detalhe deixava o card preso na coluna
 * antiga do quadro, porque o quadro lia a etapa de WorkItemBoardPlacement enquanto o
 * update só gravava WorkItem.StageId. A projeção foi eliminada pela D83 e a tarefa passou
 * a ter etapa única, mas o comportamento observável continua valendo como contrato.
 */
test('concluir pela gaveta move o card no quadro e a mudança sobrevive ao recarregamento',
  async ({ page, request, authenticatedGoto }) => {
    await authenticatedGoto('/projects');
    await withBoardFactory(page, request, async factory => {
      const board = await factory.board();
      // D89: colunas próprias do quadro, sem depender do seed ou de outro spec.
      const aberta = await factory.stage(board.id, 3);
      const concluido = await factory.stage(board.id, 5); // StageCategory.Done no domínio.
      const alvo = await factory.item(board.id, aberta.id);
      const openBoard = () => navigateWithApi(page, [`/api/WorkItems/board/${board.id}`],
        () => page.goto(`/boards/${board.id}`, { waitUntil: 'domcontentloaded' }));
      await openBoard();
      await page.getByRole('button', { name: 'Lista', exact: true }).click();

      const linha = page.getByRole('row').filter({ hasText: alvo.title }).first();
      await expect(linha).toBeVisible();
      await expect(linha.getByRole('cell').nth(3)).toHaveText(aberta.name);

      await linha.click();
      const gaveta = page.getByRole('dialog');
      await expect(gaveta).toBeVisible();

      const saved = page.waitForResponse(response => response.request().method() === 'PUT'
        && new URL(response.url()).pathname.toLowerCase() === `/api/workitems/${alvo.id}`);
      await gaveta.getByRole('combobox', { name: 'Status' }).selectOption(concluido.id);
      expect((await saved).status()).toBe(204);
      const detail = await factory.get<{ stageId: string; completedAt: string | null }>(`/api/WorkItems/${alvo.id}`);
      expect(detail.stageId).toBe(concluido.id);
      expect(detail.completedAt).toMatch(/^\d{4}-\d{2}-\d{2}T/);
      expect(await factory.get(`/api/WorkItems/board/${board.id}`)).toEqual(expect.arrayContaining([
        expect.objectContaining({ id: alvo.id, stageId: concluido.id }),
      ]));

      // A gaveta é modal: enquanto aberta, o restante da página sai da árvore de
      // acessibilidade. Fechamos antes de voltar a consultar o quadro.
      await page.keyboard.press('Escape');
      await expect(gaveta).toBeHidden();

      await expect(page.getByRole('row').filter({ hasText: alvo.title }).first()
        .getByRole('cell').nth(3)).toHaveText(concluido.name);

      // E precisa continuar correto depois de recarregar, que é o que o defeito quebrava:
      // a divergência ficava gravada no banco e sobrevivia a qualquer recarregamento.
      await openBoard();
      await page.getByRole('button', { name: 'Lista', exact: true }).click();
      await expect(page.getByRole('row').filter({ hasText: alvo.title }).first()
        .getByRole('cell').nth(3)).toHaveText(concluido.name);
    }, 'SYNC');
  });
