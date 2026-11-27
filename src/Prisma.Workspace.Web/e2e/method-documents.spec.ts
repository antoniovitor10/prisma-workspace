import { randomUUID } from 'node:crypto';
import type { APIRequestContext, Page, TestInfo } from '@playwright/test';
import { test, expect } from './fixtures/test';
import { Api, type MethodTask } from './fixtures/method-git';

/**
 * T-0024 (HU-0008): abas Documento, PRD e Corpo da task no detalhe, editor markdown, versões,
 * título e status do cabeçalho, etiqueta de status da HU no backlog e temas claro e escuro.
 *
 * Dados próprios por teste: projeto novo, método provisionado pela sessão humana (sem repositório nem
 * GitHub) e documentos gravados pela API de documentos. Roda em chromium-desktop e chromium-mobile.
 * O ambiente do CI liga MethodGit__Enabled=true (scripts/ci-e2e.sh).
 */

const EPIC = [
  '---',
  'id: EPIC-0001',
  'titulo: "Épico do E2E"',
  'status: aberta',
  '---',
  '',
  '# EPIC-0001 · Épico do E2E',
  '',
  'Objetivo do épico do E2E.',
  '',
].join('\n');

const HU = [
  '---',
  'id: HU-0001',
  'titulo: "Guardar a HU no Prisma"',
  'status: aberta',
  'epico: EPIC-0001',
  'criada_em: 2026-11-27',
  '---',
  '',
  '# HU-0001 · Guardar a HU no Prisma',
  '',
  '## Critérios de aceite',
  '',
  '| CA | Descrição |',
  '|---|---|',
  '| CA-1 | A HU aparece formatada |',
  '',
].join('\n');

const PRD = '# PRD da HU-0001\n\nPrimeira versão do PRD.\n';

const TASK_BODY = (id: string) => [
  '---',
  `id: ${id}`,
  `titulo: "Corpo da ${id}"`,
  'hu: HU-0001',
  '---',
  '',
  `# ${id} · Corpo da task`,
  '',
  '## Objetivo',
  '',
  'Texto do corpo da task.',
  '',
].join('\n');

interface MethodDocumentDto { key: string; version: number; workItemId: string; title: string | null; status: string | null }

interface Scenario {
  api: Api;
  projectId: string;
  epic: MethodDocumentDto;
  hu: MethodDocumentDto;
  prd: MethodDocumentDto;
}

const docPath = (projectId: string, key: string) =>
  `/api/projects/${projectId}/method/documents/${key.split('/').map(encodeURIComponent).join('/')}`;

/** Grava o documento: cria (baseVersion 0) ou, se já existir, grava sobre a versão atual. */
async function putDocument(api: Api, projectId: string, key: string, content: string, filePath?: string) {
  const current = await api.get(docPath(projectId, key));
  const baseVersion = current.status() === 404 ? 0 : (await current.json() as MethodDocumentDto).version;
  return api.json<MethodDocumentDto>(api.put(docPath(projectId, key), { content, baseVersion, ...(filePath ? { filePath } : {}) }));
}

async function sessionApi(page: Page, request: APIRequestContext) {
  const session = await page.evaluate(() => ({
    token: localStorage.getItem('prisma_workspace_token'),
    organizationId: localStorage.getItem('prisma_workspace_organization'),
  }));
  if (!session.token || !session.organizationId) throw new Error('Sessão E2E autenticada não disponível.');
  return new Api(request, { Authorization: `Bearer ${session.token}`, 'X-Organization-Id': session.organizationId });
}

/** Projeto novo com o método provisionado e Épico, História (com PRD) gravados pela API de documentos. */
async function createScenario(page: Page, request: APIRequestContext): Promise<Scenario> {
  const api = await sessionApi(page, request);
  const created = await api.json<string | { id: string }>(api.post('/api/projects', {
    name: `T-0024 documentos ${randomUUID()}`, description: 'Fixture exclusiva E2E dos documentos do método.', nature: 1, workType: 1,
  }), 201);
  const projectId = typeof created === 'string' ? created : created.id;
  await api.json(api.post(`/api/projects/${projectId}/method/provision`, { autonomyLevel: 0 }));
  await api.json(api.put(`/api/projects/${projectId}/method/settings`, { enabled: true, autonomyLevel: 0 }));
  const epic = await putDocument(api, projectId, 'EPIC-0001', EPIC, 'docs/trabalho/EPIC-0001-e2e/epico.md');
  const hu = await putDocument(api, projectId, 'HU-0001', HU, 'docs/trabalho/HU-0001-e2e/hu.md');
  const prd = await putDocument(api, projectId, 'HU-0001/prd', PRD, 'docs/trabalho/HU-0001-e2e/prd.md');
  return { api, projectId, epic, hu, prd };
}

async function createMethodTask(scenario: Scenario, id: string) {
  const task = await scenario.api.json<MethodTask>(scenario.api.post(`/api/projects/${scenario.projectId}/method/tasks`, {
    id, title: `Corpo da ${id}`, hu: 'HU-0001', filePath: `docs/trabalho/HU-0001-e2e/tasks/${id}.md`,
    covers: ['CA-1'], risk: 'normal', status: 'backlog',
  }));
  await putDocument(scenario.api, scenario.projectId, id, TASK_BODY(id));
  return task;
}

async function archive(scenario: Scenario | undefined) {
  if (!scenario) return;
  const response = await scenario.api.post(`/api/projects/${scenario.projectId}/archive`);
  expect(response.status(), 'arquivar o projeto da fixture').toBe(204);
}

async function openBacklog(page: Page, projectId: string) {
  await page.goto(`/projects/${projectId}/backlog`);
  await expect(page.getByRole('textbox', { name: 'Pesquisar backlog', exact: true })).toBeVisible();
}

/** Abre o detalhe pela linha do item no backlog e confere o título do item no cabeçalho do detalhe. */
async function openItem(page: Page, workItemId: string, title: string | RegExp) {
  await page.locator(`[data-work-item-id="${workItemId}"]`).getByRole('button', { name: /^Abrir detalhes de / }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByRole('heading', { name: title, ...(typeof title === 'string' ? { exact: true } : {}) })).toBeVisible();
  return dialog;
}

async function useTheme(page: Page, mode: 'light' | 'dark') {
  const current = await page.locator('html').getAttribute('data-theme');
  if (current !== mode) {
    await page.getByRole('button', { name: mode === 'dark' ? 'Ativar modo escuro' : 'Ativar modo claro', exact: true }).click();
  }
  await expect(page.locator('html')).toHaveAttribute('data-theme', mode);
}

async function screenshot(page: Page, testInfo: TestInfo, name: string) {
  await testInfo.attach(`${name}-${testInfo.project.name}`, { body: await page.screenshot(), contentType: 'image/png' });
}

const huTitle = 'HU-0001 · Guardar a HU no Prisma';
const epicTitle = 'EPIC-0001 · Épico do E2E';

test.describe('documentos do método no detalhe (T-0024)', () => {
  test.beforeEach(async ({ authenticatedGoto }) => {
    test.setTimeout(120_000);
    await authenticatedGoto('/projects');
  });

  for (const mode of ['light', 'dark'] as const) {
    test(`HU: aba Documento formatada, etiqueta no backlog e status pelo campo (tema ${mode === 'light' ? 'claro' : 'escuro'}) (CA-1, CA-13)`, async ({ page, request }, testInfo) => {
      let scenario: Scenario | undefined;
      try {
        scenario = await createScenario(page, request);
        await openBacklog(page, scenario.projectId);
        await useTheme(page, mode);

        const row = page.locator(`[data-work-item-id="${scenario.hu.workItemId}"]`);
        await expect(row.getByText('Status da HU: Aberta', { exact: true })).toBeVisible();

        const dialog = await openItem(page, scenario.hu.workItemId, huTitle);
        await dialog.getByRole('button', { name: 'Documento', exact: true }).click();
        const panel = dialog.getByRole('region', { name: 'documento da HU', exact: true });
        await expect(panel.getByRole('heading', { level: 1, name: huTitle })).toBeVisible();
        await expect(panel.getByRole('table')).toBeVisible();
        await expect(panel.getByRole('cell', { name: 'A HU aparece formatada' })).toBeVisible();
        const strip = panel.getByLabel('Metadados do documento');
        await expect(strip).toContainText('HU-0001');
        await expect(strip).toContainText('Aberta');
        await expect(panel.getByText('titulo:', { exact: false })).toHaveCount(0);
        await expect(dialog.getByRole('button', { name: 'PRD', exact: true })).toBeVisible();
        await screenshot(page, testInfo, `hu-documento-${mode}`);

        await panel.getByRole('combobox', { name: 'Status da HU' }).selectOption('planejada');
        await expect(panel.getByText(/^Versão 2 · gravada por/)).toBeVisible();

        await page.reload();
        await expect(page.getByRole('dialog').getByRole('heading', { name: huTitle, exact: true })).toBeVisible();
        await page.getByRole('dialog').getByRole('button', { name: 'Documento', exact: true }).click();
        const reloaded = page.getByRole('dialog').getByRole('region', { name: 'documento da HU', exact: true });
        await expect(reloaded.getByRole('combobox', { name: 'Status da HU' })).toHaveValue('planejada');
        await expect(reloaded.getByText(/^Versão 2 · gravada por/)).toBeVisible();
        await expect(reloaded.getByLabel('Metadados do documento')).toContainText('Planejada');
        await expect(page.locator('html')).toHaveAttribute('data-theme', mode);
        await screenshot(page, testInfo, `hu-status-${mode}`);

        await page.getByRole('button', { name: 'Fechar modal', exact: true }).click();
        await expect(row.getByText('Status da HU: Planejada', { exact: true })).toBeVisible();
      } finally {
        await archive(scenario);
      }
    });

    test(`PRD: editar com pré-visualização, salvar, versões e versão anterior (tema ${mode === 'light' ? 'claro' : 'escuro'}) (CA-3, CA-13)`, async ({ page, request }, testInfo) => {
      let scenario: Scenario | undefined;
      try {
        scenario = await createScenario(page, request);
        await openBacklog(page, scenario.projectId);
        await useTheme(page, mode);
        const dialog = await openItem(page, scenario.hu.workItemId, huTitle);
        await dialog.getByRole('button', { name: 'PRD', exact: true }).click();
        const panel = dialog.getByRole('region', { name: 'PRD', exact: true });
        await expect(panel.getByRole('heading', { name: 'PRD da HU-0001' })).toBeVisible();

        await panel.getByRole('button', { name: 'Editar PRD', exact: true }).click();
        const textarea = panel.getByRole('textbox', { name: 'Texto do PRD em markdown' });
        await expect(textarea).toHaveValue(PRD);
        const typed = `${PRD}\nParágrafo escrito pelo E2E.\n\n| Campo | Valor |\n|---|---|\n| status | novo |\n`;
        await textarea.fill(typed);
        await textarea.focus();
        if (testInfo.project.name.includes('mobile')) {
          // No celular a barra Salvar/Cancelar fica fixa embaixo, visível com o campo em foco.
          await expect(panel.getByRole('button', { name: 'Salvar PRD', exact: true })).toBeInViewport();
          await expect(panel.getByRole('button', { name: 'Cancelar edição', exact: true })).toBeInViewport();
        }

        await panel.getByRole('tab', { name: 'Pré-visualizar' }).click();
        const preview = panel.getByRole('region', { name: 'Pré-visualização do PRD', exact: true });
        await expect(preview.getByRole('cell', { name: 'novo', exact: true })).toBeVisible();
        await expect(preview.getByText('Parágrafo escrito pelo E2E.')).toBeVisible();
        await screenshot(page, testInfo, `prd-preview-${mode}`);

        await panel.getByRole('button', { name: 'Salvar PRD', exact: true }).click();
        await expect(panel.getByRole('textbox', { name: 'Texto do PRD em markdown' })).toHaveCount(0);
        await expect(panel.getByText(/^Versão 2 · gravada por/)).toBeVisible();

        const saved = await scenario.api.json<{ content: string; version: number }>(scenario.api.get(docPath(scenario.projectId, 'HU-0001/prd')));
        expect(saved.content).toBe(typed);
        expect(saved.version).toBe(2);

        await page.reload();
        await page.getByRole('dialog').getByRole('button', { name: 'PRD', exact: true }).click();
        const reloaded = page.getByRole('dialog').getByRole('region', { name: 'PRD', exact: true });
        await expect(reloaded.getByText('Parágrafo escrito pelo E2E.')).toBeVisible();
        const versions = reloaded.getByRole('region', { name: 'Versões do documento', exact: true });
        await expect(versions.getByText('Versão 2 (atual)', { exact: true })).toBeVisible();
        await versions.getByRole('button', { name: 'Ver versão 1', exact: true }).click();
        const old = reloaded.getByRole('region', { name: 'Texto da versão 1', exact: true });
        await expect(old.getByText('Primeira versão do PRD.')).toBeVisible();
        await expect(old.getByText('Parágrafo escrito pelo E2E.')).toHaveCount(0);
        await expect(old.getByRole('textbox')).toHaveCount(0);
        await screenshot(page, testInfo, `prd-versoes-${mode}`);
      } finally {
        await archive(scenario);
      }
    });
  }

  test('Épico: aba Documento e título pelo campo próprio (CA-2)', async ({ page, request }) => {
    let scenario: Scenario | undefined;
    try {
      scenario = await createScenario(page, request);
      await openBacklog(page, scenario.projectId);
      const dialog = await openItem(page, scenario.epic.workItemId, epicTitle);
      await dialog.getByRole('button', { name: 'Documento', exact: true }).click();
      const panel = dialog.getByRole('region', { name: 'documento do épico', exact: true });
      await expect(panel.getByRole('heading', { level: 1, name: epicTitle })).toBeVisible();
      await expect(panel.getByRole('combobox', { name: 'Status do épico' }).getByRole('option'))
        .toHaveText(['Aberta', 'Planejada', 'Concluída', 'Cancelada']);

      const title = panel.getByRole('textbox', { name: 'Título do épico' });
      await expect(title).toHaveValue('Épico do E2E');
      await title.fill('Épico renomeado pelo E2E');
      await title.press('Enter');
      await expect(panel.getByText(/^Versão 2 · gravada por/)).toBeVisible();

      await page.reload();
      const renamed = 'EPIC-0001 · Épico renomeado pelo E2E';
      await expect(page.getByRole('dialog').getByRole('heading', { name: renamed, exact: true })).toBeVisible();
      await page.getByRole('dialog').getByRole('button', { name: 'Documento', exact: true }).click();
      await expect(page.getByRole('dialog').getByRole('textbox', { name: 'Título do épico' })).toHaveValue('Épico renomeado pelo E2E');
      const saved = await scenario.api.json<{ content: string }>(scenario.api.get(docPath(scenario.projectId, 'EPIC-0001')));
      expect(saved.content).toContain('titulo: "Épico renomeado pelo E2E"');
      expect(saved.content).toContain('# EPIC-0001 · Épico do E2E');
    } finally {
      await archive(scenario);
    }
  });

  test('Task em em-desenvolvimento fica só leitura e conflito de versão em duas abas (CA-8)', async ({ page, context, request }) => {
    let scenario: Scenario | undefined;
    try {
      scenario = await createScenario(page, request);
      const editable = await createMethodTask(scenario, 'T-0001');
      const locked = await createMethodTask(scenario, 'T-0002');
      // A sessão humana autoriza o início (o token do agente não pode, princípio 3).
      const moved = await scenario.api.post(`/api/projects/${scenario.projectId}/method/tasks/T-0002/move`,
        { newStatus: 'em-desenvolvimento', reason: 'humano autoriza no E2E' });
      expect(moved.ok(), `mover T-0002: ${moved.status()}`).toBeTruthy();

      await openBacklog(page, scenario.projectId);
      let dialog = await openItem(page, locked.workItemId, /^T-0002/);
      await dialog.getByRole('button', { name: 'Corpo da task', exact: true }).click();
      let panel = dialog.getByRole('region', { name: 'corpo da task', exact: true });
      await expect(panel.getByText('O agente está executando esta task; o corpo volta a ser editável quando ela sair de em-desenvolvimento')).toBeVisible();
      await expect(panel.getByRole('heading', { level: 1, name: 'T-0002 · Corpo da task' })).toBeVisible();
      await expect(panel.getByRole('button', { name: /^Editar/ })).toHaveCount(0);
      await page.getByRole('button', { name: 'Fechar modal', exact: true }).click();

      dialog = await openItem(page, editable.workItemId, /^T-0001/);
      await dialog.getByRole('button', { name: 'Corpo da task', exact: true }).click();
      panel = dialog.getByRole('region', { name: 'corpo da task', exact: true });
      await expect(panel.getByRole('textbox', { name: 'Título da task' })).toHaveValue('Corpo da T-0001');

      // Duas abas com o mesmo documento: a segunda abre o editor antes de a primeira salvar.
      const second = await context.newPage();
      await second.goto(page.url());
      const secondDialog = second.getByRole('dialog');
      await expect(secondDialog.getByRole('heading', { name: /^T-0001/ })).toBeVisible();
      await secondDialog.getByRole('button', { name: 'Corpo da task', exact: true }).click();
      const secondPanel = secondDialog.getByRole('region', { name: 'corpo da task', exact: true });
      await secondPanel.getByRole('button', { name: 'Editar corpo da task', exact: true }).click();

      await panel.getByRole('button', { name: 'Editar corpo da task', exact: true }).click();
      await panel.getByRole('textbox', { name: 'Texto do corpo da task em markdown' }).fill('\n# T-0001\n\nGravado pela primeira aba.\n');
      await panel.getByRole('button', { name: 'Salvar corpo da task', exact: true }).click();
      await expect(panel.getByText(/^Versão 2 · gravada por/)).toBeVisible();

      const typed = '\n# T-0001\n\nTexto da segunda aba, que não pode sobrescrever.\n';
      await secondPanel.getByRole('textbox', { name: 'Texto do corpo da task em markdown' }).fill(typed);
      await secondPanel.getByRole('button', { name: 'Salvar corpo da task', exact: true }).click();
      await expect(secondPanel.getByRole('alert')).toHaveText('Este documento mudou desde que você abriu. Recarregue para ver a versão nova');
      await expect(secondPanel.getByRole('textbox', { name: 'Texto do corpo da task em markdown' })).toHaveValue(typed);

      const saved = await scenario.api.json<{ content: string; version: number }>(scenario.api.get(docPath(scenario.projectId, 'T-0001')));
      expect(saved.version).toBe(2);
      expect(saved.content).toContain('Gravado pela primeira aba.');
      expect(saved.content).not.toContain('Texto da segunda aba');
      await second.close();
    } finally {
      await archive(scenario);
    }
  });
});
