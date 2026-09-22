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

test('modal amplo, título editável por duplo clique e vários responsáveis pelo cabeçalho', async ({ page, authenticatedGoto }) => {
  const projectId = await resolveSeedProjectId(page);
  await authenticatedGoto(`/projects/${projectId}/backlog`);

  await page.getByRole('button', { name: /^Abrir detalhes de / }).first().click();
  // O popover de responsáveis também é role=dialog; o seletor abaixo isola o modal da tarefa.
  const dialog = page.locator('[role="dialog"][aria-describedby^="task-description-"]');
  await expect(dialog).toBeVisible();

  if (test.info().project.name === 'chromium-desktop') {
    const box = await dialog.boundingBox();
    expect(box?.width ?? 0).toBeGreaterThan(1000);
  }

  // título: duplo clique -> input -> Enter salva; depois restaura o valor original
  const titleHeading = dialog.getByTitle('Clique duas vezes para editar o título');
  await expect(titleHeading).toBeVisible();
  const originalTitle = (await titleHeading.textContent())?.trim() ?? '';
  expect(originalTitle.length).toBeGreaterThan(0);
  await titleHeading.dblclick();
  const titleInput = dialog.getByRole('textbox', { name: 'Editar título' });
  await expect(titleInput).toBeVisible();
  await expect(titleInput).toHaveValue(originalTitle);
  const editedTitle = `${originalTitle} (E2E)`;
  await titleInput.fill(editedTitle);
  await titleInput.press('Enter');
  await expect(dialog.getByRole('textbox', { name: 'Editar título' })).toHaveCount(0);
  await expect(dialog.getByTitle('Clique duas vezes para editar o título')).toHaveText(editedTitle);
  await expect(dialog.getByRole('textbox', { name: 'Título' })).toHaveValue(editedTitle);
  // Escape cancela a edição sem fechar o modal
  await dialog.getByTitle('Clique duas vezes para editar o título').dblclick();
  await dialog.getByRole('textbox', { name: 'Editar título' }).fill('descartar');
  await page.keyboard.press('Escape');
  await expect(dialog).toBeVisible();
  await expect(dialog.getByTitle('Clique duas vezes para editar o título')).toHaveText(editedTitle);
  await dialog.getByTitle('Clique duas vezes para editar o título').dblclick();
  await dialog.getByRole('textbox', { name: 'Editar título' }).fill(originalTitle);
  await dialog.getByRole('textbox', { name: 'Editar título' }).press('Enter');
  await expect(dialog.getByTitle('Clique duas vezes para editar o título')).toHaveText(originalTitle);

  // responsáveis: o chip do cabeçalho abre um popover que permite adicionar mais de uma pessoa
  const chip = dialog.getByTitle('Clique para adicionar ou remover responsáveis');
  await expect(chip).toHaveText(/\d+ respons/);
  const countBefore = Number(((await chip.textContent()) ?? '').match(/(\d+)/)?.[1] ?? '0');
  await chip.click();
  const popover = dialog.getByRole('dialog', { name: 'Gerenciar responsáveis' });
  await expect(popover).toBeVisible();
  await expect(popover.getByRole('textbox', { name: 'Buscar pessoa para adicionar' })).toBeFocused();

  const candidates = popover.getByRole('button', { name: /^Adicionar .* como responsável$/ });
  const available = await candidates.count();
  if (available === 0) {
    await expect(popover.getByText('Todas as pessoas já estão alocadas.')).toBeVisible();
    return;
  }
  // Nomes podem repetir no seed; o id da pessoa identifica a linha a remover.
  const removeButtonFor = (userId: string) => popover.locator(`[data-user-id="${userId}"]`).getByRole('button', { name: /^Remover .* dos responsáveis$/ });
  const firstId = (await candidates.first().getAttribute('data-user-id')) ?? '';
  expect(firstId).not.toBe('');
  await candidates.first().click();
  await expect(chip).toContainText(`${countBefore + 1} respons`);
  await expect(popover).toBeVisible();
  const removeFirst = removeButtonFor(firstId);
  await expect(removeFirst).toBeVisible();

  if (available > 1) {
    const secondId = (await candidates.first().getAttribute('data-user-id')) ?? '';
    expect(secondId).not.toBe(firstId);
    await candidates.first().click();
    await expect(chip).toContainText(`${countBefore + 2} respons`);
    await removeButtonFor(secondId).click();
    await expect(chip).toContainText(`${countBefore + 1} respons`);
  }
  await removeFirst.click();
  await expect(chip).toContainText(`${countBefore} respons`);
  await page.keyboard.press('Escape');
  await expect(popover).toHaveCount(0);
  await expect(dialog).toBeVisible();
});

test('subtarefa abre o detalhe completo com planejamento, anexos e horas', async ({ page, authenticatedGoto }) => {
  const projectId = await resolveSeedProjectId(page);
  await authenticatedGoto('/projects/' + projectId + '/backlog');
  await page.getByRole('button', { name: /^Abrir detalhes de / }).first().click();
  const dialog = page.locator("[role=dialog][aria-describedby^=task-description-]");
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole('combobox', { name: 'Sprint da tarefa' })).toBeVisible();
  await expect(dialog.getByRole('combobox', { name: 'Sprint da tarefa' })).toContainText('Product Backlog');

  await dialog.getByRole('button', { name: 'Subtarefas' }).click();
  const title = 'Subtarefa E2E ' + Date.now();
  await dialog.getByPlaceholder('Nova subtarefa (somente título)').fill(title);
  await dialog.getByRole('button', { name: 'Adicionar' }).click();

  await expect(dialog.getByTitle('Clique duas vezes para editar o título')).toHaveText(title);
  await expect(dialog.getByRole('button', { name: 'Anexos' ,exact:true})).toBeVisible();
  await expect(dialog.getByText('Apontamento de horas', { exact: true })).toBeVisible();
});
