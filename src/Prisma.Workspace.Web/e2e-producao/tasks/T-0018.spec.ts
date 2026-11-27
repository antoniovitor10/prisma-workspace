import { test, expect } from '../fixtures/projeto-teste';

/**
 * Spec de exemplo da T-0018 (CA-7): só leitura. Roda apenas com
 * `npm run e2e:producao -- T-0018` e confere que o quadro do projeto de teste carrega.
 */
test('T-0018: o quadro do projeto de teste carrega (só leitura)', async ({ page, producao }) => {
  await producao.abrirProjeto();
  await page.getByRole('navigation', { name: 'Alternar visão do projeto' }).getByRole('link', { name: 'Quadro', exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/projects/${producao.contexto.projetoId}/boards`));
  await expect(page.locator('[data-stage-id]').first()).toBeVisible();
  await producao.print('T-0018-quadro');
});
