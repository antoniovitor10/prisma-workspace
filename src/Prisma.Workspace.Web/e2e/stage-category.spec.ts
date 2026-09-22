import { test, expect, authenticatedApiGet } from './fixtures/test';

type Stage = { id: string; name: string; category: number };

const DONE = 5;

/**
 * Regressão: a criação de coluna pelo Kanban não enviava classificação, então toda
 * coluna nascia como "em andamento" — inclusive uma chamada "Concluído". Como
 * CompletedAt só é gravado quando a etapa de destino é da categoria Done, nenhuma
 * tarefa nessa coluna era contada como concluída, e não havia como corrigir o dado
 * pela interface porque StageDto sequer expunha a categoria.
 */
test('coluna criada pelo Kanban respeita a classificação escolhida',
  async ({ page, authenticatedGoto, resolveSeedProject }) => {
    const project = await resolveSeedProject();
    const board = project.boards[0];
    const nome = `Entregue E2E ${Date.now()}`;

    await authenticatedGoto(`/boards/${board.id}`);
    await page.getByRole('button', { name: 'Nova Coluna' }).click();

    await page.getByPlaceholder('Nome da Coluna').fill(nome);
    await page.getByRole('combobox', { name: 'Classificação da coluna' })
      .selectOption({ label: 'Concluída' });
    const [response] = await Promise.all([
      page.waitForResponse(result => result.request().method() === 'POST'
        && /\/api\/Stages\/board\/[0-9a-f-]+(?:\?|$)/i.test(result.url())),
      page.getByRole('button', { name: 'Adicionar' }).click(),
    ]);
    expect(response.ok(), await response.text()).toBeTruthy();

    await expect(page.getByPlaceholder('Nome da Coluna')).toHaveCount(0);
    await expect(page.getByText(nome).first()).toBeVisible();

    const stages = await authenticatedApiGet<Stage[]>(page, `/api/Stages/board/${board.id}`);
    const criada = stages.find((stage) => stage.name === nome);
    expect(criada, 'A coluna criada precisa existir no quadro.').toBeTruthy();
    // Antes da correção esta coluna nascia com category 3 (Em andamento).
    expect(criada!.category).toBe(DONE);
    await page.getByRole('button', { name: `Editar coluna ${nome}`, exact: true }).click();
    const renamed = `${nome} revisada`;
    await page.getByPlaceholder('Nome da Coluna').fill(renamed);
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
