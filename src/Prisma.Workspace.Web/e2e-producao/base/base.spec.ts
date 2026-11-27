import { test, expect } from '../fixtures/projeto-teste';

type Detalhe = { id: string; boardId: string; stageId: string | null; isArchived: boolean };
type Coluna = { id: string; name: string };

/**
 * Base fixa da suíte de produção (CA-4, CA-8). Roda sempre, no desktop e no Pixel 5:
 * projeto → quadro → backlog → criar tarefa → mover para a coluna seguinte à de
 * criação (seja qual for o nome) → abrir o detalhe → arquivar pela gaveta →
 * conferir que não sobrou tarefa aberta com o prefixo.
 */
test('base: projeto, quadro, backlog, criar, mover, detalhe, arquivar e sem sobras', async ({ page, producao }) => {
  const titulo = producao.tituloDeTeste();
  const alternador = page.getByRole('navigation', { name: 'Alternar visão do projeto' });
  const colunas = page.locator('[data-stage-id]');
  const coluna = (id: string) => page.locator(`[data-stage-id="${id}"]`);
  const marcadorDoCartao = (id: string) => coluna(id).getByRole('checkbox', { name: titulo });

  await test.step('abrir o projeto de teste', async () => {
    await producao.abrirProjeto();
    await producao.print('projeto');
  });

  await test.step('abrir o quadro', async () => {
    await alternador.getByRole('link', { name: 'Quadro', exact: true }).click();
    await expect(page).toHaveURL(new RegExp(`/projects/${producao.contexto.projetoId}/boards`));
    await expect(colunas.first()).toBeVisible();
    await producao.print('quadro');
  });

  await test.step('abrir o backlog', async () => {
    await alternador.getByRole('link', { name: 'Backlog', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Planejamento do backlog' })).toBeVisible();
    await producao.print('backlog');
  });

  const tarefaId = await test.step('criar a tarefa no backlog', async () => {
    await page.getByRole('button', { name: 'Novo item', exact: true }).click();
    await page.getByRole('textbox', { name: 'Título do novo item', exact: true }).fill(titulo);
    const criacao = page.waitForResponse(resposta => resposta.request().method() === 'POST'
      && new URL(resposta.url()).pathname.toLowerCase() === '/api/workitems');
    await page.getByRole('button', { name: 'Adicionar', exact: true }).click();
    const resposta = await criacao;
    expect(resposta.status(), 'A API precisa aceitar a criação da tarefa.').toBe(201);
    await expect(page.getByRole('textbox', { name: `Título de ${titulo}`, exact: true })).toBeVisible();
    await producao.print('tarefa-criada');
    return String(await resposta.json());
  });

  const criada = await producao.apiGet<Detalhe>(`/api/WorkItems/${tarefaId}`);
  expect(producao.contexto.quadroIds, 'A tarefa precisa nascer num quadro do projeto de teste.').toContain(criada.boardId);
  expect(criada.stageId, 'A tarefa precisa nascer numa coluna.').toBeTruthy();
  const nomesDasColunas = await producao.apiGet<Coluna[]>(`/api/Stages/board/${criada.boardId}`);

  const destino = await test.step('mover para a coluna seguinte', async () => {
    await alternador.getByRole('link', { name: 'Quadro', exact: true }).click();
    const seletorDeQuadro = page.getByRole('combobox', { name: 'Selecionar quadro', exact: true });
    // Espera o quadro carregar antes de ler o seletor: lido cedo, ele ainda está vazio ou desabilitado.
    await expect(colunas.first()).toBeVisible();
    await expect(seletorDeQuadro).toBeEnabled();
    if (await seletorDeQuadro.inputValue() !== criada.boardId)
      await seletorDeQuadro.selectOption(criada.boardId);
    await expect(marcadorDoCartao(criada.stageId!)).toBeVisible();

    // A coluna seguinte é a da direita na tela, seja qual for o nome.
    const ordem = await colunas.evaluateAll(elementos => elementos.map(elemento => elemento.getAttribute('data-stage-id')));
    const indice = ordem.indexOf(criada.stageId);
    expect(indice, 'A coluna de criação precisa aparecer no quadro.').toBeGreaterThanOrEqual(0);
    expect(indice, 'A tarefa nasceu na última coluna: não há coluna seguinte.').toBeLessThan(ordem.length - 1);
    const proxima = ordem[indice + 1]!;

    // O cartão é o div mais interno da coluna que contém o marcador e o botão de mover.
    const cartao = coluna(criada.stageId!).locator('div')
      .filter({ has: marcadorDoCartao(criada.stageId!) })
      .filter({ has: page.getByRole('button', { name: 'Mover tarefa para a próxima coluna' }) })
      .last();
    const movimento = page.waitForResponse(resposta => resposta.request().method() === 'POST'
      && new URL(resposta.url()).pathname.toLowerCase() === '/api/workitems/move');
    await cartao.getByRole('button', { name: 'Mover tarefa para a próxima coluna', exact: true }).click();
    expect((await movimento).status(), 'A API precisa aceitar o movimento.').toBe(204);
    await expect(marcadorDoCartao(proxima)).toBeVisible();
    await expect.poll(async () => (await producao.apiGet<Detalhe>(`/api/WorkItems/${tarefaId}`)).stageId).toBe(proxima);
    await producao.print('tarefa-movida');
    return nomesDasColunas.find(item => item.id === proxima);
  });

  const gaveta = page.getByRole('dialog', { name: titulo });
  await test.step('abrir o detalhe', async () => {
    await coluna(destino!.id).getByRole('heading', { name: titulo }).click();
    await expect(gaveta).toBeVisible();
    await expect(gaveta.getByLabel('Coluna atual', { exact: true })).toHaveText(destino!.name);
    await producao.print('detalhe');
  });

  await test.step('arquivar pela gaveta', async () => {
    const arquivamento = page.waitForResponse(resposta => resposta.request().method() === 'DELETE'
      && new URL(resposta.url()).pathname.toLowerCase() === `/api/workitems/${tarefaId.toLowerCase()}`);
    await gaveta.getByRole('button', { name: 'Arquivar', exact: true }).click();
    expect((await arquivamento).status(), 'A API precisa aceitar o arquivamento.').toBe(204);
    await expect(gaveta).toBeHidden();
    await expect(page.getByRole('checkbox', { name: titulo })).toHaveCount(0);
    expect((await producao.apiGet<Detalhe>(`/api/WorkItems/${tarefaId}`)).isArchived).toBe(true);
    await producao.print('arquivada');
  });

  await test.step('conferir que não sobrou tarefa aberta com o prefixo', async () => {
    await expect.poll(async () => (await producao.tarefasAbertasComPrefixo()).map(tarefa => tarefa.title)).toEqual([]);
  });
});
