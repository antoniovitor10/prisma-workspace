import type { ChildProcess } from 'node:child_process';
import { test, expect } from './fixtures/test';
import { humanSession, type Api } from './fixtures/method-git';
import { createPollProject, fakePoll, pollEnabled, setPollPullRequest, startGitHubFake } from './fixtures/method-git-poll';

/**
 * HU-0002: Git visível no card e na tarefa, com o modo sem admin (consulta com token de leitura).
 * T-0005 (selos no card), T-0006 (aba Git), T-0007 (token e cadastro sem App) e T-0008 (Consultar agora e movimento).
 * Dados próprios por execução (projeto, token falso e repositório no GitHub falso); desktop e mobile.
 */
test.describe('Git no card, aba Git e modo consulta (HU-0002)', () => {
  test.skip(!pollEnabled, 'Requer E2E_GITHUB_FAKE_PORT e a API apontando para o GitHub falso (scripts/ci-e2e.sh).');
  test.describe.configure({ mode: 'serial' });

  let fake: ChildProcess | undefined;
  let human: Api;

  test.beforeAll(async () => {
    test.setTimeout(60_000);
    fake = await startGitHubFake();
    human = (await humanSession()).api;
  });
  test.afterAll(() => { fake?.kill(); });

  test('T-0007: cadastra o token da organização e conecta o repositório sem App (CA-9, CA-13b)', async ({ page, authenticatedGoto }) => {
    const p = await createPollProject(human, 'cadastro', false);
    await authenticatedGoto(`/projects/${p.projectId}/settings?secao=metodo`);
    await page.getByRole('button', { name: 'Repositórios' }).click();

    const tokenForm = page.getByRole('form', { name: 'Salvar token de leitura' });
    await tokenForm.getByLabel('Organização GitHub').fill(p.owner);
    await tokenForm.getByLabel('Token de leitura').fill(p.token);
    await tokenForm.getByRole('button', { name: 'Salvar token' }).click();
    await expect(page.getByText('Token de leitura salvo e cifrado. Só o começo dele fica visível.')).toBeVisible();
    const tokens = page.getByRole('list', { name: 'Tokens de leitura' });
    await expect(tokens).toContainText(p.owner);
    await expect(tokens).toContainText(p.token.slice(0, 16));
    await expect(tokenForm.getByLabel('Token de leitura')).toHaveValue('');
    await expect(page.locator('body')).not.toContainText(p.token);

    const connect = page.getByRole('form', { name: 'Conectar repositório' });
    await expect(connect.getByLabel('GitHub App ID')).toBeVisible();
    await connect.getByRole('radio', { name: 'Consulta com token de leitura' }).check();
    await expect(connect.getByLabel('GitHub App ID')).toHaveCount(0);
    await expect(connect.getByLabel('Installation ID')).toHaveCount(0);
    await connect.getByLabel('Repositório').fill(p.repositoryFullName);
    await connect.getByLabel('Branch principal').fill('main');
    await connect.getByLabel('Sistema de integração').selectOption(p.systemPrincipalId);
    await connect.getByRole('button', { name: 'Conectar repositório' }).click();
    await expect(page.getByText(/Repositório conectado no modo consulta/)).toBeVisible();
    const row = page.getByRole('listitem', { name: `Repositório ${p.repositoryFullName}` });
    await expect(row).toContainText('Consulta com token de leitura');
    await expect(row).toContainText('última consulta com sucesso');
    await expect(row.getByLabel('URL do webhook')).toHaveCount(0);
    // A API nunca devolve o token: nem a lista de tokens nem a de repositórios.
    const listed = await human.get(`/api/projects/${p.projectId}/method/git-credentials`);
    expect(await listed.text()).not.toContain(p.token);
  });

  test('T-0007/T-0008: token recusado pelo GitHub aparece na tela de Repositórios e nada se move (CA-13)', async ({ page, authenticatedGoto }) => {
    const p = await createPollProject(human, 'erro', true);
    await fakePoll('PUT', '/__fixture/read-token', { token: p.token, repos: [p.repositoryFullName], status: 401 });
    await authenticatedGoto(`/projects/${p.projectId}/settings?secao=metodo`);
    await page.getByRole('button', { name: 'Repositórios' }).click();
    const row = page.getByRole('listitem', { name: `Repositório ${p.repositoryFullName}` });
    await row.getByRole('button', { name: 'Consultar agora' }).click();
    await expect(row.getByRole('alert')).toContainText('O GitHub recusou o token de leitura');
    await expect(row).toContainText('última consulta com sucesso');
    await expect(page.locator('body')).not.toContainText('Bad credentials');
  });

  test('T-0005/T-0006/T-0008: card e aba Git mostram branch, PR e repositório; Consultar agora move o card (CA-1 a CA-7, CA-10, CA-11)', async ({ page, authenticatedGoto }) => {
    test.setTimeout(120_000);
    const p = await createPollProject(human, 'fluxo', true);
    const branch = `feat/pagamento-${Date.now().toString(36)}`;
    const id = 'T-0101';
    await human.json(human.post(`/api/projects/${p.projectId}/method/tasks`, {
      id, title: 'Pagamento pelo app', hu: 'HU-9002', filePath: `docs/trabalho/HU-9002-e2e/tasks/${id}.md`, covers: ['CA-1'], risk: 'normal', status: 'backlog' }));
    await human.json(human.post(`/api/projects/${p.projectId}/method/tasks/${id}/move`, { newStatus: 'em-desenvolvimento', reason: 'humano autoriza' }));
    await human.json(human.post(`/api/projects/${p.projectId}/method/tasks/${id}/branches`, { repository: p.repositoryFullName, branch }));

    // CA-1 e CA-3: o card mostra o repositório (nome curto, completo no tooltip) e a branch local como "não enviada".
    await authenticatedGoto(`/boards/${p.boardId}`);
    const git = page.getByRole('list', { name: 'Git' });
    await expect(git.getByTitle(p.repositoryFullName)).toContainText('canais-mobile');
    await expect(git.getByTitle(`${branch} (não enviada)`)).toBeVisible();

    // CA-5: a aba Git do detalhe lista a branch com o link do GitHub.
    await page.getByRole('heading', { name: /Pagamento pelo app/ }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('button', { name: 'Git', exact: true }).click();
    const branches = dialog.getByRole('list', { name: 'Branches da tarefa' });
    await expect(branches.getByRole('link', { name: `Abrir a branch ${branch} no GitHub` }))
      .toHaveAttribute('href', `https://github.com/${p.repositoryFullName}/tree/${branch}`);
    await expect(branches).toContainText('não enviada');

    // O dev envia a branch e abre o PR; "Consultar agora" lê o GitHub na hora (CA-10).
    await fakePoll('PUT', `/__fixture/branches?repo=${encodeURIComponent(p.repositoryFullName)}`, [branch]);
    await setPollPullRequest(p, { number: 5, branch, title: 'Pagamento pelo app' });
    await dialog.getByRole('button', { name: 'Consultar agora' }).click();
    await expect(dialog.getByText(/Consulta concluída em .*1 PR\(s\) reavaliado\(s\) e 1 branch\(es\) enviada\(s\)/)).toBeVisible();

    // CA-11: a reavaliação passa pela fila do webhook e move o card para Em revisão, pelo canal consulta.
    await expect.poll(async () => (await human.json<{ status: string }>(human.get(`/api/projects/${p.projectId}/method/tasks/${id}`))).status,
      { timeout: 30_000, message: 'a consulta deveria mover o card para em-revisao' }).toBe('em-revisao');
    await page.reload();
    await page.getByRole('heading', { name: /Pagamento pelo app/ }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Git', exact: true }).click();
    const reopened = page.getByRole('dialog');
    const prs = reopened.getByRole('list', { name: 'PRs da tarefa' });
    await expect(prs.getByRole('link', { name: 'Abrir o PR #5 no GitHub' })).toHaveAttribute('href', `https://github.com/${p.repositoryFullName}/pull/5`);
    await expect(prs).toContainText('Aprovações: 0');
    // CA-6: linha do tempo com branch vinculada, enviada e PR aberto, com autor e canal.
    const timeline = reopened.getByRole('list', { name: 'Linha do tempo do Git' });
    await expect(timeline).toContainText(`Branch ${branch} vinculada`);
    await expect(timeline).toContainText(`Branch ${branch} enviada ao GitHub`);
    await expect(timeline).toContainText('PR #5 aberto; card para Em revisão');
    await expect(timeline).toContainText('canal: consulta');
    await expect(timeline).toContainText('canal: tela');

    // CA-7: vincular à mão uma branch e a URL de um PR do projeto.
    await setPollPullRequest(p, { number: 6, branch: 'feat/outra-entrega', title: 'Outra entrega' });
    const branchForm = reopened.getByRole('form', { name: 'Vincular branch' });
    await branchForm.getByLabel('Nome da branch').fill('feat/manual-e2e');
    await branchForm.getByRole('button', { name: 'Vincular branch' }).click();
    await expect(reopened.getByText('Branch feat/manual-e2e vinculada.')).toBeVisible();
    const prForm = reopened.getByRole('form', { name: 'Vincular PR' });
    await prForm.getByLabel('URL do PR').fill(`https://github.com/${p.repositoryFullName}/pull/6`);
    await prForm.getByRole('button', { name: 'Vincular PR' }).click();
    await expect(reopened.getByText('PR vinculado.')).toBeVisible();
    await expect(prs.getByRole('link', { name: 'Abrir o PR #6 no GitHub' })).toBeVisible();
    // URL de repositório fora do projeto é recusada com mensagem clara.
    await prForm.getByLabel('URL do PR').fill('https://github.com/outro-dono/outro-repo/pull/1');
    await prForm.getByRole('button', { name: 'Vincular PR' }).click();
    await expect(reopened.getByText(/URL não pertence a repositório autorizado/)).toBeVisible();

    // CA-2 e CA-4: no quadro, o card mostra o PR aberto mais recente com o estado.
    await reopened.getByRole('button', { name: 'Fechar modal' }).click();
    await page.reload();
    await expect(page.getByRole('list', { name: 'Git' }).getByTitle(/^PR #(5|6): aberto$/)).toBeVisible();
  });
});
