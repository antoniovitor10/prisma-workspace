import { randomUUID } from 'node:crypto';
import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import type { APIRequestContext, Page, TestInfo } from '@playwright/test';
import { test, expect } from './fixtures/test';

const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5400';
async function newcomer(request: APIRequestContext) {
  const email = `oficina-org-${randomUUID()}@example.test`;
  const password = process.env.E2E_TEST_USER_PASSWORD;
  if (!password) throw new Error('Configure a senha de teste local.');
  const registered = await request.post(`${apiUrl}/api/auth/register`, {
    data: { email, password, fullName: 'Pessoa Oficina Organização' },
  });
  expect(registered.status()).toBe(202);
  const account = await registered.json();
  expect(account.developmentToken).toEqual(expect.any(String));
  expect((await request.post(`${apiUrl}/api/auth/confirm-email`, {
    data: { userId: account.userId, token: account.developmentToken },
  })).status()).toBe(204);
  const login = await request.post(`${apiUrl}/api/auth/login`, { data: { email, password } });
  expect(login.status()).toBe(200);
  const token = (await login.json()).accessToken;
  expect(token).toEqual(expect.any(String));
  return token as string;
}
async function capture(page: Page, info: TestInfo, label: string) {
  const directory = resolve('../../.runlogs/oficina/auto-qa');
  mkdirSync(directory, { recursive: true });
  await page.screenshot({ path: `${directory}/${label}-${info.project.name}-${Date.now()}.png` });
}
async function session(page: Page, accessToken: string) {
  await page.goto('/login', { waitUntil: 'domcontentloaded' });
  await page.evaluate(token => {
    localStorage.setItem('prisma_workspace_token', token);
    localStorage.removeItem('prisma_workspace_organization');
  }, accessToken);
}

test.beforeEach(async ({ page }, info) => {
  await page.setViewportSize(info.project.name.includes('mobile')
    ? { width: 390, height: 844 } : { width: 1440, height: 900 });
});

test('dois pedidos simultâneos do usuário novo criam somente uma primeira organização', async ({ request }) => {
  const token = await newcomer(request);
  const headers = { Authorization: `Bearer ${token}` };
  const responses = await Promise.all(['A', 'B'].map(suffix => request.post(`${apiUrl}/api/organizations`, {
    headers, data: { name: `Oficina concorrência ${suffix} ${randomUUID()}` },
  })));
  expect(responses.map(response => response.status()).sort()).toEqual([201, 403]);
  const organizations = await request.get(`${apiUrl}/api/organizations`, { headers });
  expect(organizations.status()).toBe(200);
  expect(await organizations.json()).toHaveLength(1);
  const creation = await request.get(`${apiUrl}/api/organizations/creation`, { headers });
  expect(await creation.json()).toEqual({ canCreate: false });
});

test('onboarding cria o ambiente e deixa de oferecer nova organização ao usuário comum', async ({ page, request }) => {
  await session(page, await newcomer(request));
  await page.goto('/home', { waitUntil: 'domcontentloaded' });
  await expect(page.getByRole('heading', { name: 'Crie sua organização' })).toBeVisible();
  await page.getByLabel('Nome da organização').fill(`Oficina onboarding ${randomUUID()}`);
  const created = page.waitForResponse(response => response.request().method() === 'POST'
    && new URL(response.url()).pathname === '/api/organizations');
  await page.getByRole('button', { name: 'Criar ambiente' }).click();
  expect((await created).status()).toBe(201);
  await page.getByRole('button', { name: 'Menu da conta' }).click();
  await expect(page.getByRole('menuitem', { name: 'Nova organização' })).toHaveCount(0);
});

test('diálogo de organização contém teclado e restaura foco na conta ao fechar', async ({ page, authenticatedGoto }, info) => {
  await authenticatedGoto('/home');
  const opener = page.getByRole('button', { name: 'Menu da conta' });
  await opener.click();
  await page.getByRole('menuitem', { name: 'Nova organização' }).click();
  const input = page.getByLabel('Nome da organização');
  await expect(input).toBeFocused();
  await capture(page, info, 'organization-dialog');
  await page.keyboard.press('Shift+Tab');
  await expect(page.getByRole('button', { name: 'Cancelar', exact: true })).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(input).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog', { name: 'Nova organização' })).toBeHidden();
  await expect(opener).toBeFocused();
});

test('membro desativado recebe orientação e não cria outra primeira organização', async ({ page, request, authenticatedGoto }, info) => {
  await authenticatedGoto('/projects');
  const admin = await page.evaluate(() => ({
    token: localStorage.getItem('prisma_workspace_token')!,
    organizationId: localStorage.getItem('prisma_workspace_organization')!,
  }));
  const adminHeaders = { Authorization: `Bearer ${admin.token}`, 'X-Organization-Id': admin.organizationId };
  const password = process.env.E2E_TEST_USER_PASSWORD;
  if (!password) throw new Error('Configure a senha local de teste.');
  const invitation = await request.post(`${apiUrl}/api/organizations/current/invitations`, {
    headers: adminHeaders, data: { email: `oficina-inactive-${randomUUID()}@example.test`, role: 7, expiresInDays: 1 },
  });
  expect(invitation.status()).toBe(200);
  const completed = await request.post(`${apiUrl}/api/auth/invitation/complete`, { data: {
    token: (await invitation.json()).token, createAccount: true, fullName: 'Pessoa Oficina Inativa',
    password, confirmPassword: password,
  } });
  expect(completed.status()).toBe(200);
  const person = await completed.json();
  const claims = JSON.parse(Buffer.from(person.accessToken.split('.')[1], 'base64url').toString());
  const id: string = claims.sub ?? claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'];
  expect(id).toEqual(expect.any(String));
  const deactivate = () => request.put(`${apiUrl}/api/organizations/current/members/${id}`, {
    headers: adminHeaders, data: { role: 7, isActive: false },
  });
  try {
    expect((await deactivate()).status()).toBe(204);
    const headers = { Authorization: `Bearer ${person.accessToken}` };
    const creation = await request.get(`${apiUrl}/api/organizations/creation`, { headers });
    expect(await creation.json()).toEqual({ canCreate: false });
    expect((await request.post(`${apiUrl}/api/organizations`, {
      headers, data: { name: `Oficina criação recusada ${randomUUID()}` },
    })).status()).toBe(403);
    await session(page, person.accessToken);
    await page.goto('/home', { waitUntil: 'domcontentloaded' });
    await expect(page.getByRole('heading', { name: 'Acesso à organização indisponível' })).toBeVisible();
    await capture(page, info, 'organization-inactive');
    await expect(page.getByRole('button', { name: 'Criar ambiente' })).toHaveCount(0);
  } finally {
    expect((await deactivate()).status()).toBe(204);
  }
});
