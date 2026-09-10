import { test, expect } from './fixtures/test';

test.describe('Smoke test', () => {
  test('usuario autenticado acessa o dashboard', async ({ page, authenticatedGoto }, testInfo) => {
    await authenticatedGoto('/');
    await expect(page).toHaveURL(/\/home$/);
    await expect(page.getByRole('heading', { name: 'O trabalho que importa, claro desde o primeiro olhar.' })).toBeVisible();
    if (testInfo.project.name === 'chromium-mobile') {
      await page.getByRole('button', { name: 'Abrir menu' }).click();
      await expect(page.getByRole('dialog', { name: 'Menu de navegação' }).getByRole('menuitem', { name: 'Início' })).toHaveAttribute('aria-current', 'page');
    } else {
      await expect(page.getByRole('navigation', { name: 'Navegação lateral' }).getByRole('link', { name: 'Início' })).toHaveAttribute('aria-current', 'page');
    }
  });

  test('usuario autenticado pode fazer logout', async ({ authenticatedGoto, request }) => {
    await authenticatedGoto('/');

    const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:5400';
    const response = await request.post(`${apiUrl}/api/auth/logout`);
    expect(response.status()).toBeLessThan(400);
  });
});

test.describe('Smoke público', () => {
  test.use({ storageState: { cookies: [], origins: [] } });

  test('pagina de login carrega e exibe o formulario', async ({ page }) => {
    await page.goto('/login');
    await expect(page.getByRole('heading', { name: 'Acesse sua conta' })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Entrar\s*→$/ })).toBeVisible();
  });
});
