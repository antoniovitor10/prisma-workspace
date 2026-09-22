import { test, expect } from '@playwright/test';

test.use({ storageState: { cookies: [], origins: [] } });

test('regras de senha retornam mensagens em português sem criar conta', async ({ request }) => {
  const response = await request.post(`${process.env.E2E_API_URL ?? 'http://127.0.0.1:5400'}/api/auth/register`, {
    data: { email: `validation-${Date.now()}@example.test`, password: 'abcdefghij' },
  });
  expect(response.status()).toBe(400);
  const body = JSON.stringify(await response.json());
  expect(body).toContain('caractere especial');
  expect(body).toContain('maiúscula');
  expect(body).not.toContain('Passwords must');
});

test('aviso de cadastro permanece legível no tema escuro', async ({ page }) => {
  await page.goto('/auth');
  await page.getByRole('button', { name: 'registre-se' }).click();
  await page.getByRole('button', { name: 'Alternar tema' }).click();
  await expect(page.getByRole('heading', { name: 'Criar sua conta' })).toBeVisible();
  const notice = page.getByText(/A senha deve conter ao menos/);
  await expect(notice).toBeVisible();
  const colors = await notice.evaluate(node => {
    const style = getComputedStyle(node.closest('[role="alert"]') ?? node);
    return { color: style.color, background: style.backgroundColor };
  });
  expect(colors.color).not.toBe(colors.background);
});
