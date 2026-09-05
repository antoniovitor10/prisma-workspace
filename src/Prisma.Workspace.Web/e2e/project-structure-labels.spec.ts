import { test, expect } from './fixtures/test';

test('não exibe metodologia no cadastro nem nas configurações do projeto', async ({ page, authenticatedGoto, resolveSeedProject }) => {
  await authenticatedGoto('/projects');

  await page.getByRole('button', { name: 'Novo projeto' }).click();
  await expect(page.getByRole('combobox', { name: 'Natureza', exact: true })).toBeVisible();
  await expect(page.getByRole('combobox', { name: 'Tipo de Trabalho', exact: true })).toBeVisible();
  await expect(page.getByRole('combobox', { name: 'Estrutura de Trabalho' })).toHaveCount(0);
  await expect(page.getByText(/estrutura de trabalho/i)).toHaveCount(0);
  await expect(page.getByText(/metodologia/i)).toHaveCount(0);
  await page.getByRole('button', { name: 'Cancelar' }).click();

  const project = await resolveSeedProject();
  await authenticatedGoto(`/projects/${project.id}/settings`);

  await expect(page.getByRole('heading', { name: 'Dados do projeto' })).toBeVisible();
  await expect(page.getByRole('combobox', { name: 'Natureza', exact: true })).toBeVisible();
  await expect(page.getByRole('combobox', { name: 'Tipo de Trabalho', exact: true })).toBeVisible();
  await expect(page.getByRole('combobox', { name: 'Estrutura de Trabalho' })).toHaveCount(0);
  await expect(page.getByText(/estrutura de trabalho/i)).toHaveCount(0);
  await expect(page.getByText(/metodologia/i)).toHaveCount(0);
});
