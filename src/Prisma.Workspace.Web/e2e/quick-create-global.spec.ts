import { test, expect } from './fixtures/test';

/**
 * Regressão: o botão "Novo item" da barra superior enviava `position: Date.now()`,
 * em milissegundos. CreateWorkItemCommandValidator recusa posição >= 999.999.999.999,
 * teto que Date.now() ultrapassa desde 2001 — então toda criação rápida respondia
 * 400 validation_error e o modal ficava aberto sem explicar o motivo.
 */
test('criar item pela barra superior conclui e fecha o modal', async ({ page, authenticatedGoto }) => {
  const titulo = `QA criação rápida ${Date.now()}`;
  const falhas: string[] = [];
  page.on('response', (r) => {
    if (r.status() >= 400 && r.url().includes('/api/WorkItems')) {
      falhas.push(`${r.status()} ${r.request().method()} ${r.url()}`);
    }
  });

  await authenticatedGoto('/home');
  await page.getByRole('button', { name: /novo item/i }).first().click();
  await page.getByRole('menuitem', { name: 'Bug', exact: true }).click();

  const dialogo = page.getByRole('dialog');
  await expect(dialogo).toBeVisible();
  await expect(dialogo.getByRole('combobox', { name: 'Tipo', exact: true })).toHaveValue('4');

  await dialogo.getByRole('textbox', { name: 'Título da tarefa' }).fill(titulo);

  const criar = dialogo.getByRole('button', { name: 'Criar item' });
  await expect(criar).toBeEnabled();
  await criar.click();

  // O modal só fecha quando a criação é aceita pela API.
  await expect(dialogo).toBeHidden();
  expect(falhas, `A API recusou a criação rápida: ${falhas.join(' ; ')}`).toEqual([]);
});
