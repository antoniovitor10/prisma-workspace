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

/** Aceita respostas completas iniciadas depois do documento novo, excluindo refetch antigo/cancelado. */
export async function navigateWithApi(page: Page, paths: string[], navigate: () => Promise<Response | null>) {
  const pending = new Map(paths.map(path => {
    let resolve!: (response: Response) => void; let reject!: (error: unknown) => void;
    const promise = new Promise<Response>((yes, no) => { resolve = yes; reject = no; });
    return [path.toLowerCase(), { promise, resolve, reject }] as const;
  }));
  const completed: Request[] = []; let documentStart: number | undefined;
  const rejectAll = (error: unknown) => { for (const value of pending.values()) value.reject(error); };
  const accept = async (request: Request) => {
    if (documentStart === undefined || request.timing().startTime < documentStart) return;
    const response = await request.response(); expect(response).not.toBeNull();
    expect(response!.status(), `GET ${new URL(request.url()).pathname}`).toBe(200);
    pending.get(new URL(request.url()).pathname.toLowerCase())!.resolve(response!);
  };
  const listener = (request: Request) => {
    if (request.method() !== 'GET' || !pending.has(new URL(request.url()).pathname.toLowerCase())) return;
    completed.push(request);
    if (documentStart !== undefined) void accept(request).catch(rejectAll);
  };
  page.on('requestfinished', listener);
  try {
    const document = await navigate(); expect(document).not.toBeNull(); expect(document!.status()).toBe(200);
    const timing = document!.request().timing(); expect(timing.responseStart).toBeGreaterThanOrEqual(0);
    documentStart = timing.startTime + timing.responseStart;
    for (const request of completed) void accept(request).catch(rejectAll);
    return await Promise.all(paths.map(path => pending.get(path.toLowerCase())!.promise));
  } finally { page.off('requestfinished', listener); }
}
