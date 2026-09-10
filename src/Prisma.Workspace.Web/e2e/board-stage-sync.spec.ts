import { test, expect, authenticatedApiGet } from './fixtures/test';

type Stage = { id: string; name: string; boardId: string };
type BoardItem = { id: string; title: string; stageId: string | null };

/**
 * Regressão: concluir a tarefa pela gaveta de detalhe deixava o card preso na coluna
 * antiga do quadro, porque o quadro lia a etapa de WorkItemBoardPlacement enquanto o
 * update só gravava WorkItem.StageId. A projeção foi eliminada pela D83 e a tarefa passou
 * a ter etapa única, mas o comportamento observável continua valendo como contrato.
 */
test('concluir pela gaveta move o card no quadro e a mudança sobrevive ao recarregamento',
  async ({ page, authenticatedGoto, resolveSeedProject }) => {
    const project = await resolveSeedProject();
    const board = project.boards[0];

    // D83: o fluxo pertence ao projeto, nao ao quadro.
    const stages = await authenticatedApiGet<Stage[]>(page, `/api/Stages/project/${project.id}`);
    const concluido = stages.find((stage) => /conclu/i.test(stage.name));
    expect(concluido, `O quadro ${board.name} precisa de uma etapa de conclusão.`).toBeTruthy();

    const items = await authenticatedApiGet<BoardItem[]>(page, `/api/WorkItems/board/${board.id}`);
    const alvo = items.find((item) => item.stageId !== concluido!.id);
    expect(alvo, `O quadro ${board.name} precisa de uma tarefa fora da etapa de conclusão.`).toBeTruthy();

    await authenticatedGoto(`/boards/${board.id}`);
    await page.getByRole('button', { name: 'Lista', exact: true }).click();

    const linha = page.getByRole('row').filter({ hasText: alvo!.title }).first();
    await expect(linha).toBeVisible();
    await expect(linha.getByRole('cell').nth(3)).not.toHaveText(concluido!.name);

    await linha.click();
    const gaveta = page.getByRole('dialog');
    await expect(gaveta).toBeVisible();

    await gaveta.getByRole('combobox', { name: 'Status' }).selectOption({ label: concluido!.name });

    // A gaveta é modal: enquanto aberta, o restante da página sai da árvore de
    // acessibilidade. Fechamos antes de voltar a consultar o quadro.
    await page.keyboard.press('Escape');
    await expect(gaveta).toBeHidden();

    await expect(page.getByRole('row').filter({ hasText: alvo!.title }).first()
      .getByRole('cell').nth(3)).toHaveText(concluido!.name);

    // E precisa continuar correto depois de recarregar, que é o que o defeito quebrava:
    // a divergência ficava gravada no banco e sobrevivia a qualquer recarregamento.
    await authenticatedGoto(`/boards/${board.id}`);
    await page.getByRole('button', { name: 'Lista', exact: true }).click();
    await expect(page.getByRole('row').filter({ hasText: alvo!.title }).first()
      .getByRole('cell').nth(3)).toHaveText(concluido!.name);
  });
