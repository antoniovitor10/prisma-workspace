import { test, expect } from './fixtures/test';

function isMobileProject(projectName: string) {
  return projectName === 'chromium-mobile';
}

test.describe('release shell e navegação', () => {
  test('TASK-032 apresenta shell superior responsivo e controles globais', async ({
    page,
    authenticatedGoto,
  }, testInfo) => {
    await authenticatedGoto('/projects');

    await expect(page.locator('aside')).toHaveCount(0);
    await expect(page.getByRole('main')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Abrir pesquisa global (Ctrl K)' })).toBeVisible();
    await expect(page.getByRole('combobox', { name: 'Selecionar organização' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Novo item' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Menu da conta' })).toBeVisible();

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
      // D87: no desktop a navegação vive no trilho lateral recolhível, não em abas na barra.
      const navigation = page.getByRole('navigation', { name: 'Navegação lateral' });
      await expect(navigation).toBeVisible();
      await expect(page.getByRole('navigation', { name: 'Navegação principal' })).toHaveCount(0);
      await expect(navigation.getByRole('link', { name: 'Projetos' })).toHaveAttribute('aria-current', 'page');
      await navigation.getByRole('link', { name: 'Solicitações' }).click();
      await expect(page).toHaveURL(/\/requests$/);
      await expect(navigation.getByRole('link', { name: 'Solicitações' })).toHaveAttribute('aria-current', 'page');

      // Recolhido, o trilho é mais estreito que os rótulos que mostra expandido; expandir
      // precisa alargá-lo de verdade e a escolha precisa sobreviver a uma nova visita.
      const expandir = page.getByRole('button', { name: 'Expandir navegação' });
      const larguraRecolhido = (await navigation.boundingBox())!.width;
      await expandir.click();
      const recolher = page.getByRole('button', { name: 'Recolher navegação' });
      await expect(recolher).toBeVisible();
      await expect.poll(async () => (await navigation.boundingBox())!.width)
        .toBeGreaterThan(larguraRecolhido);

      await authenticatedGoto('/projects');
      await expect(page.getByRole('button', { name: 'Recolher navegação' })).toBeVisible();
    }
  });

  test('TASK-025 preserva breadcrumb, alternância e histórico sem reload', async ({
    page,
    authenticatedGoto,
    resolveSeedProject,
  }) => {
    const project = await resolveSeedProject();
    await authenticatedGoto(`/projects/${project.id}/backlog`);

    const projectsLink = page.getByRole('link', { name: 'Projetos', exact: true }).last();
    await expect(projectsLink).toHaveAttribute('href', '/projects');
    await expect(page.getByText('Carregando visão...', { exact: true })).toHaveCount(0, { timeout: 15_000 });
    await expect(page.getByText(project.name, { exact: true }).first())
      .toHaveAttribute('aria-current', 'page', { timeout: 15_000 });

    const viewSwitcher = page.getByRole('navigation', { name: 'Alternar visão do projeto' });
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
