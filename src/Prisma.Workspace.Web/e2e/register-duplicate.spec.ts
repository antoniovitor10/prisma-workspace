import { test, expect } from '@playwright/test';

test.use({ storageState: { cookies: [], origins: [] } });

test('cadastro duplicado mostra orientação em português', async ({ page }) => {
  const email = process.env.E2E_TEST_USER_EMAIL;
  const password = process.env.E2E_TEST_USER_PASSWORD;
  if (!email || !password) throw new Error('Credenciais E2E obrigatórias.');

  await page.goto('/login');
  await page.getByRole('button', { name: 'registre-se', exact: true }).click();
  await page.getByLabel('E-mail corporativo').fill(email);
  await page.getByLabel('Senha', { exact: true }).fill(password);
  const responsePromise = page.waitForResponse(response =>
    response.url().endsWith('/api/auth/register') && response.request().method() === 'POST');
  await page.getByRole('button', { name: 'Criar conta', exact: true }).click();
  expect((await responsePromise).status()).toBe(400);
  await expect(page.getByText('Este e-mail já está cadastrado. Entre na sua conta.', { exact: true })).toBeVisible();
});
