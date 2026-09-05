import type { Page } from '@playwright/test';
import { test, expect } from './fixtures/test';

const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5400';

async function createOrganization(page: Page, name: string): Promise<{ id: string; name: string }> {
  return page.evaluate(async ({ baseUrl, organizationName }) => {
    const token = localStorage.getItem('prisma_workspace_token');
    const organizationId = localStorage.getItem('prisma_workspace_organization');
    const response = await fetch(`${baseUrl}/api/organizations`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${token}`,
        'X-Organization-Id': organizationId ?? '',
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ name: organizationName }),
    });
    if (!response.ok) throw new Error(`${response.status} ${await response.text()}`);
    return response.json();
  }, { baseUrl: apiUrl, organizationName: name });
}

test('troca de organização atualiza os projetos sem recarregar a página', async ({ page, authenticatedGoto }) => {
  await authenticatedGoto('/projects');
  const originalProject = page.getByRole('heading', { level: 2 }).first();
  await expect(originalProject).toBeVisible();
  const originalProjectName = await originalProject.textContent();

  const organizationName = `Organização E2E ${Date.now()}`;
  const created = await createOrganization(page, organizationName);

  // Recarrega apenas para a lista de organizações conhecer a fixture recém-criada.
  await page.reload();
  await page.waitForLoadState('domcontentloaded');
  await page.getByRole('combobox', { name: 'Selecionar organização' }).selectOption(created.id);

  await expect(page.getByText('Nenhum projeto disponível.')).toBeVisible();
  await expect.poll(() => page.evaluate(() => localStorage.getItem('prisma_workspace_organization'))).toBe(created.id);
  if (originalProjectName) await expect(page.getByText(originalProjectName, { exact: true })).toHaveCount(0);
});
