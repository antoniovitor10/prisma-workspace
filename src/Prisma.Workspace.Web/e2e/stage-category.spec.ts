import { test, expect, authenticatedApiGet } from './fixtures/test';

type Stage = { id: string; name: string; category: number };

const DONE = 5;

/**
 * Regressão E03: a criação mostrava um seletor de classificação que foi entendido
 * como uma lista de colunas existentes e não deixava claro o efeito. A criação deve
 * ser simples e previsível; classificações com impacto pertencem à edição da coluna.
 */
test('coluna é criada sem combobox ambígua e pode ser reclassificada na edição',
  async ({ page, authenticatedGoto, resolveSeedProject }) => {
    const project = await resolveSeedProject();
    const board = project.boards[0];
    const nome = `Entregue E2E ${Date.now()}`;

    await authenticatedGoto(`/boards/${board.id}`);
    await page.getByRole('button', { name: 'Nova Coluna' }).click();

    await page.getByPlaceholder('Nome da Coluna').fill(nome);
    await expect(page.getByRole('combobox', { name: 'Classificação da coluna' })).toHaveCount(0);
    const [response] = await Promise.all([
      page.waitForResponse(result => result.request().method() === 'POST'
        && /\/api\/Stages\/board\/[0-9a-f-]+(?:\?|$)/i.test(result.url())),
      page.getByRole('button', { name: 'Adicionar' }).click(),
    ]);
    expect(response.ok(), await response.text()).toBeTruthy();

    await expect(page.getByPlaceholder('Nome da Coluna')).toHaveCount(0);
    await expect(page.getByRole('button', { name: `Editar coluna ${nome}`, exact: true })).toBeVisible();

    const stages = await authenticatedApiGet<Stage[]>(page, `/api/Stages/board/${board.id}`);
    const criada = stages.find((stage) => stage.name === nome);
    expect(criada, 'A coluna criada precisa existir no quadro.').toBeTruthy();
    expect(criada!.category).toBe(3);
    await page.getByRole('button', { name: `Editar coluna ${nome}`, exact: true }).click();
    const renamed = `${nome} revisada`;
    await page.getByPlaceholder('Nome da Coluna').fill(renamed);
    await page.getByRole('combobox', { name: 'Classificação da coluna' })
      .selectOption({ label: 'Concluída' });
    // A confirmação depende da consulta assíncrona, mesmo em uma coluna vazia.
    await expect(page.getByLabel('Impacto da reclassificação')).toContainText('0 tarefa(s) na coluna');
    const [updated] = await Promise.all([
      page.waitForResponse(result => result.request().method() === 'PUT'
        && result.url().includes(`/Stages/${criada!.id}`)),
      page.getByRole('button', { name: 'Salvar', exact: true }).click(),
    ]);
    expect(updated.status()).toBe(204);
    await page.reload();
    await expect(page.getByRole('button', { name: `Editar coluna ${renamed}`, exact: true })).toBeVisible();
    const persisted = await authenticatedApiGet<Stage[]>(page, `/api/Stages/board/${board.id}`);
    expect(persisted.find(stage => stage.id === criada!.id)).toMatchObject({ name: renamed, category: DONE });
  });
