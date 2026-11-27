import { randomUUID } from 'node:crypto';
import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { test, expect } from './fixtures/test';

const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5400';

// HU-0001 (T-0001): o botão "Criar projeto" mostra o loading padrão no mesmo clique, sem
// mudar de largura, não envia de novo (nem por Enter) e volta quando a resposta chega.
for (const mode of ['light', 'dark'] as const) {
  test(`HU-0001 criar projeto mostra o loading padrão no tema ${mode === 'light' ? 'claro' : 'escuro'}`, async ({ page, authenticatedGoto }, info) => {
    await page.addInitScript(theme => localStorage.setItem('prisma-theme', theme), mode);
    await authenticatedGoto('/projects?create=1');
    const dialog = page.locator('form').filter({ has: page.getByRole('heading', { name: 'Novo projeto' }) });
    const name = `HU-0001 loading ${randomUUID()}`;
    await dialog.getByPlaceholder('Nome do projeto').fill(name);
    await dialog.getByRole('combobox', { name: 'Natureza' }).selectOption({ index: 1 });
    await dialog.getByRole('combobox', { name: 'Tipo de Trabalho' }).selectOption({ index: 1 });

    let release!: () => void;
    const gate = new Promise<void>(done => { release = done; });
    let posts = 0;
    await page.route(url => url.pathname === '/api/projects', async route => {
      if (route.request().method() !== 'POST') return route.continue();
      posts += 1;
      await gate;
      await route.continue();
    });

    const button = dialog.getByRole('button', { name: 'Criar projeto', exact: true });
    const before = await button.boundingBox();
    await button.click();

    await expect(button).toHaveAttribute('aria-busy', 'true');
    await expect(button).toHaveText('Criar projeto');
    const spinner = button.locator('[data-action-spinner]');
    await expect(spinner).toBeVisible();
    await expect(spinner).toHaveAttribute('aria-hidden', 'true');
    await expect(page.getByRole('status').filter({ hasText: 'Criando projeto' })).toBeAttached();
    const during = await button.boundingBox();
    expect(Math.abs((during?.width ?? 0) - (before?.width ?? 0))).toBeLessThan(1);

    // Enter no formulário com o envio em andamento não manda outra requisição.
    await dialog.getByPlaceholder('Nome do projeto').press('Enter');
    await expect.poll(() => posts).toBe(1);

    const directory = resolve('../../.runlogs/hu-0001');
    mkdirSync(directory, { recursive: true });
    await button.screenshot({ path: `${directory}/criar-projeto-${mode}-${info.project.name}.png` });

    release();
    await expect(page).toHaveURL(/\/projects\/[0-9a-f-]{36}\/backlog/);
    expect(posts).toBe(1);

    const projectId = new URL(page.url()).pathname.split('/')[2];
    const archived = await page.evaluate(async ({ baseUrl, id }) => {
      const response = await fetch(`${baseUrl}/api/projects/${id}/archive`, {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${localStorage.getItem('prisma_workspace_token')}`,
          'X-Organization-Id': localStorage.getItem('prisma_workspace_organization') ?? '',
        },
      });
      return response.status;
    }, { baseUrl: apiUrl, id: projectId });
    expect(archived).toBe(204);
  });
}
