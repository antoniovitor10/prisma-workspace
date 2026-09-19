import { randomUUID } from 'node:crypto';
import { test, expect } from './fixtures/test';

const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5400';

test('convite cadastra, confirma e abre a organização; replay é recusado', async ({ page, browser, authenticatedGoto, request }, testInfo) => {
  await authenticatedGoto('/home');
  const email = `invite-${randomUUID()}@example.test`;
  const invitation = await page.evaluate(async ({ apiUrl, email }) => {
    const response = await fetch(`${apiUrl}/api/organizations/current/invitations`, {
      method: 'POST', headers: { 'Content-Type': 'application/json',
        Authorization: `Bearer ${localStorage.getItem('prisma_workspace_token')}`,
        'X-Organization-Id': localStorage.getItem('prisma_workspace_organization')! },
      body: JSON.stringify({ email, role: 7, expiresInDays: 7 }),
    });
    if (!response.ok) throw new Error(`Convite falhou: ${response.status}`);
    return response.json();
  }, { apiUrl, email });
  const guest = await browser.newContext({ storageState: { cookies: [], origins: [] },
    viewport: testInfo.project.use.viewport });
  const guestPage = await guest.newPage();
  const password = process.env.E2E_TEST_USER_PASSWORD!;
  try {
    await guestPage.goto(`${testInfo.project.use.baseURL}/?invite=${encodeURIComponent(invitation.token)}`);
    await expect(guestPage.getByLabel('E-mail corporativo')).toHaveValue(email);
    await expect(guestPage.getByLabel('E-mail corporativo')).toHaveAttribute('readonly', '');
    await guestPage.getByLabel('Nome completo').fill('Pessoa Convidada');
    await guestPage.getByLabel('Senha', { exact: true }).fill(password);
    await guestPage.getByLabel('Confirmar senha').fill(`${password}x`);
    await guestPage.getByRole('button', { name: 'Criar conta e entrar', exact: true }).click();
    await expect(guestPage.getByText('As senhas não coincidem.', { exact: true })).toBeVisible();
    await guestPage.getByLabel('Confirmar senha').fill(password);
    await guestPage.getByRole('button', { name: 'Criar conta e entrar', exact: true }).click();
    await expect(guestPage).toHaveURL(/\/home$/);
    await expect(guestPage.locator('main')).toBeVisible();
    expect(await guestPage.evaluate(() => localStorage.getItem('pendingInvite'))).toBeNull();
    const replay = await request.post(`${apiUrl}/api/auth/invitation/complete`, { data: {
      token: invitation.token, password, createAccount: false } });
    expect(replay.status()).toBe(400);
  } finally { await guest.close(); }
});

test('conta pendente usa a senha existente; concorrência consome convite uma vez', async ({ page, authenticatedGoto, playwright }, testInfo) => {
  test.skip(testInfo.project.name === 'chromium-mobile', 'Contrato HTTP sem dependência de viewport; UI coberta nos dois tamanhos.');
  await authenticatedGoto('/home');
  const email = `existing-${randomUUID()}@example.test`;
  const password = process.env.E2E_TEST_USER_PASSWORD!;
  const guest = await playwright.request.newContext();
  try {
    expect((await guest.post(`${apiUrl}/api/auth/register`, { data: { email, password } })).status()).toBe(202);
    const invitation = await page.evaluate(async ({ apiUrl, email }) => {
      const response = await fetch(`${apiUrl}/api/organizations/current/invitations`, {
        method: 'POST', headers: { 'Content-Type': 'application/json',
          Authorization: `Bearer ${localStorage.getItem('prisma_workspace_token')}`,
          'X-Organization-Id': localStorage.getItem('prisma_workspace_organization')! },
        body: JSON.stringify({ email, role: 7, expiresInDays: 7 }),
      });
      if (!response.ok) throw new Error(`Convite falhou: ${response.status}`);
      return response.json();
    }, { apiUrl, email });
    const preview = await guest.post(`${apiUrl}/api/auth/invitation/preview`, { data: { token: invitation.token } });
    expect((await preview.json()).accountExists).toBe(true);
    const complete = (suppliedPassword: string) => guest.post(`${apiUrl}/api/auth/invitation/complete`, {
      data: { token: invitation.token, password: suppliedPassword, createAccount: false } });
    expect((await complete(`${password}wrong`)).status()).toBe(400);
    const results = await Promise.all([complete(password), complete(password)]);
    expect(results.map(r => r.status()).sort()).toEqual([200, 400]);
    expect((await guest.post(`${apiUrl}/api/auth/login`, { data: { email, password } })).status()).toBe(200);
    expect((await guest.post(`${apiUrl}/api/auth/invitation/preview`, { data: { token: 'invalid' } })).status()).toBe(400);
  } finally { await guest.dispose(); }
});
