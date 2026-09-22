import { test, expect } from './fixtures/test';

test('erro ao editar permanece visivel no topo e status usa o nome da coluna', async ({ page, authenticatedGoto, resolveSeedProject }) => {
  const project = await resolveSeedProject();
  await authenticatedGoto(`/projects/${project.id}/backlog`);
  await page.getByRole('button', { name: /^Abrir detalhes de / }).first().click();
  const dialog = page.locator('[role="dialog"][aria-describedby^="task-description-"]');
  const status = dialog.getByRole('combobox', { name: 'Status', exact: true });
  await expect(status.locator('option:checked')).not.toHaveText('Sem coluna');
  await expect(dialog.getByLabel('Coluna atual', { exact: true })).toHaveText(await status.locator('option:checked').innerText());
  await page.route('**/api/WorkItems/*', async route => {
    if (route.request().method() === 'PUT') {
      await route.fulfill({ status: 403, contentType: 'application/problem+json', body: JSON.stringify({ title: 'Acesso negado', detail: 'Você não tem permissão para editar esta tarefa.', status: 403 }) });
    } else await route.continue();
  });
  const title = dialog.getByRole('textbox', { name: 'Título', exact: true });
  const original = await title.inputValue();
  await title.fill(`${original} tentativa recusada`);
  await title.press('Tab');
  const alert = dialog.getByRole('alert');
  await expect(alert).toContainText('permissão');
  await expect(title).toHaveValue(original);
  const bounds = await alert.boundingBox();
  expect(bounds).not.toBeNull();
  expect(bounds!.y).toBeGreaterThanOrEqual(0);
  expect(bounds!.y + bounds!.height).toBeLessThan(page.viewportSize()!.height);
});

test('checkbox obrigatorio de campo personalizado mantem tamanho compacto', async ({ page, authenticatedGoto, resolveSeedProject }) => {
  const project = await resolveSeedProject();
  await authenticatedGoto(`/projects/${project.id}/settings?secao=classificacao`);
  // A categoria e acionada pela interface para nao depender da chave de URL.
  const category = page.getByRole('button', { name: /Classificação/ });
  if (await category.count()) await category.click();
  const checkbox = page.getByRole('checkbox', { name: 'Obrigatório', exact: true });
  await expect(checkbox).toBeVisible();
  const box = await checkbox.boundingBox();
  expect(box!.width).toBeLessThanOrEqual(20);
  expect(box!.height).toBeLessThanOrEqual(20);
});
