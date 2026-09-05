import { test, expect } from './fixtures/test';

test('TASK-038 segmenta itens, preserva a consulta na URL e salva uma visão pessoal', async ({
  page,
  authenticatedGoto,
  resolveSeedProject,
}, testInfo) => {
  const project = await resolveSeedProject();
  await authenticatedGoto(`/projects/${project.id}/items`);

  await expect(page.getByRole('heading', { name: 'Itens do projeto' })).toBeVisible();
  await expect(page.getByLabel('Segmentos de itens')).toBeVisible();
  await expect(page.getByLabel('Compositor de consulta')).toBeVisible();

  const bugSegment = page.getByTestId('item-segment-4');
  await bugSegment.click();
  await expect(bugSegment).toHaveAttribute('aria-pressed', 'true');
  const selectedStyle = await bugSegment.evaluate((element) => ({
    overflow: getComputedStyle(element).overflow,
    indicatorTop: getComputedStyle(element, '::before').top,
    indicatorLeft: getComputedStyle(element, '::before').left,
  }));
  expect(selectedStyle).toEqual({ overflow: 'hidden', indicatorTop: '8px', indicatorLeft: '4px' });
  await expect(page).toHaveURL(/segment=4/);
  await expect(page.getByText('Bugs', { exact: true }).last()).toBeVisible();

  const search = page.getByPlaceholder('Título, número ou descrição');
  await search.fill('login');
  await page.getByRole('button', { name: 'Aplicar consulta' }).click();
  await expect(page).toHaveURL(/q=login/);

  await page.goBack();
  await expect(page).not.toHaveURL(/q=login/);
  await page.goForward();
  await expect(search).toHaveValue('login');

  const savedName = `Bugs login ${testInfo.project.name}`;
  await page.getByRole('button', { name: 'Salvar', exact: true }).click();
  await page.getByRole('textbox', { name: 'Nome da consulta' }).fill(savedName);
  await page.getByRole('button', { name: 'Salvar agora' }).click();
  await expect(page.getByRole('button', { name: savedName, exact: true })).toBeVisible();

  await page.getByRole('button', { name: `Excluir consulta ${savedName}` }).click();
  await expect(page.getByRole('button', { name: savedName, exact: true })).toHaveCount(0);
});
