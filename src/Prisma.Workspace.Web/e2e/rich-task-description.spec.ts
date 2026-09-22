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

test('rich description edits, expands, pastes an image and persists on reopen', async ({ page, authenticatedGoto }) => {
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
  const descriptionSaved=page.waitForResponse(response=>
    response.request().method()==='PUT'&&/\/api\/WorkItems\/[0-9a-f-]+$/i.test(response.url())&&response.ok(),
  );
  await textbox.evaluate(element=>{
    const transfer=new DataTransfer();
    transfer.items.add(new File([
      new Uint8Array([137,80,78,71,13,10,26,10,0,0,0,13,73,72,68,82,0,0,0,1,0,0,0,1,8,6,0,0,0,31,21,196,137,0,0,0,13,73,68,65,84,8,215,99,248,207,192,240,31,0,5,0,1,255,137,153,61,29,0,0,0,0,73,69,78,68,174,66,96,130]),
    ],`imagem-colada-${Date.now()}.png`,{type:'image/png'}));
    element.dispatchEvent(new ClipboardEvent('paste',{clipboardData:transfer,bubbles:true,cancelable:true}));
  });
  await expect(editor.getByRole('status')).toContainText('Imagem colada e anexada', { timeout: 10_000 });
  const pastedImage=editor.locator('img[data-attachment-id]');
  await expect(pastedImage).toBeVisible();
  const attachmentId=await pastedImage.getAttribute('data-attachment-id');
  expect(attachmentId).toBeTruthy();
  await descriptionSaved;
  await dialog.getByRole('button', { name: 'Fechar modal' }).click();
  await page.getByRole('button', { name: /^Abrir detalhes de / }).first().click();
  const reopened = page.locator(`[role="dialog"][aria-describedby="${descriptionId}"]`);
  const reopenedTextbox = reopened.getByRole('textbox', { name: /Descri.*o da tarefa/ });
  await expect(reopenedTextbox).toContainText(editedText);
  await expect(reopened.locator(`img[data-attachment-id="${attachmentId}"]`)).toBeVisible();
  await reopenedTextbox.click();
  await page.keyboard.press('ControlOrMeta+A');
  if (originalText) await page.keyboard.type(originalText);
  await expect(reopened.getByRole('status')).toHaveText('Salvo', { timeout: 5000 });
});
