import type { Page } from '@playwright/test';
import { test, expect } from './fixtures/test';

const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5400';

async function appApi<T>(page: Page, path: string): Promise<T> {
  return page.evaluate(async ({ apiUrl: baseUrl, path: requestPath }) => {
    const token = localStorage.getItem('prisma_workspace_token');
    const organizationId = localStorage.getItem('prisma_workspace_organization');
    const response = await fetch(`${baseUrl}${requestPath}`, {
      headers: {
        Authorization: `Bearer ${token}`,
        'X-Organization-Id': organizationId ?? '',
      },
    });
    if (!response.ok) throw new Error(`${response.status} ${await response.text()}`);
    return response.json();
  }, { apiUrl, path });
}

async function resolveSeedProjectId(page: Page): Promise<string> {
  await page.goto('/projects');
  await page.waitForLoadState('domcontentloaded');
  type Project = { id: string; key: string; name: string };
  const projects = await appApi<Project[]>(page, '/api/projects');
  const seeded = projects.find((project) => project.key === 'INFRACOES')
    ?? projects.find((project) => /infrações|infracoes/i.test(project.name))
    ?? projects[0];
  if (!seeded) throw new Error('Nenhum projeto seed disponível para os testes E2E.');
  return seeded.id;
}

test('gaveta da tarefa integra seis abas, busca de dependência e regras Kanban', async ({ page, authenticatedGoto }) => {
  const projectId = await resolveSeedProjectId(page);
  await authenticatedGoto(`/projects/${projectId}/backlog`);

  const openDetails = page.getByRole('button', { name: /^Abrir detalhes de / }).first();
  await expect(openDetails).toBeVisible();
  await openDetails.click();

  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  for (const tab of ['Descrição', 'Comentários', 'Subtarefas', 'Anexos', 'Histórico', 'Grafo de estados']) {
    await expect(dialog.getByRole('button', { name: tab, exact: true })).toBeVisible();
  }

  const richEditor = dialog.getByRole('region', { name: 'Editor da descrição' });
  await expect(richEditor.getByRole('textbox', { name: 'Descrição da tarefa' })).toBeVisible();
  for (const control of ['Negrito', 'Inserir link', 'Checklist', 'Inserir imagem por endereço', 'Abrir anexos']) {
    await expect(richEditor.getByRole('button', { name: control })).toBeVisible();
  }
  await richEditor.getByRole('button', { name: 'Expandir editor' }).click();
  await expect(richEditor.getByRole('button', { name: 'Sair da tela cheia' })).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(richEditor.getByRole('button', { name: 'Expandir editor' })).toBeVisible();

  await expect(dialog.getByText('Story points', { exact: true })).toHaveCount(0);
  const dependencySearch = dialog.getByRole('combobox', { name: 'Buscar tarefa relacionada' });
  await expect(dependencySearch).toBeVisible();
  await expect(dependencySearch).toHaveAttribute('placeholder', /Código ou título/);

  await dialog.getByRole('button', { name: 'Comentários' }).click();
  await expect(dialog.getByRole('heading', { name: 'Comentários internos' })).toBeVisible();
  await dialog.getByRole('button', { name: 'Histórico' }).click();
  await expect(dialog.getByRole('heading', { name: 'Histórico imutável' })).toBeVisible();
  await dialog.getByRole('button', { name: 'Grafo de estados' }).click();
  await expect(dialog.getByRole('heading', { name: 'Grafo de estados' })).toBeVisible();
});
