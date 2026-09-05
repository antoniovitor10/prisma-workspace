import { expect, test as base, type Page } from '@playwright/test';

type SeedBoard = { id: string; name: string; teamId?: string };

export type SeedProject = {
  id: string;
  key: string;
  name: string;
  defaultBoardId?: string;
  boards: SeedBoard[];
};

type TestFixtures = {
  authenticatedGoto: (path: string) => Promise<void>;
  resolveSeedProject: () => Promise<SeedProject>;
};

const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5400';

export async function authenticatedApiGet<T>(page: Page, path: string): Promise<T> {
  return page.evaluate(async ({ baseUrl, requestPath }) => {
    const token = localStorage.getItem('prisma_workspace_token');
    const organizationId = localStorage.getItem('prisma_workspace_organization');
    if (!token || !organizationId) throw new Error('Sessão E2E autenticada não disponível.');
    const response = await fetch(`${baseUrl}${requestPath}`, {
      headers: {
        Authorization: `Bearer ${token}`,
        'X-Organization-Id': organizationId,
      },
    });
    if (!response.ok) throw new Error(`GET ${requestPath} falhou com status ${response.status()}.`);
    return response.json();
  }, { baseUrl: apiUrl, requestPath: path });
}

export const test = base.extend<TestFixtures>({
  authenticatedGoto: async ({ page }, provideAuthenticatedGoto) => {
    await provideAuthenticatedGoto(async (appPath: string) => {
      const response = await page.goto(appPath, { waitUntil: 'domcontentloaded' });
      expect(response, `Navegação para ${appPath} não retornou documento.`).not.toBeNull();
      await expect(page).not.toHaveURL(/\/login(?:[/?#]|$)/);
      await expect(page.locator('main')).toBeVisible();
    });
  },
  resolveSeedProject: async ({ page }, provideResolveSeedProject) => {
    await provideResolveSeedProject(async () => {
      if (page.url() === 'about:blank') {
        const response = await page.goto('/projects', { waitUntil: 'domcontentloaded' });
        expect(response, 'A página de projetos não retornou documento.').not.toBeNull();
      }
      await expect(page).not.toHaveURL(/\/login(?:[/?#]|$)/);
      const projects = await authenticatedApiGet<SeedProject[]>(page, '/api/projects');
      const summary = projects.find((project) => project.key === 'INFRACOES')
        ?? projects.find((project) => /infrações|infracoes/i.test(project.name))
        ?? projects.find((project) => project.boards.length > 0);
      if (!summary) throw new Error('Nenhum projeto seed com quadro disponível para leitura E2E.');
      const project = await authenticatedApiGet<SeedProject>(page, `/api/projects/${summary.id}`);
      if (!project.boards.length) throw new Error(`Projeto seed ${project.key} não possui quadro.`);
      return project;
    });
  },
});

export { expect } from '@playwright/test';
