import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import type { Locator, Page, TestInfo } from '@playwright/test';
import { test, expect } from './fixtures/test';
import { withBoardFactory } from './fixtures/boards';

test.beforeEach(async ({ page }, info) => {
  await page.setViewportSize(info.project.name.includes('mobile')
    ? { width: 390, height: 844 } : { width: 1440, height: 900 });
});
async function capture(page: Page, info: TestInfo, defect: string) {
  const directory = resolve('../../.runlogs/oficina/screenshots');
  mkdirSync(directory, { recursive: true });
  await page.screenshot({ path: `${directory}/${defect}-${info.project.name}-${process.env.OFICINA_SCREENSHOT_PHASE ?? 'after'}.png`, scale: 'css' });
}
async function inViewport(control: Locator, page: Page) {
  await expect(control).toBeVisible();
  const box = await control.boundingBox();
  expect(box).not.toBeNull();
  expect(box!.x).toBeGreaterThanOrEqual(0);
  expect(box!.x + box!.width).toBeLessThanOrEqual(page.viewportSize()!.width);
}

for (const view of ['Kanban', 'Lista']) {
  test(`D13 ordenação e filtros cabem em ${view}`, async ({ page, request, authenticatedGoto }, info) => {
    await authenticatedGoto('/projects');
    await withBoardFactory(page, request, async f => {
      const board = await f.board();
      await page.goto(`/boards/${board.id}`);
      await page.getByRole('button', { name: view, exact: true }).click();
      const sort = page.getByRole('combobox', { name: 'Ordenar cartões', exact: true });
      await expect(sort).toBeVisible();
      await capture(page, info, `D13-${view}`);
      await page.getByRole('button', { name: /^Filtros(?: \(\d+\))?$/ }).click();
      const filters = page.getByRole('region', { name: 'Painel de filtros', exact: true });
      await expect(filters).toBeVisible();
      await capture(page, info, `D13-${view}-filtros`);
      await inViewport(sort, page);
      await inViewport(page.getByText(/de \d+ cartão\(ões\)/), page);
      await filters.getByText('Conteúdo do cartão', { exact: true }).click();
      await capture(page, info, `D13-${view}-conteudo`);
      for (const control of await filters.locator('input,select').all()) await inViewport(control, page);
    }, 'Oficina D13');
  });
}

test('D16 perfil do convite cabe na tela', async ({ page, authenticatedGoto }, info) => {
  await authenticatedGoto('/settings');
  const profile = page.getByRole('combobox', { name: 'Perfil do convite', exact: true });
  await expect(profile).toBeVisible();
  await profile.evaluate(el => window.scrollBy(0, el.getBoundingClientRect().top - innerHeight / 2));
  await capture(page, info, 'D16');
  await inViewport(profile, page);
});

test('D01 orientação do administrador não abre cadastro', async ({ page }, info) => {
  await page.goto('/projects');
  await page.evaluate(() => localStorage.clear());
  await page.context().clearCookies();
  await page.goto('/login');
  await page.getByRole('button', { name: 'Fale com o administrador', exact: true }).click();
  await capture(page, info, 'D01');
  await expect(page.getByRole('button', { name: /Criar conta|Cadastrar/i })).toHaveCount(0);
  await expect(page.getByText(/solicite.*convite|entre em contato.*administrador/i)).toBeVisible();
});

test('D03 portal inexistente orienta como continuar', async ({ page }, info) => {
  await page.goto('/portal/oficina-portal-inexistente');
  await expect(page.getByText(/carregando/i)).toHaveCount(0);
  await capture(page, info, 'D03');
  await expect(page.getByText(/confira.*endere|solicite.*link|entre em contato/i)).toBeVisible();
});

test('D05 Novo item fecha com Escape e devolve foco', async ({ page, authenticatedGoto }, info) => {
  await authenticatedGoto('/projects');
  const trigger = page.getByRole('button', { name: 'Novo item', exact: true });
  await trigger.click();
  await expect(page.getByRole('menu', { name: 'Selecione o tipo de item' })).toBeVisible();
  await page.keyboard.press('Escape');
  await capture(page, info, 'D05');
  await expect(page.getByRole('menu', { name: 'Selecione o tipo de item' })).toBeHidden();
  await expect(trigger).toBeFocused();
});

test('D06 campos de projeto têm rótulos visíveis', async ({ page, authenticatedGoto }, info) => {
  await authenticatedGoto('/projects');
  await page.getByRole('button', { name: /Novo projeto/i }).click();
  await expect(page.getByPlaceholder('Nome do projeto')).toBeVisible();
  await capture(page, info, 'D06-projeto');
  const projectName = page.getByRole('textbox', { name: 'Nome do projeto', exact: true });
  await expect(projectName).toBeVisible();
  expect(await projectName.evaluate(el => Array.from((el as HTMLInputElement).labels ?? []).some(label => label.textContent?.includes('Nome do projeto')))).toBe(true);
  const description = page.getByRole('textbox', { name: /Descrição do projeto/ });
  await expect(description).toBeVisible();
  expect(await description.evaluate(el => Array.from((el as HTMLTextAreaElement).labels ?? []).some(label => label.textContent?.includes('Descrição do projeto')))).toBe(true);
});

test('D06 campo de equipe tem rótulo visível', async ({ page, authenticatedGoto }, info) => {
  await authenticatedGoto('/teams');
  await expect(page.getByPlaceholder('Nome da nova equipe')).toBeVisible();
  await capture(page, info, 'D06-equipe');
  const teamName = page.getByRole('textbox', { name: 'Nome da nova equipe', exact: true });
  await expect(teamName).toBeVisible();
  expect(await teamName.evaluate(el => Array.from((el as HTMLInputElement).labels ?? []).some(label => label.textContent?.includes('Nome da nova equipe')))).toBe(true);
});

test('D17 Cancelar fecha formulário de conexão mesmo sem conexões', async ({ page, authenticatedGoto }, info) => {
  await authenticatedGoto('/settings');
  const add = page.getByRole('button', { name: 'Adicionar conexão', exact: true });
  const form = page.getByRole('form', { name: 'Cadastro de conexão IA', exact: true });
  if (await add.isVisible()) await add.click();
  await expect(form).toBeVisible();
  await form.getByRole('button', { name: 'Cancelar', exact: true }).click();
  await capture(page, info, 'D17');
  await expect(form).toBeHidden();
  await expect(add).toBeVisible();
});

test('D19 título editável da Wiki tem nome acessível', async ({ page, request, authenticatedGoto }, info) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const created = await f.api(`/api/projects/${f.projectId}/wiki/pages`, 'POST', { parentPageId: null, title: f.name('Wiki') });
    expect(created.status()).toBe(200);
    const wiki = await created.json();
    await page.goto(`/projects/${f.projectId}/wiki/${wiki.id}`);
    await expect(page.getByRole('button', { name: 'Histórico', exact: true })).toBeVisible();
    await capture(page, info, 'D19');
    await expect(page.getByRole('textbox', { name: 'Título da página', exact: true })).toBeVisible();
  }, 'Oficina D19');
});

test('D09 apontamento manual tem rótulos visíveis para Início e Fim', async ({ page, request, authenticatedGoto }, info) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const board = await f.board(); const stage = await f.stage(board.id); const item = await f.item(board.id, stage.id);
    await page.goto(`/projects/${f.projectId}/backlog?item=${item.id}`);
    const drawer = page.locator(`[role="dialog"][aria-describedby="task-description-${item.id}"]`);
    const start = drawer.getByLabel('Início do apontamento', { exact: true });
    await expect(start).toBeVisible();
    await start.scrollIntoViewIfNeeded();
    await capture(page, info, 'D09');
    await expect(drawer.getByText('Início', { exact: true })).toBeVisible();
    await expect(drawer.getByText('Fim', { exact: true })).toBeVisible();
    expect(await start.evaluate(el => Array.from((el as HTMLInputElement).labels ?? []).some(label => label.textContent?.trim() === 'Início'))).toBe(true);
    expect(await drawer.getByLabel('Fim do apontamento', { exact: true }).evaluate(el => Array.from((el as HTMLInputElement).labels ?? []).some(label => label.textContent?.trim() === 'Fim'))).toBe(true);
  }, 'Oficina D09');
});

test('D14 gráficos de relatórios têm nome acessível', async ({ page, request, authenticatedGoto }, info) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const board = await f.board(); const stage = await f.stage(board.id); await f.item(board.id, stage.id);
    const report = await f.api(`/api/reports/prepared?projectId=${f.projectId}`);
    expect(report.status()).toBe(200);
    const reportData = await report.json();
    expect(reportData.tasks.total).toBeGreaterThan(0);
    await page.goto(`/projects/${f.projectId}/reports`);
    await capture(page, info, 'D14-carregamento');
    const charts = page.locator('svg.recharts-surface');
    await expect(charts.first()).toBeVisible();
    await charts.first().scrollIntoViewIfNeeded();
    await capture(page, info, 'D14');
    expect(await charts.count()).toBeGreaterThan(0);
    for (const chart of await charts.all()) await expect(chart).toHaveAccessibleName(/\S/);
  }, 'Oficina D14');
});

test('D20 status da solicitação cabe na fila', async ({ page, request, authenticatedGoto }, info) => {
  await authenticatedGoto('/projects');
  await withBoardFactory(page, request, async f => {
    const board = await f.board();
    const slug = f.projectId;
    const portal = await f.api(`/api/projects/${f.projectId}/external-portal`, 'PUT', {
      boardId: board.id, publicSlug: slug, isEnabled: true, requiresAuthentication: false, accessModes: 1,
    });
    expect(portal.status()).toBe(200);
    const title = f.name('Solicitação com título longo para verificar o status da fila no celular');
    const submitted = await request.post(`${process.env.E2E_API_URL ?? 'http://127.0.0.1:5400'}/api/public/portals/${slug}/requests`, { data: {
      title, description: 'Cenário próprio sem dados externos.', requesterName: 'Pessoa de QA', requesterEmail: `oficina-${slug}@example.test`,
    } });
    expect(submitted.status()).toBe(201);
    const receipt = await submitted.json();
    const triaged = await f.api(`/api/external-requests/${encodeURIComponent(receipt.protocol)}/triage`, 'POST', { action: 4, message: 'Informe o complemento para este cenário próprio.' });
    expect(triaged.status()).toBe(200);
    await page.goto('/requests');
    const row = page.getByRole('button').filter({ hasText: title });
    await expect(row).toBeVisible();
    await capture(page, info, 'D20');
    await inViewport(row.getByText('Aguardando informações', { exact: true }), page);
  }, 'Oficina D20');
});
