import { test, expect } from './fixtures/test';

/**
 * HU-0003: central de ajuda (/ajuda), botão "?" por página e por seção.
 * Os artigos vêm do bundle do front: nada aqui depende de dado do banco além do login.
 */
test.describe('Central de ajuda', () => {
  test('lista por assunto, busca pelo teclado e mostra o artigo com "Atualizado em" (CA-1, CA-11)', async ({ page, authenticatedGoto }) => {
    await authenticatedGoto('/ajuda');
    await expect(page.getByRole('heading', { level: 1, name: 'Central de ajuda' })).toBeVisible();
    const list = page.getByRole('navigation', { name: 'Artigos por assunto' });
    for (const subject of ['Organização, projetos e equipes', 'Quadro, backlog e sprints', 'Integrações', 'Repositórios e Git']) {
      await expect(list.getByRole('heading', { name: subject })).toBeVisible();
    }

    const search = page.getByRole('searchbox', { name: 'Buscar na ajuda' });
    await search.focus();
    await page.keyboard.type('installation id');
    await expect(page.getByRole('status').filter({ hasText: 'encontrado' })).toBeVisible();
    await page.keyboard.press('Tab');
    const firstResult = page.getByRole('list', { name: 'Resultados da busca' }).getByRole('link').first();
    await expect(firstResult).toBeFocused();
    await expect(firstResult).toContainText('Conectar um repositório do GitHub');
    await page.keyboard.press('Enter');

    await expect(page).toHaveURL(/\/ajuda\/repositorios-e-git\/github-app#installation-id$/);
    const article = page.getByRole('article', { name: 'Conectar um repositório do GitHub' });
    await expect(article.getByText(/^Atualizado em \d{2}\/\d{2}\/\d{4}$/)).toBeVisible();
    await expect(article.getByRole('heading', { name: 'Onde achar o Installation ID' })).toBeFocused();
    // CA-6: a parte de administração cita nomes de configuração, nunca valores.
    await expect(article.getByText('MethodGit__Secrets__<nome>', { exact: false }).first()).toBeVisible();
  });

  test('funciona no tema escuro e sem rolagem horizontal (CA-11)', async ({ page, authenticatedGoto }) => {
    await authenticatedGoto('/ajuda/integracoes/tokens');
    const article = page.getByRole('article', { name: 'Tokens de agente, humano e sistema' });
    await expect(article).toBeVisible();
    const toggle = page.getByRole('button', { name: /Ativar modo (escuro|claro)/ });
    if ((await toggle.getAttribute('aria-label')) === 'Ativar modo escuro') await toggle.click();
    await expect(page.getByRole('button', { name: 'Ativar modo claro' })).toBeVisible();
    await expect(page.locator('body')).toHaveCSS('background-color', 'rgb(7, 11, 23)');
    await expect(article).toHaveCSS('background-color', 'rgb(14, 20, 36)');
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow, 'a central de ajuda não pode criar rolagem horizontal').toBeLessThanOrEqual(1);
    await page.getByRole('button', { name: 'Ativar modo claro' }).click();
  });

  test('o "?" da página abre o artigo na seção certa sem perder o que foi digitado (CA-3)', async ({ page, authenticatedGoto, resolveSeedProject }) => {
    await authenticatedGoto('/projects');
    const project = await resolveSeedProject();
    await authenticatedGoto(`/projects/${project.id}/backlog`);
    // Rascunho aberto e não salvo: o painel da ajuda não pode descartá-lo.
    await page.locator('main').getByRole('button', { name: 'Novo item' }).click();
    const title = page.getByLabel('Título do novo item');
    await title.fill('Rascunho que não pode sumir');

    const helpButton = page.getByRole('button', { name: 'Ajuda desta página' });
    await helpButton.click();
    const dialog = page.getByRole('dialog', { name: 'Backlog e hierarquia de itens' });
    await expect(dialog).toBeVisible();
    await expect(dialog.getByRole('heading', { name: 'Backlog', exact: true })).toBeFocused();
    await expect(dialog.getByText(/^Atualizado em/)).toBeVisible();
    await expect(dialog.getByRole('link', { name: 'Abrir na central de ajuda' })).toHaveAttribute('href', '/ajuda/quadro-backlog-e-sprints/backlog#backlog');

    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();
    await expect(helpButton).toBeFocused();
    await expect(title).toHaveValue('Rascunho que não pode sumir');
    await expect(page).toHaveURL(new RegExp(`/projects/${project.id}/backlog`));
  });

  test('cada página com artigo abre um artigo e uma seção que existem (CA-8)', async ({ page, authenticatedGoto, resolveSeedProject }) => {
    await authenticatedGoto('/projects');
    const project = await resolveSeedProject();
    const pages: { path: string; article: string; section?: string }[] = [
      { path: '/projects', article: 'Projetos, acesso e configurações do projeto', section: 'Criar um projeto' },
      { path: `/projects/${project.id}/sprints`, article: 'Sprints', section: 'Visão geral' },
      { path: `/projects/${project.id}/boards`, article: 'Quadro Kanban', section: 'Quadros' },
      { path: `/projects/${project.id}/settings?secao=pessoas`, article: 'Projetos, acesso e configurações do projeto', section: 'Dar acesso ao projeto' },
      { path: '/teams', article: 'Equipes', section: 'Criar uma equipe' },
    ];
    for (const target of pages) {
      await authenticatedGoto(target.path);
      await page.getByRole('button', { name: 'Ajuda desta página' }).click();
      const dialog = page.getByRole('dialog', { name: target.article });
      await expect(dialog, target.path).toBeVisible();
      if (target.section) await expect(dialog.getByRole('heading', { name: target.section, exact: true }), target.path).toBeFocused();
      await dialog.getByRole('button', { name: 'Fechar ajuda' }).click();
      await expect(dialog).toBeHidden();
    }
  });

  test('o "?" da seção de membros abre a ajuda de convites e a central é alcançável pelo menu (CA-3)', async ({ page, authenticatedGoto }, testInfo) => {
    await authenticatedGoto('/settings');
    await page.getByRole('button', { name: 'Ajuda sobre membros e convites' }).click();
    const dialog = page.getByRole('dialog', { name: 'Organização, membros e convites' });
    await expect(dialog.getByRole('heading', { name: 'Convidar pessoas', exact: true })).toBeFocused();
    await dialog.getByRole('link', { name: 'Abrir na central de ajuda' }).click();
    await expect(page).toHaveURL(/\/ajuda\/organizacao-e-equipes\/organizacao#convidar-pessoas$/);
    await expect(page.getByRole('article', { name: 'Organização, membros e convites' })).toBeVisible();

    await authenticatedGoto('/reports');
    if (testInfo.project.name === 'chromium-mobile') {
      await page.getByRole('button', { name: 'Abrir menu' }).click();
      await page.getByRole('menuitem', { name: 'Central de ajuda' }).click();
    } else {
      await page.getByRole('button', { name: 'Menu da conta' }).click();
      await page.getByRole('menuitem', { name: 'Central de ajuda' }).click();
    }
    await expect(page.getByRole('heading', { level: 1, name: 'Central de ajuda' })).toBeVisible();
  });
});
