import { expect, test } from '@playwright/test';

test.describe('Configuração inicial', () => {
  test.use({ storageState: { cookies: [], origins: [] } });

  test('orienta o primeiro administrador e protege o token', async ({ page }) => {
    await page.route('**/api/setup/status', (route) => route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ initialized: false, setupAvailable: true }),
    }));

    let submittedBody = '';
    let submittedToken = '';
    await page.route('**/api/setup', async (route) => {
      const request = route.request();
      submittedBody = request.postData() ?? '';
      submittedToken = request.headers()['x-prisma-setup-token'] ?? '';
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({ initialized: true }),
      });
    });

    await page.goto('/setup');
    await expect(page.getByRole('heading', { name: 'Prepare seu Prisma WorkSpace' })).toBeVisible();
    await page.getByLabel('Código de instalação').fill('codigo-temporario');
    await page.getByLabel('Nome completo').fill('Ana Silva');
    await page.getByLabel('E-mail').fill('ana@example.com');
    await page.getByLabel(/^Senha/).fill('SenhaForte#123');
    await page.getByLabel('Nome da organização').fill('Equipe Prisma');
    await page.getByLabel('Identificador (slug)').fill('equipe-prisma');
    await page.getByRole('button', { name: /Concluir configuração/ }).click();

    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByRole('heading', { name: 'Acesse sua conta' })).toBeVisible();
    expect(submittedToken).toBe('codigo-temporario');
    expect(submittedBody).not.toContain('codigo-temporario');
  });

  test('conclui uma única vez contra SQL Server real', async ({ request }) => {
    const setupToken = process.env.E2E_SETUP_TOKEN;
    test.skip(!setupToken, 'Executado somente na fase E2E com banco limpo e setup habilitado.');

    const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5400';
    const administratorEmail = 'first-admin@prisma.example.invalid';
    const administratorPassword = 'SetupE2E#2026Strong';
    const data = {
      administratorName: 'Primeiro Administrador',
      administratorEmail,
      administratorPassword,
      organizationName: 'Prisma E2E',
      organizationSlug: 'prisma-e2e',
    };
    const submit = () => request.post(`${apiUrl}/api/setup`, {
      headers: { 'X-Prisma-Setup-Token': setupToken! },
      data,
    });

    const responses = await Promise.all([submit(), submit()]);
    expect(responses.map((response) => response.status()).sort()).toEqual([201, 409]);

    const status = await request.get(`${apiUrl}/api/setup/status`);
    expect(await status.json()).toEqual({ initialized: true, setupAvailable: false });

    const login = await request.post(`${apiUrl}/api/auth/login`, {
      data: { email: administratorEmail, password: administratorPassword },
    });
    expect(login.status()).toBe(200);
    expect((await login.json()).accessToken).toBeTruthy();
  });
});
