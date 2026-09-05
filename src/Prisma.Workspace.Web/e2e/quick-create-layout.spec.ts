import { expect, test } from './fixtures/test';

test.describe('criação rápida', () => {
  test('TASK-020 mantém a seleção de quadros legível e sem overflow', async ({ page, authenticatedGoto }) => {
    await authenticatedGoto('/projects');
    await page.getByRole('button', { name: 'Novo item' }).click();

    const dialog = page.getByRole('dialog', { name: 'Criar item rapidamente' });
    await expect(dialog).toBeVisible();
    await expect(dialog.getByRole('combobox', { name: 'Projeto' })).toBeVisible();

    const checkboxes = dialog.getByRole('checkbox');
    await expect(checkboxes.first()).toBeVisible();
    const checkboxBox = await checkboxes.first().boundingBox();
    expect(checkboxBox).not.toBeNull();
    expect(checkboxBox!.width).toBeLessThanOrEqual(20);
    expect(checkboxBox!.height).toBeLessThanOrEqual(20);

    const hasHorizontalOverflow = await dialog.evaluate(element => element.scrollWidth > element.clientWidth);
    expect(hasHorizontalOverflow).toBe(false);

    const create = dialog.getByRole('button', { name: 'Criar item' });
    await expect(create).toBeDisabled();
    await dialog.getByRole('textbox', { name: 'Título da tarefa' }).fill('Validar layout da criação rápida');
    await expect(create).toBeEnabled();
  });
});
