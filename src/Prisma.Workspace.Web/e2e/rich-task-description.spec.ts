import type { Page } from '@playwright/test';
import { test, expect } from './fixtures/test';

const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5400';
async function projectId(page: Page) {
  await page.goto('/projects');
  const projects = await page.evaluate(async (baseUrl) => {
    const token = localStorage.getItem('prisma_workspace_token');
    const organizationId = localStorage.getItem('prisma_workspace_organization');
    const response = await fetch(`${baseUrl}/api/projects`, { headers: { Authorization: `Bearer ${token}`, 'X-Organization-Id': organizationId ?? '' } });
    return response.json() as Promise<Array<{ id: string; key: string; name: string }>>;
  }, apiUrl);
  const seed = projects.find((item) => item.key === 'INFRACOES') ?? projects[0];
  if (!seed) throw new Error('Nenhum projeto seed disponivel.');
  return seed.id;
}

test('rich description edits, expands, rejects unsafe URL and persists on reopen', async ({ page, authenticatedGoto }) => {
  await authenticatedGoto(`/projects/${await projectId(page)}/backlog`);
  await page.getByRole('button', { name: /^Abrir detalhes de / }).first().click();
  const dialog = page.locator('[role="dialog"][aria-describedby^="task-description-"]');
  const descriptionId = await dialog.getAttribute('aria-describedby');
  const editor = dialog.getByRole('region', { name: /Editor da descri/ });
  const textbox = editor.getByRole('textbox', { name: /Descri.*o da tarefa/ });
  await expect(textbox).toBeVisible();
  const originalText = await textbox.innerText();
  const editedText = `Descricao E2E ${test.info().project.name}-${Date.now()}`;
  await textbox.click();
  await page.keyboard.press('ControlOrMeta+A');
  await page.keyboard.type(editedText);
  await expect(textbox).toContainText(editedText);
  await expect(editor.getByRole('status')).toHaveText('Salvo', { timeout: 5000 });
  await editor.getByRole('button', { name: /Expandir editor/ }).click();
  await expect(editor.getByRole('button', { name: 'Sair da tela cheia' })).toBeVisible();
  const viewport = page.viewportSize();
  const expandedBox = await editor.boundingBox();
  expect(expandedBox?.width ?? 0).toBeGreaterThanOrEqual((viewport?.width ?? 0) - 2);
  expect(expandedBox?.height ?? 0).toBeGreaterThanOrEqual((viewport?.height ?? 0) - 2);
  await expect(textbox).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(editor.getByRole('button', { name: /Expandir editor/ })).toBeFocused();
  page.once('dialog', (prompt) => prompt.accept('javascript:alert(1)'));
  await editor.getByRole('button', { name: /Inserir imagem por endere/ }).click();
  await expect(editor.getByRole('status')).toContainText('http:// ou https://', { timeout: 5000 });
  await expect(editor.locator('img[src^="javascript:"]')).toHaveCount(0);
  await dialog.getByRole('button', { name: 'Fechar modal' }).click();
  await page.getByRole('button', { name: /^Abrir detalhes de / }).first().click();
  const reopened = page.locator(`[role="dialog"][aria-describedby="${descriptionId}"]`);
  const reopenedTextbox = reopened.getByRole('textbox', { name: /Descri.*o da tarefa/ });
  await expect(reopenedTextbox).toContainText(editedText);
  await reopenedTextbox.click();
  await page.keyboard.press('ControlOrMeta+A');
  if (originalText) await page.keyboard.type(originalText);
  await expect(reopened.getByRole('status')).toHaveText('Salvo', { timeout: 5000 });
});
