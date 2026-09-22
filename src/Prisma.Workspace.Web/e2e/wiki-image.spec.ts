import * as fs from 'node:fs';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';
import { test, expect, authenticatedApiGet, type SeedProject } from './fixtures/test';

const currentDir = path.dirname(fileURLToPath(import.meta.url));
const tinyPng = path.join(currentDir, '..', '.artifacts', 'e2e-wiki-tiny.png');

/** PNG 1×1 mínimo (vermelho). */
const TINY_PNG_BASE64 =
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==';

function ensureTinyPng() {
  const dir = path.dirname(tinyPng);
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
  if (!fs.existsSync(tinyPng)) fs.writeFileSync(tinyPng, Buffer.from(TINY_PNG_BASE64, 'base64'));
}

async function resolveDemoProject(page: Parameters<typeof authenticatedApiGet>[0]): Promise<SeedProject> {
  const projects = await authenticatedApiGet<SeedProject[]>(page, '/api/projects');
  const project = projects.find((item) => item.key === 'DEMO')
    ?? projects.find((item) => item.boards.length > 0);
  if (!project) throw new Error('Nenhum projeto disponível para o teste da wiki.');
  return project;
}

test.describe('wiki imagem TipTap', () => {
  test.beforeAll(() => ensureTinyPng());

  test('insere data URI e a imagem sobrevive ao reload do editor', async ({ page, authenticatedGoto }) => {
    test.setTimeout(90_000);
    await authenticatedGoto('/projects');
    // O seletor de organização só aparece a partir da segunda organização; sem checar a
    // presença, a espera do selectOption consome o timeout do cenário inteiro.
    const seletorOrg = page.getByLabel('Selecionar organização');
    if (await seletorOrg.count() > 0) {
      await seletorOrg.selectOption({ label: 'Prisma Demo' }).catch(() => undefined);
      await page.waitForTimeout(500);
    }

    const project = await resolveDemoProject(page);
    const tree = await authenticatedApiGet<Array<{ id: string; title: string }>>(
      page,
      `/api/projects/${project.id}/wiki/tree`,
    );
    let pageId = tree[0]?.id;
    if (!pageId) {
      const created = await page.evaluate(async ({ apiUrl, projectId }) => {
        const token = localStorage.getItem('prisma_workspace_token');
        const organizationId = localStorage.getItem('prisma_workspace_organization');
        const response = await fetch(`${apiUrl}/api/projects/${projectId}/wiki/pages`, {
          method: 'POST',
          headers: {
            Authorization: `Bearer ${token}`,
            'X-Organization-Id': organizationId ?? '',
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({ parentPageId: null, title: 'E2E Wiki Image' }),
        });
        if (!response.ok) throw new Error(`create wiki page ${response.status}`);
        return response.json() as Promise<{ id: string }>;
      }, { apiUrl: process.env.E2E_API_URL ?? 'http://127.0.0.1:5400', projectId: project.id });
      pageId = created.id;
    }

    await authenticatedGoto(`/projects/${project.id}/wiki/${pageId}`);
    await expect(page.getByRole('button', { name: 'Inserir imagem' })).toBeVisible({ timeout: 15_000 });

    const fileChooserPromise = page.waitForEvent('filechooser');
    await page.getByRole('button', { name: 'Inserir imagem' }).click();
    const chooser = await fileChooserPromise;
    await chooser.setFiles(tinyPng);

    await expect.poll(async () => page.locator('.ProseMirror img').count(), { timeout: 10_000 }).toBeGreaterThan(0);
    await expect(page.locator('.ProseMirror img').first()).toHaveAttribute('src', /^data:image\//);

    // O salvamento do editor é por debounce, e a imagem vai embutida como data URI —
    // carga útil grande. Isolado isto conclui em ~4s; com a suíte inteira concorrendo pela
    // máquina, 20s não bastavam e o cenário falhava sem que nada estivesse errado no
    // produto. O que a asserção prova continua igual: a imagem persistiu no servidor.
    await expect.poll(async () => {
      const saved = await authenticatedApiGet<{ contentHtml: string }>(
        page,
        `/api/projects/${project.id}/wiki/pages/${pageId}`,
      );
      return /data:image/.test(saved.contentHtml) ? 'ok' : 'pending';
    }, { timeout: 60_000, intervals: [500, 1_000, 2_000] }).toBe('ok');

    await page.reload({ waitUntil: 'domcontentloaded' });
    await expect(page.getByRole('button', { name: 'Inserir imagem' })).toBeVisible({ timeout: 15_000 });
    // Regressão TipTap: allowBase64=false descarta img data URI no parseHTML.
    await expect(page.locator('.ProseMirror img').first()).toHaveAttribute('src', /^data:image\//, { timeout: 10_000 });
  });
});
