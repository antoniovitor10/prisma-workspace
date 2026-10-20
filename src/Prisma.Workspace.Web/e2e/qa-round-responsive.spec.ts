import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import type { Locator, Page, TestInfo } from '@playwright/test';
import { test, expect } from './fixtures/test';
import { withBoardFactory } from './fixtures/boards';

// D99: regressões da rodada, medidas em viewports exatos para comparar as capturas.
test.beforeEach(async ({ page }, info) => {
  await page.setViewportSize(info.project.name.includes('mobile')
    ? { width: 390, height: 844 } : { width: 1440, height: 900 });
});
async function capture(page: Page, info: TestInfo, defect: string) {
  const phase = process.env.OFICINA_SCREENSHOT_PHASE ?? 'after';
  const directory = resolve('../../.runlogs/oficina/screenshots');
  mkdirSync(directory, { recursive: true });
  await page.screenshot({ path: `${directory}/${defect}-${info.project.name}-${phase}.png`, scale: 'css' });
}
async function reachable(locator: Locator, page: Page) {
  await expect(locator).toBeVisible();
  const original = await locator.boundingBox();
  expect(original).not.toBeNull();
  expect(original!.x).toBeGreaterThanOrEqual(0);
  expect(original!.x + original!.width).toBeLessThanOrEqual(page.viewportSize()!.width);
  await locator.scrollIntoViewIfNeeded();
  const box = await locator.evaluate(el => {
    const r = el.getBoundingClientRect();
    const points = [r.left + 4, r.left + r.width / 2, r.right - 4];
    const visible = points.every(x => {
      const hit = document.elementFromPoint(x, r.y + r.height / 2);
      return !!hit && (hit === el || el.contains(hit));
    });
    return { left: r.left, right: r.right, visible };
  });
  expect(box.left).toBeGreaterThanOrEqual(0);
  expect(box.right).toBeLessThanOrEqual(page.viewportSize()!.width);
  expect(box.visible).toBe(true);
}

test('D04 seletor de organização permanece alcançável no cabeçalho', async ({ page, request, authenticatedGoto }, info) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const created = await f.api('/api/organizations', 'POST', { name: f.name('Organização com nome longo que deve permanecer alcançável no cabeçalho'), slug: f.projectId });
    expect(created.status()).toBe(201);
    const { id: organizationId } = await created.json();
    expect(organizationId).toEqual(expect.any(String));
    await page.reload({ waitUntil: 'domcontentloaded' });
    const picker = page.getByRole('combobox', { name: 'Selecionar organização', exact: true });
    await expect(picker).toBeVisible();
    await picker.selectOption(organizationId);
    await expect(picker).toHaveValue(organizationId);
    await capture(page, info, 'D04');
    await reachable(picker, page);
  }, 'Oficina D04');
});

test('D15 formulário de permissão mantém Aplicar dentro da tela', async ({ page, request, authenticatedGoto }, info) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    await page.goto('/settings', { waitUntil: 'domcontentloaded' });
    const form = page.getByRole('heading', { name: 'Permissões específicas', exact: true }).locator('../..').locator('form');
    const apply = form.getByRole('button', { name: 'Aplicar', exact: true });
    await expect(apply).toBeVisible();
    // Rola apenas na vertical: scrollIntoView horizontal poderia mascarar o corte original.
    await apply.evaluate(el => window.scrollBy(0, el.getBoundingClientRect().top - innerHeight / 2));
    await capture(page, info, 'D15');
    await reachable(apply, page);
    // A regressão verifica o controle real; os testes de permissões cobrem persistência.
    expect(f.projectId).toBeTruthy();
  }, 'Oficina D15');
});

test('D18 histórico da Wiki permanece alcançável com título longo', async ({ page, request, authenticatedGoto }, info) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const created = await f.api(`/api/projects/${f.projectId}/wiki/pages`, 'POST', {
      parentPageId: null, title: f.name('Wiki com título longo para conferir responsividade do histórico'),
    });
    expect(created.status()).toBe(200);
    const wiki = await created.json();
    await page.goto(`/projects/${f.projectId}/wiki/${wiki.id}`, { waitUntil: 'domcontentloaded' });
    const history = page.getByRole('button', { name: 'Histórico', exact: true });
    await expect(history).toBeVisible();
    await capture(page, info, 'D18');
    await reachable(history, page);
    await history.click();
    await expect(page.getByText('Histórico de versões', { exact: true })).toBeVisible();
  }, 'Oficina D18');
});
