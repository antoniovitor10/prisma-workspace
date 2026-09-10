import { expect, test } from './fixtures/test';

test.describe('criação rápida', () => {
  // D83: a tarefa pertence a um único quadro, então a escolha é um seletor e não mais
  // uma lista de checkboxes. O que continua valendo é a legibilidade e a ausência de
  // overflow no diálogo, que era o objeto original da TASK-020.
  test('TASK-020 mantém a seleção de quadro legível e sem overflow', async ({ page, authenticatedGoto }) => {
    await authenticatedGoto('/projects');
    await page.getByRole('button', { name: 'Novo item' }).click();
    await page.getByRole('menuitem', { name: 'Tarefa', exact: true }).click();

    const dialog = page.getByRole('dialog', { name: 'Criar item rapidamente' });
    await expect(dialog).toBeVisible();
    await expect(dialog.getByRole('combobox', { name: 'Projeto' })).toBeVisible();

    const boardSelect = dialog.getByRole('combobox', { name: 'Quadro' });
    await expect(boardSelect).toBeVisible();
    await expect(boardSelect).not.toHaveValue('');
    await expect(dialog.getByRole('checkbox')).toHaveCount(0);

    const hasHorizontalOverflow = await dialog.evaluate(element => element.scrollWidth > element.clientWidth);
    expect(hasHorizontalOverflow).toBe(false);

    const create = dialog.getByRole('button', { name: 'Criar item' });
    await expect(create).toBeDisabled();
    await dialog.getByRole('textbox', { name: 'Título da tarefa' }).fill('Validar layout da criação rápida');
    await expect(create).toBeEnabled();
  });
});
