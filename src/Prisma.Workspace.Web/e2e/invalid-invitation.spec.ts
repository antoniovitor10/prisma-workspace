import { test, expect } from '@playwright/test';

test.use({ storageState: { cookies: [], origins: [] } });

for (const source of ['url', 'storage'] as const) {
  test(`convite indisponível em ${source} permite voltar ao login`, async ({ page }) => {
    await page.route('**/api/setup/status', route => route.fulfill({ json: { setupAvailable: false, initialized: true } }));
    await page.route('**/api/auth/invitation/preview', route => route.fulfill({
      status: 400, contentType: 'application/problem+json',
      body: JSON.stringify({ title: 'Convite expirado ou indisponível.', detail: 'Convite expirado ou indisponível.' }),
    }));
    if (source === 'storage') {
      await page.goto('/auth');
      await page.evaluate(() => localStorage.setItem('pendingInvite', 'invalid-test-invite'));
    }
    await page.goto(source === 'url' ? '/auth?invite=invalid-test-invite' : '/auth');
    await expect(page.getByRole('button', { name: 'Ir para o login' })).toBeVisible();
    await expect(page.getByText('Verificando convite...')).toHaveCount(0);
    await page.getByRole('button', { name: 'Ir para o login' }).click();
    await expect(page.getByRole('button', { name: 'Entrar →', exact: true })).toBeEnabled();
    await expect(page.getByRole('textbox', { name: 'E-mail corporativo' })).toBeEditable();
    expect(await page.evaluate(() => localStorage.getItem('pendingInvite'))).toBeNull();
    expect(new URL(page.url()).searchParams.has('invite')).toBe(false);
    await page.reload();
    await expect(page.getByRole('button', { name: 'Entrar →', exact: true })).toBeEnabled();
    await expect(page.getByText('Verificando convite...')).toHaveCount(0);
  });
}
