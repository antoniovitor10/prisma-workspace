import { test, expect } from './fixtures/test';

/**
 * HU-0003, CA-7: gera os prints dos artigos da central de ajuda com os dados de exemplo do seed
 * de demonstração (contas `*.example.invalid`, projeto "Produto Demo"), sem dado pessoal.
 *
 * Os arquivos vão para `src/features/help/articles/<assunto>/imagens/`. Para refazer os prints
 * depois de mudar uma tela, rode só este arquivo contra uma imagem nova e commite as imagens:
 *
 *   bash scripts/ci-e2e.sh <imagem> e2e/help-screenshots.spec.ts
 *
 * O script monta o repositório no contêiner do navegador, então os prints caem direto na pasta
 * dos artigos. Na suíte completa do CI o teste roda igual (confere que as telas abrem), mas os
 * arquivos ficam no checkout descartável.
 */
const articles = 'src/features/help/articles';

test('gera os prints dos artigos com dados de exemplo (CA-7)', async ({ page, authenticatedGoto, resolveSeedProject }, testInfo) => {
  test.skip(testInfo.project.name !== 'chromium-desktop', 'Os prints usam só a largura de desktop.');
  await page.setViewportSize({ width: 1366, height: 900 });
  await authenticatedGoto('/projects');
  const project = await resolveSeedProject();
  const main = page.locator('main');

  await authenticatedGoto(`/projects/${project.id}/backlog`);
  await expect(page.getByRole('heading', { name: 'Planejamento do backlog' })).toBeVisible();
  await page.screenshot({ path: `${articles}/quadro-backlog-e-sprints/imagens/backlog.png`, animations: 'disabled' });

  await authenticatedGoto(`/projects/${project.id}/sprints`);
  await expect(page.getByRole('button', { name: 'Nova sprint' })).toBeVisible();
  await page.screenshot({ path: `${articles}/quadro-backlog-e-sprints/imagens/sprints.png`, animations: 'disabled' });

  await authenticatedGoto(`/projects/${project.id}/boards`);
  await expect(page.getByRole('button', { name: 'Nova Coluna' })).toBeVisible();
  await page.screenshot({ path: `${articles}/quadro-backlog-e-sprints/imagens/kanban.png`, animations: 'disabled' });

  await authenticatedGoto(`/projects/${project.id}/settings?secao=pessoas`);
  await expect(page.getByRole('heading', { name: 'Membros e papéis' })).toBeVisible();
  await main.screenshot({ path: `${articles}/organizacao-e-equipes/imagens/pessoas-do-projeto.png`, animations: 'disabled' });

  await authenticatedGoto('/teams');
  await expect(page.getByRole('button', { name: 'Criar equipe' })).toBeVisible();
  await main.screenshot({ path: `${articles}/organizacao-e-equipes/imagens/equipes.png`, animations: 'disabled' });
});
