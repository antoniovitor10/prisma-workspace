import type { Page, Request, Response } from '@playwright/test';
import { test as base, expect } from './test';
export { expect };

// Estes lotes verificam comportamento com API real. Tipografia externa fica no QA visual:
// a folha do Google falha no servidor e pode atrasar a execução dos scripts da página.
export const test = base.extend({
  page: async ({ page }, providePage) => {
    await page.route('https://fonts.googleapis.com/**', route => route.fulfill({
      status: 200, contentType: 'text/css', body: '',
    }));
    await providePage(page);
  },
});

/** Aguarda respostas completas de requisições iniciadas pelo documento novo. */
export async function navigateWithApi(page: Page, paths: string[], navigate: () => Promise<Response | null>) {
  const pending = new Map(paths.map(path => {
    let resolve!: (response: Response) => void; let reject!: (error: unknown) => void;
    const promise = new Promise<Response>((yes, no) => { resolve = yes; reject = no; });
    return [path.toLowerCase(), { promise, resolve, reject }] as const;
  }));
  const requests = new Set<Request>(); let navigationStarted = false;
  const rejectAll = (error: unknown) => { for (const value of pending.values()) value.reject(error); };
  const accept = async (request: Request) => {
    if (!requests.has(request)) return;
    const response = await request.response(); expect(response).not.toBeNull();
    expect(response!.status(), `GET ${new URL(request.url()).pathname}`).toBe(200);
    pending.get(new URL(request.url()).pathname.toLowerCase())!.resolve(response!);
  };
  const listener = (request: Request) => {
    if (request.method() !== 'GET' || !pending.has(new URL(request.url()).pathname.toLowerCase())) return;
    void accept(request).catch(rejectAll);
  };
  const started = (request: Request) => {
    if (request.isNavigationRequest() && request.frame() === page.mainFrame()) navigationStarted = true;
    if (navigationStarted && request.method() === 'GET' && (pending.has(new URL(request.url()).pathname.toLowerCase())
      || request.isNavigationRequest() || request.resourceType() === 'script')) requests.add(request);
  };
  const failed = (request: Request) => {
    if (requests.has(request)) rejectAll(new Error(`GET ${new URL(request.url()).pathname}: ${request.failure()?.errorText ?? 'falha de transporte'}`));
  };
  page.on('requestfailed', failed);
  page.on('request', started);
  page.on('requestfinished', listener);
  try {
    const [, responses] = await Promise.all([
      navigate().then(document => {
        expect(document).not.toBeNull(); expect(document!.status()).toBe(200);
        expect(navigationStarted).toBe(true);
      }),
      Promise.all(paths.map(path => pending.get(path.toLowerCase())!.promise)),
    ]);
    return responses;
  } finally { page.off('requestfailed', failed); page.off('request', started); page.off('requestfinished', listener); }
}
