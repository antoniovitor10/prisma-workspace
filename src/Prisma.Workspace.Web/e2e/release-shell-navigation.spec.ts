import { test, expect, authenticatedApiGet } from './fixtures/test';

function isMobileProject(projectName: string) {
  return projectName === 'chromium-mobile';
}

test.describe('release shell e navegação', () => {
  test('TASK-032 apresenta shell superior responsivo e controles globais', async ({
    page,
    authenticatedGoto,
  }, testInfo) => {
    await authenticatedGoto('/projects');

    await expect(page.getByRole('main')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Abrir pesquisa global (Ctrl K)' })).toBeVisible();
    // O seletor de organização aparece a partir da segunda organização: com uma só ele
    // não oferece escolha alguma e saiu da barra. Aqui a asserção acompanha a contagem
    // real, em vez de exigir um controle que pode legitimamente não existir.
    const organizacoes = await authenticatedApiGet<unknown[]>(page, '/api/organizations');
    const seletorOrg = page.getByRole('combobox', { name: 'Selecionar organização' });
    if (organizacoes.length > 1) await expect(seletorOrg).toBeVisible();
    else await expect(seletorOrg).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Novo item' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Menu da conta' })).toBeVisible();

    // Fora do projeto a lateral contextual não aparece (D88).
    await expect(page.getByRole('navigation', { name: 'Navegação do projeto' })).toHaveCount(0);

    if (isMobileProject(testInfo.project.name)) {
      const trigger = page.getByRole('button', { name: 'Abrir menu' });
      await expect(trigger).toBeVisible();
      await trigger.click();
      const menu = page.getByRole('dialog', { name: 'Menu de navegação' });
      await expect(menu).toBeVisible();
      await expect(menu.getByRole('menuitem', { name: 'Projetos' })).toBeVisible();
      await expect(menu.getByRole('menuitem').first()).toBeFocused();
      await page.keyboard.press('Escape');
      await expect(menu).toHaveCount(0);
      await expect(trigger).toBeFocused();

      await trigger.click();
      await page.mouse.click(page.viewportSize()!.width - 2, page.viewportSize()!.height - 2);
      await expect(menu).toHaveCount(0);
    } else {
      // D88: no desktop a navegação global vive no cabeçalho.
      const navigation = page.getByRole('navigation', { name: 'Navegação principal' });
      await expect(navigation).toBeVisible();
      await expect(navigation.getByRole('link', { name: 'Projetos' })).toHaveAttribute('aria-current', 'page');
      await navigation.getByRole('link', { name: 'Solicitações' }).click();
      await expect(page).toHaveURL(/\/requests$/);
      await expect(navigation.getByRole('link', { name: 'Solicitações' })).toHaveAttribute('aria-current', 'page');
    }
  });

  test('D88: painel do projeto aparece só dentro do projeto', async ({
    page,
    authenticatedGoto,
    resolveSeedProject,
  }, testInfo) => {
    const project = await resolveSeedProject();
    await authenticatedGoto(`/projects/${project.id}/items`);

    if (isMobileProject(testInfo.project.name)) {
      // No mobile a lateral some; as abas ficam na faixa horizontal do workspace.
      await expect(page.getByRole('navigation', { name: 'Navegação do projeto' })).toHaveCount(0);
      const areas = page.getByRole('navigation', { name: 'Áreas do projeto' });
      await expect(areas).toBeVisible();
      await expect(areas.getByRole('link', { name: 'Itens' })).toHaveAttribute('aria-current', 'page');
      await areas.getByRole('link', { name: 'Kanban' }).click();
      await expect(page).toHaveURL(new RegExp(`/projects/${project.id}/boards$`));
    } else {
      const painel = page.getByRole('navigation', { name: 'Navegação do projeto' });
      await expect(painel).toBeVisible();
      await expect(painel.getByRole('heading', { name: project.name })).toBeVisible();
      const areas = painel.getByRole('navigation', { name: 'Áreas do projeto' });
      await expect(areas.getByRole('link', { name: 'Itens' })).toHaveAttribute('aria-current', 'page');
      await areas.getByRole('link', { name: 'Backlog' }).click();
      await expect(page).toHaveURL(new RegExp(`/projects/${project.id}/backlog$`));
      await expect(areas.getByRole('link', { name: 'Backlog' })).toHaveAttribute('aria-current', 'page');
    }

    await authenticatedGoto('/projects');
    await expect(page.getByRole('navigation', { name: 'Navegação do projeto' })).toHaveCount(0);
  });

  test('TASK-025 preserva breadcrumb, alternância e histórico sem reload', async ({
    page,
    authenticatedGoto,
    resolveSeedProject,
  }) => {
    const project = await resolveSeedProject();
    await authenticatedGoto(`/projects/${project.id}/backlog`);

    await expect(page.getByText('Carregando visão...', { exact: true })).toHaveCount(0, { timeout: 15_000 });
    // Breadcrumb aponta para a lista; a alternância Backlog/Quadro vive na barra de contexto.
    await expect(page.getByRole('link', { name: 'Projetos', exact: true }).first()).toHaveAttribute('href', '/projects');
    const viewSwitcher = page.getByRole('navigation', { name: 'Alternar visão do projeto' });
    await expect(viewSwitcher).toBeVisible({ timeout: 15_000 });
    const backlog = viewSwitcher.getByRole('link', { name: 'Backlog' });
    const board = viewSwitcher.getByRole('link', { name: 'Quadro' });
    await expect(backlog).toHaveAttribute('aria-current', 'page');

    const navigationEntriesBefore = await page.evaluate(() => performance.getEntriesByType('navigation').length);
    await board.click();
    await expect(page).toHaveURL(new RegExp(`/projects/${project.id}/boards$`));
    await expect(board).toHaveAttribute('aria-current', 'page');
    await expect.poll(() => page.evaluate(() => performance.getEntriesByType('navigation').length))
      .toBe(navigationEntriesBefore);

    await page.goBack();
    await expect(page).toHaveURL(new RegExp(`/projects/${project.id}/backlog$`));
    await expect(backlog).toHaveAttribute('aria-current', 'page');
    await page.goForward();
    await expect(page).toHaveURL(new RegExp(`/projects/${project.id}/boards$`));
    await expect(board).toHaveAttribute('aria-current', 'page');
  });
});
