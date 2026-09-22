import { test, expect } from './fixtures/test';

/**
 * Regressão: "Imprimir" chamava window.print() sem nenhuma folha de impressão, então
 * saía a página inteira — cabeçalho do produto, navegação, filtros e botões — em vez
 * do relatório. O PDF, por sua vez, ocupava a folha A4 sem margem nem identificação.
 */
test('impressão do relatório sai apenas com o conteúdo do relatório',
  async ({ page, authenticatedGoto }) => {
    await authenticatedGoto('/reports');
    await expect(page.locator('[data-print-root]')).toBeVisible();

    await page.emulateMedia({ media: 'print' });

    // Seletores de CSS, e não de papel: elementos ocultos saem da árvore de
    // acessibilidade, então getByRole não os encontraria para inspecionar.
    await expect(page.locator('header').first()).toHaveCSS('visibility', 'hidden');
    await expect(page.locator('nav').first()).toHaveCSS('visibility', 'hidden');

    // E o relatório precisa continuar visível.
    await expect(page.locator('[data-print-root]')).toHaveCSS('visibility', 'visible');

    await page.emulateMedia({ media: 'screen' });
  });
