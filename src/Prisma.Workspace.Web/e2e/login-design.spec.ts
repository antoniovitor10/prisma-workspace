import { test, expect } from '@playwright/test';

test.use({ storageState: { cookies: [], origins: [] } });

test('login Prisma preserva acesso, recuperação e layout responsivo', async ({ page }, testInfo) => {
  await page.goto('/login');
  await expect(page.getByRole('heading', { name: 'Acesse sua conta' })).toBeVisible();
  await expect(page.getByRole('button', { name: /Entrar/ }).first()).toBeVisible();
  if (testInfo.project.name === 'chromium-desktop') {
    const prism = page.getByAltText('Prisma facetado');
    await expect(prism).toBeVisible();
    await expect.poll(() => prism.evaluate(node => (node as HTMLImageElement).naturalWidth)).toBeGreaterThan(0);
  }
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBeTruthy();
  await page.screenshot({ path: `test-results/login-${testInfo.project.name}.png`, fullPage: true });
  await page.getByRole('button', { name: /Esqueceu sua senha/ }).click();
  await expect(page.getByRole('button', { name: /Enviar/ })).toBeVisible();
});
