import { test, expect } from './fixtures/test';

/**
 * Regressão a11y/resíduos (Agente C):
 * - selects da Empresa sem nome acessível
 * - Equipes estourava ~509px em viewport 393
 */
test.describe('A11y selects e Equipes responsivo', () => {
  test('Empresa: selects de cliente têm aria-label', async ({ page, authenticatedGoto }) => {
    await authenticatedGoto('/company');
    await expect(page.getByRole('heading', { name: 'Empresa' })).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('button', { name: 'Projetos' })).toBeVisible();

    // Exige pelo menos um combobox nomeado — falha no front antigo (select sem aria-label).
    const clientSelects = page.getByRole('combobox', { name: /Cliente do projeto/i });
    await expect(
      clientSelects.first(),
      'Select de cliente do projeto precisa de aria-label acessível',
    ).toBeVisible({ timeout: 15000 });

    const unlabeled = await page.evaluate(() => {
      const selects = [...document.querySelectorAll('select')];
      return selects.filter((el) => {
        const label = (el.getAttribute('aria-label') || '').trim();
        const id = el.getAttribute('id');
        const byFor = id
          ? document.querySelector(`label[for="${CSS.escape(id)}"]`)
          : null;
        const wrapped = el.closest('label');
        return !label && !byFor && !wrapped;
      }).map((el) => el.outerHTML.slice(0, 120));
    });

    expect(unlabeled, `Selects sem rótulo na Empresa: ${unlabeled.join(' | ')}`).toEqual([]);
  });

  test('Equipes: sem overflow horizontal no mobile', async ({ page, authenticatedGoto }, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium-mobile', 'Viewport Pixel 5 (mobile)');

    await authenticatedGoto('/teams');
    await expect(page.getByRole('heading', { name: /Equipes/i })).toBeVisible({ timeout: 15000 });

    const overflow = await page.evaluate(() => ({
      scrollWidth: document.documentElement.scrollWidth,
      clientWidth: document.documentElement.clientWidth,
    }));

    expect(
      overflow.scrollWidth,
      `Equipes não deve rolar na horizontal (scrollWidth=${overflow.scrollWidth}, clientWidth=${overflow.clientWidth})`,
    ).toBeLessThanOrEqual(overflow.clientWidth);
  });
});
