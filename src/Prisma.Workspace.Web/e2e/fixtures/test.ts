import { expect, test as base, type APIRequestContext, type Page } from '@playwright/test';

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
const cleanupActions = new WeakMap<APIRequestContext, Array<() => Promise<void>>>();
const httpMethods = new Set(['fetch', 'get', 'post', 'put', 'patch', 'delete', 'head']);

/** Registra recursos antes de executá-los, mantendo a limpeza fora do corpo do teste. */
export function cleanupFor(request: APIRequestContext) {
  const actions = cleanupActions.get(request);
  if (!actions) throw new Error('A factory requer a fixture request de e2e/fixtures/test.ts.');
  return (action: () => Promise<void>) => { actions.push(action); };
}

function safeTransportError(error: unknown) {
  // O call log de transporte do Playwright inclui Authorization e cookies.
  // Preservar o motivo sem propagar cabeçalhos, credenciais ou a causa bruta.
  const reason = (error instanceof Error ? error.message : String(error)).split('\n')[0]
    .replace(/\beyJ[A-Za-z0-9_-]*\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\b/g, '[token removido]')
    .replace(/prisma_refresh=[^\s;]+/g, 'prisma_refresh=[removido]');
  return new Error(reason);
}

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
    if (!response.ok) throw new Error(`GET ${requestPath} falhou com status ${response.status}.`);
    return response.json();
  }, { baseUrl: apiUrl, requestPath: path });
}

export const test = base.extend<TestFixtures>({
  request: [async ({ request }, provideRequest, testInfo) => {
    const safeRequest = new Proxy(request, {
      get(target, property) {
        const member = Reflect.get(target, property);
        if (typeof member !== 'function') return member;
        if (typeof property !== 'string' || !httpMethods.has(property)) return member.bind(target);
        return async (...args: unknown[]) => {
          try { return await member.apply(target, args); }
          catch (error) { throw safeTransportError(error); }
        };
      },
    });
    const actions: Array<() => Promise<void>> = [];
    cleanupActions.set(safeRequest, actions);
    try { await provideRequest(safeRequest); } finally {
      const startedAt = Date.now();
      const errors: unknown[] = [];
      const count = actions.length;
      for (const action of actions.reverse()) {
        try { await action(); } catch (error) { errors.push(safeTransportError(error)); }
      }
      cleanupActions.delete(safeRequest);
      if (count) testInfo.annotations.push({ type: 'fixture-cleanup',
        description: `${count} ações; ${Date.now() - startedAt} ms; ${errors.length} falhas` });
      if (errors.length) throw new AggregateError(errors, 'Falha na limpeza dos dados próprios E2E.');
    }
    // A fixture tem prazo próprio; o corpo do cenário continua com 30s.
  }, { scope: 'test', timeout: 20_000 }],
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
